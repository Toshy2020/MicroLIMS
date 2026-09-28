namespace MicroLIMS.Shared.Exceptions;

// Thrown when the record a request names (by route id) does not exist.
// ExceptionMiddleware turns it into 404.
//
// Derives from InvalidOperationException so the code that already catches
// that type - batch actions reporting a skipped item, services handling a
// lookup failure - keeps catching it.
//
// A missing record referenced from a request body (a department id on a
// new sample, a list of sampling points) stays an InvalidOperationException:
// that is a bad request, not a missing resource.
public class NotFoundException : InvalidOperationException
{
    public NotFoundException(string message) : base(message) { }
}
