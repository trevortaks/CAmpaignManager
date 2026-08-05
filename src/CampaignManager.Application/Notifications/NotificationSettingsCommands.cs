using CampaignManager.Application.Abstractions;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CampaignManager.Application.Notifications;

public sealed record NotificationSettingsDto(
    string? RecipientEmail, bool NotifyProviderOffline, bool NotifyCampaignCompleted,
    bool NotifyCampaignFailed, double HighFailureRateThreshold);

public sealed record GetNotificationSettingsQuery : IRequest<NotificationSettingsDto>;

public sealed class GetNotificationSettingsHandler
    : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public GetNotificationSettingsHandler(IAppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<NotificationSettingsDto> Handle(GetNotificationSettingsQuery query, CancellationToken ct)
    {
        var settings = await _db.NotificationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == _tenant.OrganizationId, ct);
        return settings is null
            ? new NotificationSettingsDto(null, true, false, true, 0.25)
            : new NotificationSettingsDto(
                settings.RecipientEmail, settings.NotifyProviderOffline, settings.NotifyCampaignCompleted,
                settings.NotifyCampaignFailed, settings.HighFailureRateThreshold);
    }
}

public sealed record SaveNotificationSettingsCommand(NotificationSettingsDto Input) : IRequest;

public sealed class SaveNotificationSettingsHandler : IRequestHandler<SaveNotificationSettingsCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public SaveNotificationSettingsHandler(IAppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task Handle(SaveNotificationSettingsCommand command, CancellationToken ct)
    {
        var organizationId = _tenant.OrganizationId ?? throw new DomainException("No organization context.");
        var settings = await _db.NotificationSettings.FirstOrDefaultAsync(
            s => s.OrganizationId == organizationId, ct);
        if (settings is null)
        {
            settings = new NotificationSettings { OrganizationId = organizationId };
            _db.NotificationSettings.Add(settings);
        }

        var input = command.Input;
        settings.RecipientEmail = string.IsNullOrWhiteSpace(input.RecipientEmail) ? null : input.RecipientEmail;
        settings.NotifyProviderOffline = input.NotifyProviderOffline;
        settings.NotifyCampaignCompleted = input.NotifyCampaignCompleted;
        settings.NotifyCampaignFailed = input.NotifyCampaignFailed;
        settings.HighFailureRateThreshold = Math.Clamp(input.HighFailureRateThreshold, 0.01, 1.0);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record ListNotificationLogQuery(int Page = 1, int PageSize = 50)
    : IRequest<IReadOnlyList<NotificationLog>>;

public sealed class ListNotificationLogHandler
    : IRequestHandler<ListNotificationLogQuery, IReadOnlyList<NotificationLog>>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public ListNotificationLogHandler(IAppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<NotificationLog>> Handle(ListNotificationLogQuery query, CancellationToken ct) =>
        await _db.NotificationLogs.AsNoTracking()
            .Where(n => n.OrganizationId == _tenant.OrganizationId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((Math.Max(1, query.Page) - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
}
