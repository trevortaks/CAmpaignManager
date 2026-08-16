using CampaignManager.Sdk.Models.Common;

namespace CampaignManager.Sdk.Exceptions;

/// <summary>Thrown when the CampaignManager API returns a non-success status code. Wraps the
/// RFC 7807 problem body when one was returned (400/404/409/422/500 always carry one via the
/// server's GlobalExceptionHandler; 401/429 may not).</summary>
public sealed class CampaignManagerApiException : Exception
{
    public int StatusCode { get; }
    public string? ProblemTitle { get; }
    public string? ProblemDetail { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }
    public CampaignManagerProblemDetails? ProblemDetails { get; }

    public CampaignManagerApiException(int statusCode, CampaignManagerProblemDetails? problemDetails)
        : base(BuildMessage(statusCode, problemDetails))
    {
        StatusCode = statusCode;
        ProblemDetails = problemDetails;
        ProblemTitle = problemDetails?.Title;
        ProblemDetail = problemDetails?.Detail;
        if (problemDetails is not null && problemDetails.TryGetValidationErrors(out var errors))
        {
            ValidationErrors = errors;
        }
    }

    private static string BuildMessage(int statusCode, CampaignManagerProblemDetails? problemDetails)
    {
        var title = problemDetails?.Title ?? "Request failed";
        var message = $"{title} ({statusCode})";
        return problemDetails?.Detail is { Length: > 0 } detail ? $"{message}: {detail}" : message;
    }
}
