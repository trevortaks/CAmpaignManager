using System.Net;
using System.Net.Mail;
using CampaignManager.Application.Notifications;
using Microsoft.Extensions.Configuration;

namespace CampaignManager.Infrastructure.Notifications;

/// <summary>Sends admin notifications via SMTP, configured under "Notifications:Smtp".
/// When no host is configured, throws so NotificationService records the attempt as
/// unsent (with the reason) in the NotificationLog rather than silently dropping it.</summary>
public sealed class SmtpNotificationSink : INotificationSink
{
    private readonly IConfiguration _configuration;

    public SmtpNotificationSink(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string recipientEmail, string subject, string body, CancellationToken ct)
    {
        var section = _configuration.GetSection("Notifications:Smtp");
        var host = section["Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("Notifications:Smtp:Host is not configured.");
        }

        var port = int.TryParse(section["Port"], out var p) ? p : 587;
        var from = section["From"] ?? "campaignmanager@localhost";
        var enableSsl = !string.Equals(section["EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);

        using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
        var username = section["Username"];
        var password = section["Password"];
        if (!string.IsNullOrEmpty(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var mail = new MailMessage(from, recipientEmail, subject, body);
        await client.SendMailAsync(mail, ct);
    }
}
