using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MicroLIMS.API.Middleware;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Operational logs must be tied to the same technical correlation id that
// ErrorLog, Incident and the browser's error report already carry, so one
// request can be reconstructed across all four afterwards.
public class OperationalLoggingTests
{
    // Captures the scopes a middleware opens, which is what carries the
    // correlation id onto every log line written during the request.
    private sealed class ScopeCapturingLogger<T> : ILogger<T>
    {
        public List<object?> Scopes { get; } = new();
        public Exception? ThrowOnBeginScope { get; set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            if (ThrowOnBeginScope is not null) throw ThrowOnBeginScope;
            Scopes.Add(state);
            return new NoopDisposable();
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }

        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
    }

    private static IReadOnlyDictionary<string, object> AsScopeDictionary(object? scope) =>
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(scope!);

    // ---- 1. The correlation id reaches the logging scope ----

    [Fact]
    public async Task InboundCorrelationId_IsCarriedIntoTheLoggingScope()
    {
        const string inbound = "abc123correlation";
        var logger = new ScopeCapturingLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = inbound;

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, logger);
        await middleware.InvokeAsync(context);

        var scope = AsScopeDictionary(Assert.Single(logger.Scopes));
        Assert.Equal(inbound, scope["CorrelationId"]);
    }

    // The same id that goes into the scope is the one echoed to the client
    // and stamped onto ErrorLog/Incident - that is what makes the four
    // systems line up.
    [Fact]
    public async Task GeneratedCorrelationId_MatchesTheResponseHeaderAndTheScope()
    {
        var logger = new ScopeCapturingLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();
        string? seenInsidePipeline = null;

        var middleware = new CorrelationIdMiddleware(
            ctx => { seenInsidePipeline = CorrelationIdMiddleware.GetCorrelationId(ctx); return Task.CompletedTask; },
            logger);
        await middleware.InvokeAsync(context);

        var header = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        var scope = AsScopeDictionary(Assert.Single(logger.Scopes));

        Assert.False(string.IsNullOrWhiteSpace(header));
        Assert.Equal(header, scope["CorrelationId"]);
        Assert.Equal(header, seenInsidePipeline);
    }

    // A caller-supplied header is still rejected when it is not a short
    // printable token, and the generated replacement is what gets logged.
    [Fact]
    public async Task HostileInboundHeader_IsNotCarriedIntoTheScope()
    {
        var hostile = new string('x', 500);
        var logger = new ScopeCapturingLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = hostile;

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);

        var scope = AsScopeDictionary(Assert.Single(logger.Scopes));
        Assert.NotEqual(hostile, scope["CorrelationId"]);
        Assert.Equal(32, ((string)scope["CorrelationId"]).Length);
    }

    // ---- 2. The scope carries an identifier and nothing else ----

    [Fact]
    public async Task LoggingScope_CarriesOnlyTheCorrelationId_NoRequestContent()
    {
        var logger = new ScopeCapturingLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers["Authorization"] = "Bearer super-secret-token";
        context.Request.Headers["Cookie"] = "session=secret";

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);

        var scope = AsScopeDictionary(Assert.Single(logger.Scopes));
        Assert.Equal(new[] { "CorrelationId" }, scope.Keys.ToArray());

        var serialised = string.Join("|", scope.Select(kv => $"{kv.Key}={kv.Value}"));
        foreach (var secret in new[] { "Bearer", "super-secret-token", "session=secret", "Authorization" })
            Assert.DoesNotContain(secret, serialised, StringComparison.OrdinalIgnoreCase);
    }

    // ---- 3. A logging fault must not silently swallow the request ----

    // If the logging infrastructure itself fails, the pipeline must not
    // continue as though the request succeeded - the failure surfaces and
    // is handled by the existing exception middleware rather than being
    // hidden. What must never happen is a request completing while its
    // logging was lost without trace.
    [Fact]
    public async Task LoggingFailure_DoesNotAllowTheRequestToCompleteSilently()
    {
        var logger = new ScopeCapturingLogger<CorrelationIdMiddleware>
        {
            ThrowOnBeginScope = new InvalidOperationException("log sink unavailable")
        };
        var context = new DefaultHttpContext();
        var pipelineRan = false;

        var middleware = new CorrelationIdMiddleware(
            _ => { pipelineRan = true; return Task.CompletedTask; }, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.False(pipelineRan);

        // The correlation id was still stamped on the response before the
        // failure, so the client can still quote it.
        Assert.False(string.IsNullOrWhiteSpace(
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()));
    }

    // ---- 22. Operational logging creates no GxP audit entries ----

    // Logging goes to ILogger, never to the database, so there is no path
    // by which an operational log line could reach AuditLog. This pins the
    // middleware's dependency surface: an HttpContext and a logger, no
    // DbContext.
    [Fact]
    public void CorrelationMiddleware_HasNoDatabaseDependency()
    {
        var parameters = typeof(CorrelationIdMiddleware)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToArray();

        Assert.Equal(new[] { "RequestDelegate", "ILogger`1" }, parameters);
    }
}
