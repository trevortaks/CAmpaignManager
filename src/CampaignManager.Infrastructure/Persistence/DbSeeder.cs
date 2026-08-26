using System.Security.Cryptography;
using System.Text;
using CampaignManager.Application.Abstractions;
using CampaignManager.Domain.Entities;
using CampaignManager.Domain.Enums;
using CampaignManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CampaignManager.Infrastructure.Persistence;

/// <summary>Development seed: demo organization, admin user, fake providers for all channels,
/// a template and a well-known API key. Idempotent — safe to run at every startup.</summary>
public static class DbSeeder
{
    public const string DemoOrgSlug = "demo";
    public const string AdminEmail = "admin@demo.local";
    public const string AdminPassword = "Admin!Passw0rd1";
    /// <summary>Development-only API key (plaintext). Never seed a known key in production.</summary>
    public const string DevApiKey = "cmk_dev_2f9c1a8e4b7d3f60";
    public const string DevWebhookSecret = "dev-webhook-secret";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Slug == DemoOrgSlug);
        if (org is null)
        {
            org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Demo Organization",
                Slug = DemoOrgSlug,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Organizations.Add(org);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded demo organization {OrgId}", org.Id);
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Admin", "Operator", "Viewer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() });
            }
        }

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByEmailAsync(AdminEmail);
        if (admin is null)
        {
            admin = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                OrganizationId = org.Id,
                DisplayName = "Demo Admin"
            };
            var result = await userManager.CreateAsync(admin, AdminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to seed admin user: " + string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            await userManager.AddToRoleAsync(admin, "Admin");
            logger.LogInformation("Seeded admin user {Email}", AdminEmail);
        }
        else if (admin.OrganizationId != org.Id)
        {
            admin.OrganizationId = org.Id;
            var result = await userManager.UpdateAsync(admin);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to repair demo admin organization: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
            logger.LogWarning("Reassigned demo admin to recreated organization {OrgId}", org.Id);
        }

        if (!await db.ProviderConfigurations.IgnoreQueryFilters().AnyAsync(p => p.OrganizationId == org.Id))
        {
            var protector = services.GetRequiredService<ICredentialProtector>();
            var fakeCreds = protector.Protect(new Dictionary<string, string>
            {
                ["apiKey"] = "fake-secret-key"
            });

            db.ProviderConfigurations.AddRange(
                NewProvider(org.Id, Channel.Sms, "fake-sms", "Fake SMS (dev)", fakeCreds),
                NewProvider(org.Id, Channel.Email, "fake-email", "Fake Email (dev)", fakeCreds),
                NewProvider(org.Id, Channel.WhatsApp, "fake-whatsapp", "Fake WhatsApp (dev)", fakeCreds));
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded fake providers for all channels");
        }

        if (!await db.MessageTemplates.IgnoreQueryFilters().AnyAsync(t => t.OrganizationId == org.Id))
        {
            db.MessageTemplates.Add(new MessageTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                Name = "Appointment Reminder",
                Channel = Channel.Sms,
                Body = "Hello {{FirstName}}, your appointment is on {{Date}}.",
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        if (!await db.ApiKeys.AnyAsync(k => k.OrganizationId == org.Id))
        {
            db.ApiKeys.Add(new ApiKey
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                Name = "Development key",
                KeyHash = HashKey(DevApiKey),
                KeyPrefix = DevApiKey[..12],
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded development API key (X-Api-Key: {Key})", DevApiKey);
        }
    }

    public static string HashKey(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static ProviderConfiguration NewProvider(
        Guid orgId, Channel channel, string key, string name, byte[] creds) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = orgId,
        Channel = channel,
        ProviderKey = key,
        Name = name,
        Priority = 10,
        IsEnabled = true,
        EncryptedCredentials = creds,
        SettingsJson = """{"failureRatePercent":"5"}""",
        WebhookSecret = DevWebhookSecret,
        CreatedAtUtc = DateTime.UtcNow
    };
}
