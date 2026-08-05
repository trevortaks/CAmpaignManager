namespace CampaignManager.Domain.Exceptions;

public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string entity, string from, string to)
        : base($"{entity} cannot transition from {from} to {to}.")
    {
    }
}
