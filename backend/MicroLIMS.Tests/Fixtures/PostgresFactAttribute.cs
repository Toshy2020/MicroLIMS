using Xunit;

namespace MicroLIMS.Tests.Fixtures;

// Marks a test as requiring the PostgreSQL test server. When
// MICROLIMS_TEST_POSTGRES is not set the test is reported as SKIPPED with
// the reason, never as passed - so a run without a database cannot be
// mistaken for a green integration suite.
//
// Using an attribute rather than letting the fixture throw keeps the
// distinction visible in the test report (passed / failed / skipped),
// which is what a CI gate needs to make a decision on.
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresTestConfiguration.IsConfigured)
            Skip = PostgresTestConfiguration.SkipReason;
    }
}

// Theory equivalent - same rule.
// Do NOT use with multiple data rows: CI's run-count check (verify_test_results.py)
// fails because xUnit 2 reports a skipped theory as ONE test, so the run without
// Postgres discovers fewer tests than the run with it. Prefer a [PostgresFact]
// that iterates the cases through PostgresCases.RunAsync.
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (!PostgresTestConfiguration.IsConfigured)
            Skip = PostgresTestConfiguration.SkipReason;
    }
}

// Runs every case in turn and names the failing case in the failure message.
public static class PostgresCases
{
    public static async Task RunAsync<T>(IEnumerable<T> cases, Func<T, Task> body)
    {
        foreach (var c in cases)
        {
            try { await body(c); }
            catch (Exception ex)
            {
                throw new Xunit.Sdk.XunitException($"Case [{c}] failed: {ex.GetType().Name}: {ex.Message}", ex);
            }
        }
    }
}
