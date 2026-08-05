using CampaignManager.Domain.Enums;

namespace CampaignManager.Domain.StateMachine;

public static class MessageStateMachine
{
    private static readonly IReadOnlyDictionary<MessageStatus, MessageStatus[]> Allowed =
        new Dictionary<MessageStatus, MessageStatus[]>
        {
            [MessageStatus.Queued] = [MessageStatus.Processing, MessageStatus.Expired],
            [MessageStatus.Processing] = [MessageStatus.Sent, MessageStatus.Failed, MessageStatus.Rejected],
            [MessageStatus.Sent] =
            [
                MessageStatus.Delivered, MessageStatus.Read, MessageStatus.Failed,
                MessageStatus.Expired, MessageStatus.Unknown
            ],
            [MessageStatus.Delivered] = [MessageStatus.Read],
            [MessageStatus.Unknown] = [MessageStatus.Delivered, MessageStatus.Read, MessageStatus.Failed],
            [MessageStatus.Read] = [],
            [MessageStatus.Failed] = [],
            [MessageStatus.Expired] = [],
            [MessageStatus.Rejected] = []
        };

    // Delivery-progress rank used to reject out-of-order webhook downgrades
    // (e.g. a late "Sent" callback after "Delivered" was recorded).
    private static readonly IReadOnlyDictionary<MessageStatus, int> Rank =
        new Dictionary<MessageStatus, int>
        {
            [MessageStatus.Queued] = 0,
            [MessageStatus.Processing] = 1,
            [MessageStatus.Unknown] = 2,
            [MessageStatus.Sent] = 3,
            [MessageStatus.Delivered] = 4,
            [MessageStatus.Read] = 5
        };

    public static bool CanTransition(MessageStatus from, MessageStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool IsTerminal(MessageStatus status) =>
        Allowed.TryGetValue(status, out var targets) && targets.Length == 0;

    /// <summary>Whether a provider webhook reporting <paramref name="reported"/> should update a
    /// message currently in <paramref name="current"/>. Progress statuses must move forward;
    /// Failed/Expired are accepted from any non-terminal state.</summary>
    public static bool ShouldApplyWebhookStatus(MessageStatus current, MessageStatus reported)
    {
        if (IsTerminal(current)) return false;
        if (reported is MessageStatus.Failed or MessageStatus.Expired) return true;
        if (!Rank.TryGetValue(reported, out var reportedRank) ||
            !Rank.TryGetValue(current, out var currentRank))
        {
            return false;
        }

        return reportedRank > currentRank;
    }
}
