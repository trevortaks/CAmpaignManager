using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Campaigns.Commands.CreateCampaign;
using CampaignManager.Application.Jobs;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Infrastructure.Jobs;

/// <summary>Materializes one occurrence of a CampaignSeries by reusing the normal
/// CreateCampaign pipeline (validation, dedup, bulk insert, dispatch) — no logic duplicated.</summary>
public sealed class CampaignSeriesJob : ICampaignSeriesJob
{
    private readonly IAppDbContext _db;
    private readonly ITenantSetter _tenantSetter;
    private readonly ISender _sender;
    private readonly ISeriesScheduler _scheduler;
    private readonly ILogger<CampaignSeriesJob> _logger;

    public CampaignSeriesJob(
        IAppDbContext db, ITenantSetter tenantSetter, ISender sender,
        ISeriesScheduler scheduler, ILogger<CampaignSeriesJob> logger)
    {
        _db = db;
        _tenantSetter = tenantSetter;
        _sender = sender;
        _scheduler = scheduler;
        _logger = logger;
    }

    public async Task RunAsync(Guid organizationId, Guid seriesId)
    {
        _tenantSetter.Set(organizationId);
        var series = await _db.CampaignSeries.FirstOrDefaultAsync(s => s.Id == seriesId);
        if (series is null || !series.IsActive)
        {
            return;
        }

        var seriesRecipients = await _db.CampaignSeriesRecipients
            .Where(r => r.SeriesId == seriesId)
            .ToListAsync();
        var recipients = seriesRecipients
            .Select(r => new CampaignRecipientDto
            {
                Address = r.Address,
                Personalization = r.PersonalizationJson == null
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(r.PersonalizationJson)
            })
            .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogWarning("CampaignSeries {SeriesId} has no recipients; skipping occurrence", seriesId);
        }
        else
        {
            var response = await _sender.Send(new CreateCampaignCommand(
                new CreateCampaignRequest
                {
                    Name = $"{series.Name} — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                    Channel = series.Channel.ToString(),
                    Sender = series.Sender,
                    Subject = series.Subject,
                    MessageBody = series.MessageBody,
                    TemplateId = series.TemplateId,
                    CallbackUrl = series.CallbackUrl,
                    Priority = series.Priority,
                    Recipients = recipients
                }, seriesId));

            _logger.LogInformation("CampaignSeries {SeriesId} materialized campaign {CampaignId} ({TrackingId})",
                seriesId, response.CampaignId, response.TrackingId);
        }

        series.LastRunAtUtc = DateTime.UtcNow;
        series.NextRunAtUtc = _scheduler.GetNextOccurrence(series.CronExpression, DateTime.UtcNow);
        await _db.SaveChangesAsync();
    }
}
