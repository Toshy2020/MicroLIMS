using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed on purpose: the HplcRunSamples check constraint and the
// unique index on the working standard lot code only exist in a real database.
[Collection("PostgresDatabaseCollection")]
public class WorkingStandardPostgresIntegrationTests
{
    private const string Password = "IntegrationPassword123!";

    private readonly PostgresTestFixture _fixture;

    public WorkingStandardPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task RunSample_BothOrNeitherSubject_RejectedByCheckConstraint()
    {
        await using var db = _fixture.CreateDbContext();

        // Both subject ids null. Postgres evaluates CHECK constraints before
        // the FK triggers, so no run row has to exist.
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"HplcRunSamples\" (\"HplcRunId\", \"TestOrderId\", \"WorkingStandardQualificationId\", \"Status\", \"AssignedAt\", \"AssignedByUserId\") " +
            "VALUES (0, NULL, NULL, 0, now(), 0)"));

        var pg = ex as PostgresException ?? ex.InnerException as PostgresException;
        Assert.NotNull(pg);
        Assert.Equal("CK_HplcRunSamples_OneSubject", pg!.ConstraintName);
    }

    [PostgresFact]
    public async Task Approve_TwoAtSameMoment_SecondGetsSignAgain()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var seed = await SeedAsync(suffix, 2);

        var interceptor = new ApproveOtherFirst(_fixture);
        await using var dbA = _fixture.CreateDbContext(interceptor);
        var serviceA = TestServiceFactory.WorkingStandard(dbA);
        interceptor.OtherId = seed.QualificationIds[1];

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => serviceA.ApproveAsync(seed.QualificationIds[0], new WorkingStandardSignRequest(Password, null), seed.UserId, null));
        Assert.Contains("Another approval took that working standard code at the same moment - sign again.", ex.Message);

        await using var check = _fixture.CreateDbContext();
        var lots = await check.Materials.AsNoTracking()
            .Where(m => m.MaterialType == MaterialType.WorkingStandard && m.MaterialMasterEntryId == seed.EntryId)
            .ToListAsync();
        var lab = LabClock.Default.ToLabLocal(DateTime.UtcNow);
        var lot = Assert.Single(lots);
        Assert.StartsWith("WS-", lot.Code);
        Assert.EndsWith($"/{lab:MM}/{lab:yyyy}", lot.Code);

        var statuses = await check.WorkingStandardQualifications.AsNoTracking()
            .Where(q => seed.QualificationIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, q => q.Status);
        Assert.Equal(WorkingStandardQualificationStatus.Reviewed, statuses[seed.QualificationIds[0]]);
        Assert.Equal(WorkingStandardQualificationStatus.Approved, statuses[seed.QualificationIds[1]]);
    }

    [PostgresFact]
    public async Task ApprovedLot_UsableInSolutionPreparationAndSstPicker()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var seed = await SeedAsync(suffix, 1);

        await using var db = _fixture.CreateDbContext();
        await TestServiceFactory.WorkingStandard(db)
            .ApproveAsync(seed.QualificationIds[0], new WorkingStandardSignRequest(Password, null), seed.UserId, null);

        await using var read = _fixture.CreateDbContext();
        var lot = await read.Materials.AsNoTracking()
            .SingleAsync(m => m.MaterialType == MaterialType.WorkingStandard && m.MaterialMasterEntryId == seed.EntryId);

        var picker = await TestServiceFactory.Material(read).GetUsableReferenceStandardsAsync(seed.UserId);
        Assert.Contains(picker, m => m.Id == lot.Id);

        var today = LabClock.Default.LabToday;
        Assert.True(LotUsability.Check(lot, seed.EntryId, 1m, today).Usable);
    }

    private sealed record Seed(int UserId, int EntryId, List<int> QualificationIds);

    private async Task<Seed> SeedAsync(string suffix, int count)
    {
        await using var db = _fixture.CreateDbContext();

        var approver = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        approver.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
        approver.IsActive = true;

        var role = await db.Roles.FirstAsync(r => r.Type == RoleType.Analyst);
        var preparer = new User { FullName = $"WS Preparer {suffix}", Username = $"wsp_{suffix}", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        var reviewer = new User { FullName = $"WS Reviewer {suffix}", Username = $"wsr_{suffix}", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        db.Users.AddRange(preparer, reviewer);

        var entry = new MaterialMasterEntry
        {
            SectionId = _fixture.SeededSectionId, Code = $"WSE-{suffix}", Name = $"WS Standard {suffix}",
            Category = MaterialMasterCategory.ReferenceStandard, BaseUnit = MaterialUnit.Gram, IsActive = true,
            CreatedByUserId = _fixture.SeededUserId, LastModifiedByUserId = _fixture.SeededUserId,
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();

        var qualifications = Enumerable.Range(1, count).Select(i => new WorkingStandardQualification
        {
            SectionId = _fixture.SeededSectionId, Code = $"WSQ-{suffix}-{i}",
            Kind = WorkingStandardQualificationKind.Initial, Status = WorkingStandardQualificationStatus.Reviewed,
            MaterialMasterEntryId = entry.Id, SourceMaterialName = entry.Name, SourceBatchNumber = $"SRC-{suffix}-{i}",
            QuantityGrams = 5m, Location = "Shelf 1", MeanAssayPercent = 99.5m, RsdPercent = 0.4m, PotencyPercent = 99.5m, Passed = true,
            CreatedByUserId = preparer.Id, CreatedAt = DateTime.UtcNow,
            PreparedByUserId = preparer.Id, PreparedAt = DateTime.UtcNow,
            ReviewedByUserId = reviewer.Id, ReviewedAt = DateTime.UtcNow,
        }).ToList();
        db.WorkingStandardQualifications.AddRange(qualifications);
        await db.SaveChangesAsync();

        return new Seed(approver.Id, entry.Id, qualifications.Select(q => q.Id).ToList());
    }

    // Stands in for the other approver: the instant this approval's save is
    // about to insert the new lot, it approves the other qualification on a
    // brand-new DbContext, taking the same next code.
    private sealed class ApproveOtherFirst : SaveChangesInterceptor
    {
        private readonly PostgresTestFixture _fixture;
        private bool _fired;

        public int OtherId { get; set; }

        public ApproveOtherFirst(PostgresTestFixture fixture) => _fixture = fixture;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var pending = eventData.Context!.ChangeTracker.Entries<Material>()
                .Any(e => e.State == EntityState.Added && e.Entity.MaterialType == MaterialType.WorkingStandard);

            if (pending && !_fired && OtherId != 0)
            {
                _fired = true;
                await using var otherDb = _fixture.CreateDbContext();
                await TestServiceFactory.WorkingStandard(otherDb).ApproveAsync(
                    OtherId, new WorkingStandardSignRequest(Password, null), _fixture.SeededUserId, null, cancellationToken);
            }

            return result;
        }
    }
}
