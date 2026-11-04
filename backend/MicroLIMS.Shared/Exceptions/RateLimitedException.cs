namespace MicroLIMS.Shared.Exceptions;

// An administrative action that is deliberately limited in frequency (test
// sends, storage probe) was requested again too soon. Answered as 429.
public class RateLimitedException : Exception
{
    public int RetryAfterSeconds { get; }

    public RateLimitedException(string message, int retryAfterSeconds) : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
