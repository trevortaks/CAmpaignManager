using System.Data;
using System.Text.Json;
using CampaignManager.Application.Abstractions;
using CampaignManager.Contracts.Campaigns;
using CampaignManager.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CampaignManager.Infrastructure.Persistence;

/// <summary>SqlBulkCopy-based writer for high-volume campaigns. Bypasses EF change tracking
/// entirely — used only above CreateCampaignHandler.BulkCopyThreshold, where per-row
/// AddRange/SaveChanges overhead becomes the bottleneck.</summary>
public sealed class SqlBulkRecipientWriter : IBulkRecipientWriter
{
    private readonly string _connectionString;

    public SqlBulkRecipientWriter(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
    }

    public async Task<IReadOnlyList<long>> BulkInsertRecipientsAsync(
        Guid organizationId, Guid campaignId, IReadOnlyList<CampaignRecipientDto> recipients,
        CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        var table = new DataTable();
        table.Columns.Add("OrganizationId", typeof(Guid));
        table.Columns.Add("CampaignId", typeof(Guid));
        table.Columns.Add("Address", typeof(string));
        table.Columns.Add("PersonalizationJson", typeof(string));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));

        foreach (var recipient in recipients)
        {
            table.Rows.Add(
                organizationId, campaignId, recipient.Address,
                recipient.Personalization is { Count: > 0 }
                    ? (object)JsonSerializer.Serialize(recipient.Personalization)
                    : DBNull.Value,
                utcNow);
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        using (var bulkCopy = new SqlBulkCopy(connection) { DestinationTableName = "CampaignRecipients", BatchSize = 5000 })
        {
            foreach (DataColumn column in table.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(table, ct);
        }

        // SqlBulkCopy does not return generated identities; the campaign's recipient rows are
        // written exactly once (at creation), so reading them back by CampaignId is safe.
        var ids = new List<long>(recipients.Count);
        await using var command = new SqlCommand(
            "SELECT Id FROM CampaignRecipients WHERE CampaignId = @campaignId ORDER BY Id", connection);
        command.Parameters.AddWithValue("@campaignId", campaignId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    public async Task BulkInsertMessagesAsync(
        Guid organizationId, Guid campaignId, Channel channel, IReadOnlyList<long> recipientIds,
        CancellationToken ct)
    {
        if (recipientIds.Count == 0) return;

        var utcNow = DateTime.UtcNow;
        var table = new DataTable();
        table.Columns.Add("PublicId", typeof(Guid));
        table.Columns.Add("OrganizationId", typeof(Guid));
        table.Columns.Add("CampaignId", typeof(Guid));
        table.Columns.Add("RecipientId", typeof(long));
        table.Columns.Add("Channel", typeof(byte));
        table.Columns.Add("Status", typeof(byte));
        table.Columns.Add("AttemptCount", typeof(byte));
        table.Columns.Add("QueuedAtUtc", typeof(DateTime));
        table.Columns.Add("UpdatedAtUtc", typeof(DateTime));

        foreach (var recipientId in recipientIds)
        {
            table.Rows.Add(
                Guid.NewGuid(), organizationId, campaignId, recipientId,
                (byte)channel, (byte)MessageStatus.Queued, (byte)0, utcNow, utcNow);
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        using var bulkCopy = new SqlBulkCopy(connection) { DestinationTableName = "Messages", BatchSize = 5000 };
        foreach (DataColumn column in table.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(table, ct);
    }
}
