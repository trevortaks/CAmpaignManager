using CampaignManager.Application;
using CampaignManager.Application.Abstractions;
using CampaignManager.Infrastructure;
using CampaignManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();
    builder.Services
        .AddAuthentication(IdentityConstants.ApplicationScheme)
        .AddIdentityCookies();
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });
    builder.Services.AddAuthorization();

    builder.Services.AddControllersWithViews();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();

    // Same tenant resolution as the API: organization comes from the auth cookie's claim.
    app.Use(async (context, next) =>
    {
        var orgClaim = context.User.FindFirst(JwtTokenService.OrganizationClaim)?.Value;
        if (Guid.TryParse(orgClaim, out var organizationId))
        {
            context.RequestServices.GetRequiredService<ITenantSetter>().Set(organizationId);
        }

        await next();
    });

    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Campaigns}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CampaignManager.AdminUI terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
