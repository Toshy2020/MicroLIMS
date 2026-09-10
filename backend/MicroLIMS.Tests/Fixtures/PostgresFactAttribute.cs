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
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (!PostgresTestConfiguration.IsConfigured)
            Skip = PostgresTestConfiguration.SkipReason;
    }
}
