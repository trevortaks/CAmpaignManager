using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using CampaignManager.Application.Providers;
using CampaignManager.Domain.Enums;

namespace CampaignManager.Infrastructure.Providers.Ses;

/// <summary>Amazon SES via the AWS SDK. Credentials: accessKeyId, secretAccessKey;
/// settings: region (e.g. "us-east-1").</summary>
public sealed class SesEmailProvider : IChannelProvider, ITestableProvider
{
    public Channel Channel => Channel.Email;
    public string ProviderKey => "ses";

    private static AmazonSimpleEmailServiceClient? CreateClient(ProviderCredentials credentials)
    {
        if (!credentials.Secrets.TryGetValue("accessKeyId", out var accessKeyId) ||
            !credentials.Secrets.TryGetValue("secretAccessKey", out var secretAccessKey))
        {
            return null;
        }

        var region = RegionEndpoint.GetBySystemName(
            credentials.Settings.GetValueOrDefault("region", "us-east-1"));
        return new AmazonSimpleEmailServiceClient(accessKeyId, secretAccessKey, region);
    }

    public async Task<SendResult> SendAsync(
        ProviderSendRequest request, ProviderCredentials credentials, CancellationToken ct)
    {
        using var client = CreateClient(credentials);
        if (client is null)
        {
            return SendResult.TransientFailure("missing_credentials", "SES accessKeyId/secretAccessKey not configured.");
        }

        try
        {
            var response = await client.SendEmailAsync(new SendEmailRequest
            {
                Source = request.Sender,
                Destination = new Destination { ToAddresses = [request.RecipientAddress] },
                Message = new Message
                {
                    Subject = new Content(request.Subject ?? "(no subject)"),
                    Body = new Body { Text = new Content(request.Body) }
                }
            }, ct);
            return SendResult.Ok(response.MessageId);
        }
        catch (MessageRejectedException ex)
        {
            return SendResult.Rejected("message_rejected", ex.Message);
        }
        catch (AccountSendingPausedException ex)
        {
            return SendResult.TransientFailure("account_paused", ex.Message);
        }
        catch (Amazon.SimpleEmail.AmazonSimpleEmailServiceException ex)
        {
            return SendResult.TransientFailure(ex.ErrorCode ?? "ses_error", ex.Message);
        }
    }

    /// <summary>Verifies credentials by fetching the account sending quota (no email sent).</summary>
    public async Task<SendResult> TestAsync(ProviderCredentials credentials, CancellationToken ct)
    {
        using var client = CreateClient(credentials);
        if (client is null)
        {
            return SendResult.TransientFailure("missing_credentials", "SES accessKeyId/secretAccessKey not configured.");
        }

        try
        {
            await client.GetSendQuotaAsync(new GetSendQuotaRequest(), ct);
            return SendResult.Ok("test-ok");
        }
        catch (Amazon.SimpleEmail.AmazonSimpleEmailServiceException ex)
        {
            return SendResult.TransientFailure(ex.ErrorCode ?? "ses_error", ex.Message);
        }
        catch (Amazon.Runtime.AmazonServiceException ex)
        {
            return SendResult.TransientFailure("aws_error", ex.Message);
        }
    }
}
