using CampaignManager.Domain.Enums;

namespace CampaignManager.Domain.StateMachine;

public static class CampaignStateMachine
{
    private static readonly IReadOnlyDictionary<CampaignStatus, CampaignStatus[]> Allowed =
        new Dictionary<CampaignStatus, CampaignStatus[]>
        {
            [CampaignStatus.Draft] = [CampaignStatus.Queued, CampaignStatus.Scheduled, CampaignStatus.Cancelled],
            [CampaignStatus.Scheduled] = [CampaignStatus.Queued, CampaignStatus.Cancelled],
            [CampaignStatus.Queued] = [CampaignStatus.Processing, CampaignStatus.Cancelled],
            [CampaignStatus.Processing] =
            [
                CampaignStatus.Completed, CampaignStatus.CompletedWithErrors,
                CampaignStatus.Failed, CampaignStatus.Cancelled
            ],
            [CampaignStatus.Completed] = [],
            [CampaignStatus.CompletedWithErrors] = [],
            [CampaignStatus.Failed] = [],
            [CampaignStatus.Cancelled] = []
        };

    public static bool CanTransition(CampaignStatus from, CampaignStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool IsTerminal(CampaignStatus status) =>
        Allowed.TryGetValue(status, out var targets) && targets.Length == 0;

    /// <summary>Final status from message outcomes: no failures → Completed,
    /// some → CompletedWithErrors, all → Failed.</summary>
    public static CampaignStatus Finalize(int totalMessages, int failedMessages)
    {
        if (failedMessages == 0) return CampaignStatus.Completed;
        return failedMessages >= totalMessages ? CampaignStatus.Failed : CampaignStatus.CompletedWithErrors;
    }
}
