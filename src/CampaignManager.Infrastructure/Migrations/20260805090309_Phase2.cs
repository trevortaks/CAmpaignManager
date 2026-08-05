using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampaignManager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastTestError",
                table: "ProviderConfigurations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LastTestSucceeded",
                table: "ProviderConfigurations",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTestedAtUtc",
                table: "ProviderConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "ProviderConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RateLimitPerMinute",
                table: "ProviderConfigurations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryDelaySeconds",
                table: "ProviderConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DailyStatistics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Channel = table.Column<byte>(type: "tinyint", nullable: false),
                    ProviderConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Queued = table.Column<int>(type: "int", nullable: false),
                    Sent = table.Column<int>(type: "int", nullable: false),
                    Delivered = table.Column<int>(type: "int", nullable: false),
                    Read = table.Column<int>(type: "int", nullable: false),
                    Failed = table.Column<int>(type: "int", nullable: false),
                    Rejected = table.Column<int>(type: "int", nullable: false),
                    Expired = table.Column<int>(type: "int", nullable: false),
                    AvgDeliverySeconds = table.Column<double>(type: "float", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeadLetters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReportedStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReplayCount = table.Column<int>(type: "int", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AbandonedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeadLetters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyStatistics_OrganizationId_Date_Channel_ProviderConfigurationId",
                table: "DailyStatistics",
                columns: new[] { "OrganizationId", "Date", "Channel", "ProviderConfigurationId" },
                unique: true,
                filter: "[ProviderConfigurationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeadLetters_ReceivedAtUtc",
                table: "WebhookDeadLetters",
                column: "ReceivedAtUtc",
                filter: "[ResolvedAtUtc] IS NULL AND [AbandonedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyStatistics");

            migrationBuilder.DropTable(
                name: "WebhookDeadLetters");

            migrationBuilder.DropColumn(
                name: "LastTestError",
                table: "ProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "LastTestSucceeded",
                table: "ProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "LastTestedAtUtc",
                table: "ProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "ProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "RateLimitPerMinute",
                table: "ProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "RetryDelaySeconds",
                table: "ProviderConfigurations");
        }
    }
}
