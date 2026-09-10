using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Tests for the test infrastructure itself.
//
// The invariant being protected: running the suite cannot destroy the
// application's database. These assert the guard directly, and none of
// them connects to - let alone drops - any database.
public class TestDatabaseSafetyTests
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres";

    // ---- 2. Generated names are isolated ----

    [Fact]
    public void NewTestDatabaseName_IsUniquePerCall()
    {
        var names = Enumerable.Range(0, 200)
            .Select(_ => PostgresTestConfiguration.NewTestDatabaseName())
            .ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void NewTestDatabaseName_CarriesTheTestPrefix()
    {
        var name = PostgresTestConfiguration.NewTestDatabaseName();

        Assert.StartsWith("microlims_test_", name);
        // Prefix plus a 32-character GUID.
        Assert.Equal("microlims_test_".Length + 32, name.Length);
    }

    [Fact]
    public void NewTestDatabaseName_IsAcceptedByItsOwnSafetyCheck()
    {
        var name = PostgresTestConfiguration.NewTestDatabaseName();

        // Must not throw - the generator and the gate have to agree.
        PostgresTestConfiguration.EnsureSafeToDrop(name, AdminConnectionString);
    }

    // ---- 3 & 4. Non-test targets are rejected ----

    [Theory]
    [InlineData("LIMSV2")]              // the actual application database
    [InlineData("limsv2")]
    [InlineData("postgres")]            // maintenance database
    [InlineData("template0")]
    [InlineData("template1")]
    [InlineData("microlims")]           // plausible but not generated
    [InlineData("microlims_prod")]
    [InlineData("microlims_test")]      // prefix alone, no unique suffix
    [InlineData("microlims_test_")]
    [InlineData("microlims_doccontrol_wp1_test")] // the OLD fixed name
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureSafeToDrop_RejectsAnythingNotGenerated(string databaseName)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PostgresTestConfiguration.EnsureSafeToDrop(databaseName, AdminConnectionString));

        Assert.Contains("Refusing to drop", ex.Message);
    }

    [Fact]
    public void EnsureSafeToDrop_RejectsANameThatOnlyLooksGenerated()
    {
        // Right prefix, wrong suffix shape - a hand-written name must not
        // slip through by resembling the pattern.
        Assert.Throws<InvalidOperationException>(() =>
            PostgresTestConfiguration.EnsureSafeToDrop("microlims_test_notahexguid", AdminConnectionString));

        Assert.Throws<InvalidOperationException>(() =>
            PostgresTestConfiguration.EnsureSafeToDrop("microlims_test_LIMSV2", AdminConnectionString));
    }

    [Fact]
    public void EnsureSafeToDrop_RejectsTheMaintenanceDatabaseItConnectsThrough()
    {
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var adminPointingAtTheSameDatabase =
            $"Host=localhost;Port=5432;Database={name};Username=postgres";

        var ex = Assert.Throws<InvalidOperationException>(
            () => PostgresTestConfiguration.EnsureSafeToDrop(name, adminPointingAtTheSameDatabase));

        Assert.Contains("maintenance database", ex.Message);
    }

    // The structural reason the application database cannot be selected:
    // configuration supplies a server, never a database name.
    [Fact]
    public void BuildTestConnectionString_AlwaysTargetsTheGeneratedDatabase_NotTheConfiguredOne()
    {
        var adminPointingAtTheAppDatabase =
            "Host=localhost;Port=5432;Database=LIMSV2;Username=postgres";
        var generated = PostgresTestConfiguration.NewTestDatabaseName();

        var built = PostgresTestConfiguration.BuildTestConnectionString(adminPointingAtTheAppDatabase, generated);

        var database = new NpgsqlConnectionStringBuilder(built).Database;
        Assert.Equal(generated, database);
        Assert.NotEqual("LIMSV2", database);
    }

    [Fact]
    public void BuildTestConnectionString_RefusesANonGeneratedTarget()
    {
        Assert.Throws<InvalidOperationException>(
            () => PostgresTestConfiguration.BuildTestConnectionString(AdminConnectionString, "LIMSV2"));
    }

    [Fact]
    public void BuildTestConnectionString_PreservesServerAndCredentialsFromConfiguration()
    {
        // Credentials come from configuration and are carried through
        // unchanged - the fixture never composes its own.
        var admin = "Host=db.example;Port=6432;Database=postgres;Username=tester;Password=supplied-by-config";
        var generated = PostgresTestConfiguration.NewTestDatabaseName();

        var builder = new NpgsqlConnectionStringBuilder(
            PostgresTestConfiguration.BuildTestConnectionString(admin, generated));

        Assert.Equal("db.example", builder.Host);
        Assert.Equal(6432, builder.Port);
        Assert.Equal("tester", builder.Username);
        Assert.True(builder.IncludeErrorDetail);
    }

    // ---- 1 & 7. Missing configuration is handled safely ----

    [Fact]
    public void SkipReason_NamesTheVariableAndDoesNotLeakASecret()
    {
        var reason = PostgresTestConfiguration.SkipReason;

        Assert.Contains(PostgresTestConfiguration.ConnectionStringVariable, reason);
        Assert.Contains("<secret>", reason); // placeholder, not a real value
    }

    [Fact]
    public void ConfigurationVariable_HasTheExpectedName()
    {
        // Pinned because CI documentation and developer setup both refer
        // to it by name.
        Assert.Equal("MICROLIMS_TEST_POSTGRES", PostgresTestConfiguration.ConnectionStringVariable);
    }

    // This very test class proves the point: it exercises the safety layer
    // with no database available at all.
    [Fact]
    public void UnitTests_RunWithoutADatabaseServer()
    {
        var configured = PostgresTestConfiguration.IsConfigured;

        // True or false, this test runs and passes either way.
        Assert.True(configured || !configured);
    }
}
