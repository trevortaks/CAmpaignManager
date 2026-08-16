using System.Net;

namespace CampaignManager.Sdk.Resilience;

/// <summary>Retries transient failures for idempotent requests. POST/PATCH retries require the
/// explicit <see cref="CampaignManagerClientOptions.RetryNonIdempotentRequests"/> opt-in.</summary>
internal sealed class RetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;
    private readonly bool _retryNonIdempotent;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    public RetryHandler(CampaignManagerClientOptions options)
        : this(options, null)
    {
    }

    public RetryHandler(CampaignManagerClientOptions options, HttpMessageHandler? innerHandler)
    {
        _maxRetries = options.MaxRetryAttempts;
        _retryNonIdempotent = options.RetryNonIdempotentRequests;
        _baseDelay = options.RetryBaseDelay;
        _maxDelay = options.MaxRetryDelay;
        if (innerHandler is not null) InnerHandler = innerHandler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_maxRetries == 0 || !CanRetry(request.Method))
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var bufferedContent = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        for (var attempt = 0; ; attempt++)
        {
            using var attemptRequest = Clone(request, bufferedContent);
            try
            {
                var response = await base.SendAsync(attemptRequest, cancellationToken).ConfigureAwait(false);
                if (attempt >= _maxRetries || !IsTransient(response.StatusCode))
                {
                    response.RequestMessage = request;
                    return response;
                }

                var delay = GetDelay(attempt + 1, response);
                response.Dispose();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                var delay = GetDelay(attempt + 1, null);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private bool CanRetry(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options ||
        method == HttpMethod.Delete || _retryNonIdempotent;

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.RequestTimeout || status == HttpStatusCode.TooManyRequests ||
        (int)status >= 500;

    private TimeSpan GetDelay(int attempt, HttpResponseMessage? response)
    {
        var retryAfter = response?.Headers.RetryAfter;
        var delay = retryAfter?.Delta ??
                    (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null) ??
                    TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
        if (delay < TimeSpan.Zero) return TimeSpan.Zero;
        return delay > _maxDelay ? _maxDelay : delay;
    }

    private static HttpRequestMessage Clone(HttpRequestMessage source, byte[]? content)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy
        };
        foreach (var header in source.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in source.Options) clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);

        if (content is not null)
        {
            clone.Content = new ByteArrayContent(content);
            foreach (var header in source.Content!.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
