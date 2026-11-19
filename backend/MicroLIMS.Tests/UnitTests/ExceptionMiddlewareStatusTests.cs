using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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

    private sealed class RecordingCapture : IErrorCaptureService
    {
        public List<ErrorCaptureRequest> Captured { get; } = [];
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default)
        {
            Captured.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger : ILogger<ExceptionMiddleware>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
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

    [Fact]
    public async Task UnexpectedException_Is500_AndDoesNotLeakItsMessage()
    {
        var (status, body) = await RunAsync(new NullReferenceException("secret internal detail"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain("secret internal detail", body);
    }

    [Fact]
    public async Task UnauthorizedAccess_Is403_WithItsMessage()
    {
        var (status, body) = await RunAsync(new UnauthorizedAccessException("Not your task."));

        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Contains("Not your task.", body);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientCancelledRequest_IsNotCapturedOrLoggedAsAnError(bool taskCanceled)
    {
        using var aborted = new CancellationTokenSource();
        var capture = new RecordingCapture();
        var logger = new RecordingLogger();
        Exception thrown = taskCanceled ? new TaskCanceledException() : new OperationCanceledException();
        var middleware = new ExceptionMiddleware(_ =>
        {
            aborted.Cancel();
            throw thrown;
        }, logger, capture);
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Empty(capture.Captured);
        Assert.DoesNotContain(logger.Levels, l => l >= LogLevel.Warning);
        Assert.Equal(ExceptionMiddleware.ClientClosedRequestStatusCode, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task CancellationWhileTheClientIsStillConnected_IsStillReported()
    {
        var capture = new RecordingCapture();
        var middleware = new ExceptionMiddleware(
            _ => throw new TaskCanceledException("upstream timeout"), NullLogger<ExceptionMiddleware>.Instance, capture);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Single(capture.Captured);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }
}
