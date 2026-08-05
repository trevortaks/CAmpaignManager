using System.Net;
using System.Net.Mail;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Smtp;

/// <summary>Plain SMTP email. Credentials: username, password; settings: host, port, enableSsl.</summary>
public sealed class SmtpEmailProvider : IChannelProvider
{
    public Channel Channel => Channel.Email;
    public string ProviderKey => "smtp";

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        var host = credentials.Settings.GetValueOrDefault("host");
        if (string.IsNullOrEmpty(host))
        {
            return SendResult.TransientFailure("missing_settings", "SMTP host not configured.");
        }

        var port = int.TryParse(credentials.Settings.GetValueOrDefault("port"), out var p) ? p : 587;
        var enableSsl = !string.Equals(credentials.Settings.GetValueOrDefault("enableSsl"), "false",
            StringComparison.OrdinalIgnoreCase);

        try
        {
            using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
            if (credentials.Secrets.TryGetValue("username", out var username) &&
                credentials.Secrets.TryGetValue("password", out var password))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            using var mail = new MailMessage(request.Sender, request.RecipientAddress)
            {
                Subject = request.Subject ?? string.Empty,
                Body = request.Body
            };
            await client.SendMailAsync(mail, ct);
            // SMTP has no provider message id; synthesize one for tracking.
            return SendResult.Ok($"smtp-{Guid.NewGuid():N}");
        }
        catch (SmtpFailedRecipientException ex)
        {
            return SendResult.Rejected("recipient_rejected", ex.Message);
        }
        catch (SmtpException ex)
        {
            return SendResult.TransientFailure(ex.StatusCode.ToString(), ex.Message);
        }
        catch (FormatException ex)
        {
            return SendResult.Rejected("invalid_address", ex.Message);
        }
    }
}
