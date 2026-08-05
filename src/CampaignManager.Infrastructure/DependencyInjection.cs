using CampaignManager.Application.Abstractions;
using CampaignManager.Application.Jobs;
using CampaignManager.Application.Providers;
using CampaignManager.Application.Templating;
using CampaignManager.Infrastructure.Identity;
using CampaignManager.Infrastructure.Jobs;
using CampaignManager.Infrastructure.Persistence;
using CampaignManager.Infrastructure.Providers.Fake;
using CampaignManager.Infrastructure.Providers.Meta;
using CampaignManager.Infrastructure.Providers.Registry;
using CampaignManager.Infrastructure.Providers.Smtp;
using CampaignManager.Infrastructure.Providers.Twilio;
using CampaignManager.Infrastructure.Security;
using CampaignManager.Infrastructure.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CampaignManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<Persistence.Interceptors.AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
            options
                .UseSqlServer(
                    configuration.GetConnectionString("Default"),
                    sql => sql.EnableRetryOnFailure(3))
                .AddInterceptors(sp.GetRequiredService<Persistence.Interceptors.AuditSaveChangesInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
        services.AddScoped<ITenantSetter>(sp => sp.GetRequiredService<CurrentTenant>());

        // Shared key ring so Api, Workers and AdminUI can all decrypt provider credentials.
        var keysPath = configuration["DataProtection:KeysPath"] ?? "../../dataprotection-keys";
        services.AddDataProtection()
            .SetApplicationName("CampaignManager")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        services.AddSingleton<ICredentialProtector, DataProtectionCredentialProtector>();

        // SignInManager's dependency graph needs these even in non-web hosts (Workers)
        // where DI validation runs; harmless in Api/AdminUI which configure real schemes.
        services.AddSingleton(TimeProvider.System);
        services.AddAuthentication();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        services.AddHttpClient();
        // Completion callbacks: no redirects (SSRF guard checks the original host only)
        // and a short timeout so slow endpoints can't stall worker slots.
        services.AddHttpClient("campaign-callbacks", client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IChannelProvider, FakeSmsProvider>();
        services.AddSingleton<IChannelProvider, FakeEmailProvider>();
        services.AddSingleton<IChannelProvider, FakeWhatsAppProvider>();
        services.AddSingleton<IChannelProvider, TwilioSmsProvider>();
        services.AddSingleton<IChannelProvider, SmtpEmailProvider>();
        services.AddSingleton<IChannelProvider, MetaWhatsAppProvider>();
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();
        services.AddScoped<IProviderSelector, ProviderSelector>();
        services.AddSingleton<IProviderThrottle, Providers.ProviderThrottle>();
        services.AddSingleton<FailoverSender>();
        services.AddSingleton<Security.IWebhookSignatureVerifier, Security.MetaWebhookSignatureVerifier>();
        services.AddSingleton<Security.IWebhookSignatureVerifier, Security.TwilioWebhookSignatureVerifier>();

        services.AddSingleton<ITemplateRenderer, PlaceholderTemplateRenderer>();
        services.AddScoped<ICampaignDispatcher, HangfireCampaignDispatcher>();
        services.AddScoped<ICampaignProcessingJob, CampaignProcessingJob>();
        services.AddScoped<IMaintenanceJobs, MaintenanceJobs>();

        return services;
    }
}
