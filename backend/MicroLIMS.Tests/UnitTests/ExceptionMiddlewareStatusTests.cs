using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The status each kind of service exception reaches the client with. A
// missing record used to come back as 400, the same as a rule violation.
public class ExceptionMiddlewareStatusTests
{
    private sealed class NoCapture : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static async Task<(int Status, string Body)> RunAsync(Exception thrown)
    {
        var middleware = new ExceptionMiddleware(_ => throw thrown, NullLogger<ExceptionMiddleware>.Instance, new NoCapture());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task NotFoundException_Is404_WithItsMessage()
    {
        var (status, body) = await RunAsync(new NotFoundException("Sample 42 not found."));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Contains("Sample 42 not found.", body);
    }

    [Fact]
    public async Task KeyNotFoundException_Is404()
    {
        var (status, _) = await RunAsync(new KeyNotFoundException("Document revision 7 not found."));

        Assert.Equal(StatusCodes.Status404NotFound, status);
    }

    [Fact]
    public async Task RuleViolation_StaysA400()
    {
        var (status, _) = await RunAsync(new InvalidOperationException("Insufficient stock."));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
    }
}
