using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using Npgsql;
using System.Text.Json;

namespace MicroLIMS.API.Middleware;

// Turns an EF Core / Npgsql failure into a severity and a bit of
// structured context. Lives in the API layer because it is the only
// project that references the Npgsql provider directly - the Application
// layer's capture service stays provider-agnostic.
public static class DatabaseErrorClassifier
{
    // True when a database failure is anywhere in the exception chain.
    // EF Core wraps transient connection failures in a plain
    // InvalidOperationException ("An exception has been raised that is
    // likely due to a transient failure.") with the NpgsqlException
    // underneath, so matching on the outermost type alone reports a
    // database outage as a business-rule warning.
    public static bool IsDatabaseFailure(Exception exception) =>
        Find<DbUpdateException>(exception) is not null || Find<NpgsqlException>(exception) is not null;

    public static ErrorSeverity Classify(Exception exception)
    {
        var postgres = Find<PostgresException>(exception);
        if (postgres is not null)
        {
            // SQLSTATE class 08 is "connection exception"; 57P03 is
            // cannot_connect_now (server starting up / shutting down).
            if (postgres.SqlState.StartsWith("08", StringComparison.Ordinal) || postgres.SqlState == "57P03")
                return ErrorSeverity.Critical;

            // Class 23 is integrity constraint violation; everything else
            // that got a reply from the server is an ordinary query fault.
            return ErrorSeverity.Error;
        }

        // An NpgsqlException that is not a PostgresException never got a
        // reply at all - socket, pool exhaustion or timeout. The database
        // is unreachable, which is the worst case for a lab mid-run.
        if (Find<NpgsqlException>(exception) is not null)
            return ErrorSeverity.Critical;

        // A DbUpdateException with no PostgreSQL cause underneath it -
        // concurrency, or a model/state problem.
        return ErrorSeverity.Error;
    }

    // Deliberately omits PostgresException.Detail: it echoes the offending
    // row's values, which would put sample/result data into a prunable
    // non-GxP table. Constraint and column names are enough to diagnose.
    public static string? BuildRawContext(Exception exception)
    {
        var postgres = Find<PostgresException>(exception);
        if (postgres is null) return null;

        var context = new Dictionary<string, string?>
        {
            ["sqlState"] = postgres.SqlState,
            ["tableName"] = postgres.TableName,
            ["constraintName"] = postgres.ConstraintName,
            ["columnName"] = postgres.ColumnName
        };

        return JsonSerializer.Serialize(context);
    }

    private static T? Find<T>(Exception exception) where T : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is T match) return match;
        }
        return null;
    }
}
