using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Exceptions;
using CampaignManager.Application.Jobs;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Campaigns.Series;

public sealed record SaveCampaignSeriesCommand(SaveCampaignSeriesInput Input) : IRequest<Guid>;

public sealed class SaveCampaignSeriesHandler : IRequestHandler<SaveCampaignSeriesCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly ISeriesScheduler _scheduler;

    public SaveCampaignSeriesHandler(IAppDbContext db, ICurrentTenant tenant, ISeriesScheduler scheduler)
    {
        _db = db;
        _tenant = tenant;
        _scheduler = scheduler;
    }

    public async Task<Guid> Handle(SaveCampaignSeriesCommand command, CancellationToken ct)
    {
        var organizationId = _tenant.OrganizationId ?? throw new DomainException("No organization context.");
        var input = command.Input;

        if (!Enum.TryParse<Channel>(input.Channel, ignoreCase: true, out var channel))
        {
            throw new DomainException("Channel must be one of: Sms, Email, WhatsApp.");
        }

        if (string.IsNullOrWhiteSpace(input.MessageBody) && input.TemplateId is null)
        {
            throw new DomainException("Either MessageBody or TemplateId must be supplied.");
        }

        if (input.Recipients.Count == 0)
        {
            throw new DomainException("A recurring campaign requires at least one recipient.");
        }

        CampaignSeries series;
        if (input.Id is { } id)
        {
            series = await _db.CampaignSeries.FirstOrDefaultAsync(s => s.Id == id, ct)
                ?? throw new NotFoundException(nameof(CampaignSeries), id);
            if (!string.IsNullOrEmpty(series.RecurringJobId))
            {
                _scheduler.Unschedule(series.RecurringJobId);
            }

            var existingRecipients = _db.CampaignSeriesRecipients.Where(r => r.SeriesId == series.Id);
            _db.CampaignSeriesRecipients.RemoveRange(existingRecipients);
        }
        else
        {
            series = new CampaignSeries
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = input.Name,
                Sender = input.Sender,
                CronExpression = input.CronExpression,
                CreatedByUserId = _tenant.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.CampaignSeries.Add(series);
        }

        series.Name = input.Name;
        series.Channel = channel;
        series.Sender = input.Sender;
        series.Subject = input.Subject;
        series.MessageBody = input.MessageBody;
        series.TemplateId = input.TemplateId;
        series.CallbackUrl = input.CallbackUrl;
        series.Priority = input.Priority;
        series.CronExpression = input.CronExpression;
        series.IsActive = input.IsActive;

        var uniqueRecipients = input.Recipients
            .GroupBy(r => r.Address.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());
        foreach (var recipient in uniqueRecipients)
        {
            _db.CampaignSeriesRecipients.Add(new CampaignSeriesRecipient
            {
                SeriesId = series.Id,
                OrganizationId = organizationId,
                Address = recipient.Address.Trim(),
                PersonalizationJson = recipient.Personalization is { Count: > 0 }
                    ? System.Text.Json.JsonSerializer.Serialize(recipient.Personalization)
                    : null
            });
        }

        if (series.IsActive)
        {
            series.RecurringJobId = _scheduler.Schedule(organizationId, series.Id, series.CronExpression);
            series.NextRunAtUtc = _scheduler.GetNextOccurrence(series.CronExpression, DateTime.UtcNow);
        }
        else
        {
            series.RecurringJobId = null;
            series.NextRunAtUtc = null;
        }

        await _db.SaveChangesAsync(ct);
        return series.Id;
    }
}

public sealed record SetCampaignSeriesActiveCommand(Guid SeriesId, bool IsActive) : IRequest;

public sealed class SetCampaignSeriesActiveHandler : IRequestHandler<SetCampaignSeriesActiveCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;
    private readonly ISeriesScheduler _scheduler;

    public SetCampaignSeriesActiveHandler(IAppDbContext db, ICurrentTenant tenant, ISeriesScheduler scheduler)
    {
        _db = db;
        _tenant = tenant;
        _scheduler = scheduler;
    }

    public async Task Handle(SetCampaignSeriesActiveCommand command, CancellationToken ct)
    {
        var series = await _db.CampaignSeries.FirstOrDefaultAsync(s => s.Id == command.SeriesId, ct)
            ?? throw new NotFoundException(nameof(CampaignSeries), command.SeriesId);

        if (command.IsActive == series.IsActive)
        {
            return;
        }

        if (command.IsActive)
        {
            series.RecurringJobId = _scheduler.Schedule(
                _tenant.OrganizationId!.Value, series.Id, series.CronExpression);
            series.NextRunAtUtc = _scheduler.GetNextOccurrence(series.CronExpression, DateTime.UtcNow);
        }
        else if (!string.IsNullOrEmpty(series.RecurringJobId))
        {
            _scheduler.Unschedule(series.RecurringJobId);
            series.RecurringJobId = null;
            series.NextRunAtUtc = null;
        }

        series.IsActive = command.IsActive;
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record DeleteCampaignSeriesCommand(Guid SeriesId) : IRequest;

public sealed class DeleteCampaignSeriesHandler : IRequestHandler<DeleteCampaignSeriesCommand>
{
    private readonly IAppDbContext _db;
    private readonly ISeriesScheduler _scheduler;

    public DeleteCampaignSeriesHandler(IAppDbContext db, ISeriesScheduler scheduler)
    {
        _db = db;
        _scheduler = scheduler;
    }

    public async Task Handle(DeleteCampaignSeriesCommand command, CancellationToken ct)
    {
        var series = await _db.CampaignSeries.FirstOrDefaultAsync(s => s.Id == command.SeriesId, ct)
            ?? throw new NotFoundException(nameof(CampaignSeries), command.SeriesId);
        if (!string.IsNullOrEmpty(series.RecurringJobId))
        {
            _scheduler.Unschedule(series.RecurringJobId);
        }

        _db.CampaignSeriesRecipients.RemoveRange(
            _db.CampaignSeriesRecipients.Where(r => r.SeriesId == series.Id));
        _db.CampaignSeries.Remove(series);
        await _db.SaveChangesAsync(ct);
    }
}
