using CampaignManager.Domain.Enums;
using FluentValidation;

namespace CampaignManager.Application.Campaigns.Commands.CreateCampaign;

public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public const int MaxRecipients = 100_000;

    public CreateCampaignValidator()
    {
        RuleFor(c => c.Request.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Request.Sender).NotEmpty().MaximumLength(320);
        RuleFor(c => c.Request.Channel)
            .Must(channel => Enum.TryParse<Channel>(channel, ignoreCase: true, out _))
            .WithMessage("Channel must be one of: Sms, Email, WhatsApp.");
        RuleFor(c => c.Request)
            .Must(r => !string.IsNullOrWhiteSpace(r.MessageBody) || r.TemplateId.HasValue)
            .WithMessage("Either MessageBody or TemplateId must be supplied.")
            .OverridePropertyName("MessageBody");
        RuleFor(c => c.Request.Subject)
            .NotEmpty()
            .When(c => string.Equals(c.Request.Channel, "Email", StringComparison.OrdinalIgnoreCase)
                       && !c.Request.TemplateId.HasValue)
            .WithMessage("Subject is required for email campaigns without a template.");
        RuleFor(c => c.Request.Recipients)
            .NotEmpty()
            .Must(r => r.Count <= MaxRecipients)
            .WithMessage($"A campaign may not exceed {MaxRecipients:N0} recipients.");
        RuleForEach(c => c.Request.Recipients)
            .Must(r => !string.IsNullOrWhiteSpace(r.Address) && r.Address.Length <= 320)
            .WithMessage("Each recipient must have an address of at most 320 characters.");
        RuleFor(c => c.Request.ScheduledAtUtc)
            .Must(scheduled => scheduled is null || scheduled > DateTime.UtcNow.AddMinutes(-1))
            .WithMessage("ScheduledAtUtc must be in the future.");
        RuleFor(c => c.Request.CallbackUrl)
            .Must(url => url is null || Uri.TryCreate(url, UriKind.Absolute, out var u)
                && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            .WithMessage("CallbackUrl must be an absolute http(s) URL.");
    }
}
