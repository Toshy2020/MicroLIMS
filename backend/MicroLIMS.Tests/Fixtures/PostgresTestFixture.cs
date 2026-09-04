using System.Data;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.Fixtures;

public class PostgresTestFixture : IAsyncLifetime
{
    private const string BaseConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=0125598369@Mt;Include Error Detail=true";
    public const string TestDatabaseName = "microlims_doccontrol_wp1_test";
    public const string TestConnectionString = $"Host=localhost;Port=5432;Database={TestDatabaseName};Username=postgres;Password=0125598369@Mt;Include Error Detail=true";

    public int SeededUserId { get; private set; }
    public int SeededControllerUserId { get; private set; }
    public int SeededDocTypeId { get; private set; }
    public int SeededDepartmentId { get; private set; }
    public int SeededSectionId { get; private set; }

    public MicroLimsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        return new MicroLimsDbContext(options);
    }

    public IDatabaseSequenceHelper CreateSequenceHelper(MicroLimsDbContext db)
    {
        return new DatabaseSequenceHelper(db);
    }

    public async Task InitializeAsync()
    {
        // 1. Recreate clean test database
        await using (var adminConn = new NpgsqlConnection(BaseConnectionString))
        {
            await adminConn.OpenAsync();

            await using (var termCmd = adminConn.CreateCommand())
            {
                termCmd.CommandText = $@"
SELECT pg_terminate_backend(pid) 
FROM pg_stat_activity 
WHERE datname = '{TestDatabaseName}' AND pid <> pg_backend_pid();";
                await termCmd.ExecuteNonQueryAsync();
            }

            await using (var dropCmd = adminConn.CreateCommand())
            {
                dropCmd.CommandText = $"DROP DATABASE IF EXISTS {TestDatabaseName};";
                await dropCmd.ExecuteNonQueryAsync();
            }

            await using (var createCmd = adminConn.CreateCommand())
            {
                createCmd.CommandText = $"CREATE DATABASE {TestDatabaseName};";
                await createCmd.ExecuteNonQueryAsync();
            }
        }

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

    public async Task DisposeAsync()
    {
        // Cleanup test database connections
        try
        {
            await using var adminConn = new NpgsqlConnection(BaseConnectionString);
            await adminConn.OpenAsync();
            await using var termCmd = adminConn.CreateCommand();
            termCmd.CommandText = $@"
SELECT pg_terminate_backend(pid) 
FROM pg_stat_activity 
WHERE datname = '{TestDatabaseName}' AND pid <> pg_backend_pid();";
            await termCmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore teardown disconnect errors
        }
    }
}
