namespace MicroLIMS.Application.Services;

// A receipt lost a race twice (another report for the same injection was
// committed first). Nothing was saved and sending the same report again is
// safe; the API answers 503 with Retry-After. Deliberately not an
// InvalidOperationException, which ExceptionMiddleware reports as a 400.
public class IngestRetryableException : Exception
{
    public IngestRetryableException(string message) : base(message) { }
}
