using Cronos;
using FluentValidation;

namespace CampaignManager.Application.Campaigns.Series;

public sealed class SaveCampaignSeriesValidator : AbstractValidator<SaveCampaignSeriesCommand>
{
    public SaveCampaignSeriesValidator()
    {
        RuleFor(c => c.Input.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Input.Sender).NotEmpty().MaximumLength(320);
        RuleFor(c => c.Input.CronExpression)
            .NotEmpty()
            .Must(BeAValidCronExpression)
            .WithMessage("CronExpression must be a valid standard 5-field cron expression (UTC).");
        RuleFor(c => c.Input.Recipients).NotEmpty();
        RuleForEach(c => c.Input.Recipients)
            .Must(r => !string.IsNullOrWhiteSpace(r.Address) && r.Address.Length <= 320)
            .WithMessage("Each recipient must have an address of at most 320 characters.");
    }

    private static bool BeAValidCronExpression(string expression)
    {
        try
        {
            CronExpression.Parse(expression);
            return true;
        }
        catch (CronFormatException)
        {
            return false;
        }
    }
}
