using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.API.HealthChecks;

// Readiness check for the one dependency MicroLIMS genuinely cannot serve
// requests without: its PostgreSQL database.
//
// Two questions, in order:
//   1. Can we reach the database at all?
//   2. Is the schema the one this build of the code expects?
//
// An instance whose database is a migration behind will fail at the first
// query against a missing column, so it is NOT ready - reporting it ready
// would let a deployment roll forward onto a broken instance.
//
// Strictly read-only. It opens a connection and reads the migrations
// history table; it never applies a migration, writes a row, or touches
// application data. Health is an infrastructure probe, not a workflow.
public class DatabaseReadinessHealthCheck : IHealthCheck
{
    // Fixed, operator-facing strings. Deliberately generic: a health
    // endpoint may be reachable by a load balancer, so nothing here names
    // a host, a database, a credential, or a SQL statement. The framework
    // logs the underlying exception server-side for whoever needs detail.
    public const string UnreachableDescription = "PostgreSQL is not reachable.";
    public const string PendingMigrationsDescription = "PostgreSQL schema is behind the application.";
    public const string ReadyDescription = "PostgreSQL is reachable and the schema is current.";

    private readonly MicroLimsDbContext _db;

    public DatabaseReadinessHealthCheck(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Cheap: opens a connection and issues a trivial command. Not
            // a query against application tables.
            if (!await _db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy(UnreachableDescription);

            // Reads __EFMigrationsHistory and compares it with the
            // migrations compiled into this build. Read-only - the EF API
            // that would change anything is MigrateAsync, never called here.
            var pending = await _db.Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingCount = pending.Count();

            if (pendingCount > 0)
            {
                // The count is safe to report - it is a number, not a name,
                // and it tells an operator how far behind the database is.
                return HealthCheckResult.Unhealthy(
                    $"{PendingMigrationsDescription} ({pendingCount} migration(s) not applied.)");
            }

            return HealthCheckResult.Healthy(ReadyDescription);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The probe itself was cancelled - not evidence about the
            // database, so let the caller's cancellation propagate.
            throw;
        }
        catch (Exception ex)
        {
            // Reached only when the connection opens but interrogating it
            // fails - a refused connection makes CanConnectAsync return
            // false above rather than throw. Either way an unready
            // instance, never a crash: a readiness probe must not be able
            // to take the process down. The exception is attached for the
            // framework's own server-side logging and is never written to
            // the HTTP response - see HealthCheckExtensions.
            return HealthCheckResult.Unhealthy(UnreachableDescription, ex);
        }
    }
}
