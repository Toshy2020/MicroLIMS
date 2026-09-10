using System.Data;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.Fixtures;

// Shared PostgreSQL database for the [Collection("PostgresDatabaseCollection")]
// integration tests.
//
// Safety model (see PostgresTestConfiguration for the detail):
//   - No credential in source. The server comes from MICROLIMS_TEST_POSTGRES.
//   - The database NAME is never configurable - it is generated per run,
//     so no environment setting can aim the fixture at a real database.
//   - Every DROP passes EnsureSafeToDrop first.
//   - Schema is built by applying the application's own EF migrations.
//   - The generated database is dropped again on teardown, so runs do not
//     accumulate databases on the test server.
//
// When the server is not configured the fixture initialises to an
// unavailable state instead of throwing, and [PostgresFact] reports the
// tests as skipped.
public class PostgresTestFixture : IAsyncLifetime
{
    private readonly string? _adminConnectionString = PostgresTestConfiguration.AdminConnectionString;

    // Generated per fixture instance - one per test run, shared by every
    // class in the collection.
    private readonly string _testDatabaseName = PostgresTestConfiguration.NewTestDatabaseName();

    private string? _testConnectionString;

    public bool IsAvailable { get; private set; }

    public string TestDatabaseName => _testDatabaseName;

    public int SeededUserId { get; private set; }
    public int SeededControllerUserId { get; private set; }
    public int SeededDocTypeId { get; private set; }
    public int SeededDepartmentId { get; private set; }
    public int SeededSectionId { get; private set; }

    public MicroLimsDbContext CreateDbContext()
    {
        if (_testConnectionString is null)
            throw new InvalidOperationException(PostgresTestConfiguration.SkipReason);

        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseNpgsql(_testConnectionString)
            .Options;

        return new MicroLimsDbContext(options);
    }

    public IDatabaseSequenceHelper CreateSequenceHelper(MicroLimsDbContext db)
    {
        return new DatabaseSequenceHelper(db);
    }

    public async Task InitializeAsync()
    {
        // No configured server: stay unavailable rather than throw, so the
        // collection does not fail wholesale and [PostgresFact] can report
        // each test as skipped with a reason.
        if (_adminConnectionString is null)
        {
            IsAvailable = false;
            return;
        }

        // Validates the generated name before any DDL runs. The name is
        // freshly generated, so this can only fail if the generator itself
        // is changed to produce something unsafe - which is the point.
        _testConnectionString =
            PostgresTestConfiguration.BuildTestConnectionString(_adminConnectionString, _testDatabaseName);

        // 1. Create the isolated test database
        await using (var adminConn = new NpgsqlConnection(_adminConnectionString))
        {
            await adminConn.OpenAsync();

            // The name is per-run, so nothing should exist - but a DROP
            // IF EXISTS keeps the fixture idempotent if a previous run was
            // killed mid-flight. Guarded like every other destructive call.
            await DropDatabaseAsync(adminConn, _testDatabaseName);

            await using (var createCmd = adminConn.CreateCommand())
            {
                createCmd.CommandText = $"CREATE DATABASE \"{_testDatabaseName}\";";
                await createCmd.ExecuteNonQueryAsync();
            }
        }

        IsAvailable = true;

        // 2. Apply all EF Core migrations
        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();

            // 3. Seed baseline referenced records for FKs
            var adminRole = new Role
            {
                Type = RoleType.SystemAdministrator,
                Name = "System Administrator",
                IsSystemRole = true,
                IsActive = true
            };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();

            var user = new User
            {
                FullName = "QA Document Admin",
                Username = "qa_admin",
                PasswordHash = "hashed_dummy",
                RoleId = adminRole.Id,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            SeededUserId = user.Id;

            var controllerRole = new Role
            {
                Type = RoleType.SectionHead,
                Name = "Document Controller",
                IsSystemRole = true,
                IsActive = true
            };
            db.Roles.Add(controllerRole);
            await db.SaveChangesAsync();

            var controllerUser = new User
            {
                FullName = "Document Controller",
                Username = "doc_controller",
                PasswordHash = "hashed_dummy",
                RoleId = controllerRole.Id,
                IsActive = true
            };
            db.Users.Add(controllerUser);
            await db.SaveChangesAsync();
            SeededControllerUserId = controllerUser.Id;

            var docType = new DocumentType
            {
                Code = "SOP",
                Name = "Standard Operating Procedure",
                DefaultReviewCycleMonths = 24,
                IsActive = true
            };
            db.DocumentTypes.Add(docType);

            var dept = new DocumentDepartment
            {
                Code = "QC",
                Name = "Quality Control",
                IsActive = true
            };
            db.DocumentDepartments.Add(dept);
            await db.SaveChangesAsync();

            var section = new DocumentSection
            {
                DepartmentId = dept.Id,
                Name = "Microbiology Laboratory",
                IsActive = true
            };
            db.DocumentSections.Add(section);
            await db.SaveChangesAsync();

            SeededDocTypeId = docType.Id;
            SeededDepartmentId = dept.Id;
            SeededSectionId = section.Id;
        }
    }

    // Drops the generated database so test runs do not accumulate
    // databases on the server. Only ever touches the name this fixture
    // created, and only after EnsureSafeToDrop approves it.
    public async Task DisposeAsync()
    {
        if (_adminConnectionString is null || !IsAvailable) return;

        try
        {
            await using var adminConn = new NpgsqlConnection(_adminConnectionString);
            await adminConn.OpenAsync();
            await DropDatabaseAsync(adminConn, _testDatabaseName);
        }
        catch
        {
            // Teardown must never fail a green run. A leftover database is
            // uniquely named and harmless; the next run creates its own.
        }
    }

    // The only place the fixture issues a DROP. Every caller goes through
    // EnsureSafeToDrop first - there is deliberately no unguarded path.
    private static async Task DropDatabaseAsync(NpgsqlConnection adminConn, string databaseName)
    {
        PostgresTestConfiguration.EnsureSafeToDrop(databaseName, adminConn.ConnectionString);

        await using (var termCmd = adminConn.CreateCommand())
        {
            termCmd.CommandText =
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                "WHERE datname = @db AND pid <> pg_backend_pid();";
            termCmd.Parameters.AddWithValue("db", databaseName);
            await termCmd.ExecuteNonQueryAsync();
        }

        await using var dropCmd = adminConn.CreateCommand();
        dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\";";
        await dropCmd.ExecuteNonQueryAsync();
    }
}
