using CampaignManager.Application.Abstractions;
using CampaignManager.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CampaignManager.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(
                Environment.GetEnvironmentVariable("CAMPAIGNMANAGER_CONNECTION")
                ?? "Server=localhost,1433;Database=CampaignManager;User Id=sa;Password=CampaignDev!Passw0rd;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options, new CurrentTenant());
    }
}
