using Microsoft.Extensions.Logging.Console;

namespace MicroLIMS.API.Extensions;

// Operational logging configuration.
//
// The deliberate choice here is what is NOT done: no rolling file sink.
// On the current backend host the application filesystem lives inside the
// container, so a log file would vanish with the process that produced it -
// precisely when the log is most wanted. Writing one anyway would produce
// something that looks like a durable log store and is not.
//
// So logs go to stdout as structured JSON, which the hosting platform
// captures and retains outside the container. That is the only sink
// available to this deployment that survives the process, and making it
// machine-readable is what turns it from console noise into something an
// operator can filter by CorrelationId, level or request path.
//
// This is diagnostics only. Regulated evidence goes to AuditLog, security
// evidence to SecurityAuditEvent, and structured incidents to
// ErrorLog/Incident - none of which are replaced by anything here.
public static class LoggingExtensions
{
    public static void AddMicroLimsLogging(this WebApplicationBuilder builder)
    {
        // Providers are cleared first so the configuration below is the
        // whole story - otherwise the host's default console provider
        // stays attached and every line is emitted twice.
        builder.Logging.ClearProviders();

        if (builder.Environment.IsDevelopment())
        {
            // A human reading a terminal wants to read it. Scopes are on
            // so the correlation id still appears next to each line.
            builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
                options.UseUtcTimestamp = true;
            });
            return;
        }

        // Everywhere else the reader is a log viewer or a grep, so emit
        // one JSON object per line: timestamp, level, category, message,
        // the structured properties the call site supplied, and the
        // correlation-id scope.
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
            options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions
            {
                // One line per entry - a pretty-printed object breaks
                // line-oriented log collectors.
                Indented = false
            };
        });
    }
}
