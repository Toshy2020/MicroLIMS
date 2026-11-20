using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ConfirmatoryPlateNullableWorkflowStepPostgresTests(PostgresTestFixture fixture)
{
    private static readonly DateTime RecordedAt = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task LocationLinkedPlate_PersistsWithNullWorkflowStepAndAttributedAudit()
    {
        var (materialId, first, _, _) = await SeedAsync();
        var plate = Plate(materialId, first);
        await using (var actDb = fixture.CreateDbContext())
        {
            actDb.CurrentUserId = fixture.SeededUserId;
            actDb.ConfirmatoryPlateObservations.Add(plate);
            await actDb.SaveChangesAsync();
        }

        await using var assertDb = fixture.CreateDbContext();
        var stored = await assertDb.ConfirmatoryPlateObservations.AsNoTracking().SingleAsync(p => p.Id == plate.Id);
        Assert.Null(stored.WorkflowStepResultId);
        Assert.Equal(first.Id, stored.LocationPathogenObservationId);
        Assert.Equal(first.SampleLocationId, stored.SampleLocationId);
        Assert.Equal(materialId, stored.MaterialId);
        Assert.Equal(plate.MediumIndex, stored.MediumIndex);
        Assert.Equal(plate.Observation, stored.Observation);
        Assert.Equal(plate.ExpectedAppearanceSnapshot, stored.ExpectedAppearanceSnapshot);
        Assert.Equal(fixture.SeededUserId, stored.RecordedByUserId);
        Assert.Equal(RecordedAt, stored.RecordedAtUtc);

        var audit = await assertDb.AuditLogs.AsNoTracking().SingleAsync(
            a => a.EntityName == nameof(ConfirmatoryPlateObservation) && a.EntityId == plate.Id.ToString());
        Assert.Equal(fixture.SeededUserId, audit.UserId);
        Assert.Equal("Create", audit.Action);
        Assert.Null(audit.PreviousValue);
        Assert.NotNull(audit.NewValue);
        using var values = JsonDocument.Parse(audit.NewValue);
        Assert.Equal(first.Id, values.RootElement.GetProperty(nameof(plate.LocationPathogenObservationId)).GetInt32());
        Assert.Equal(fixture.SeededUserId, values.RootElement.GetProperty(nameof(plate.RecordedByUserId)).GetInt32());
    }

    [PostgresFact]
    public async Task TwoLocations_CanUseTheSameMaterialWithNullWorkflowSteps()
    {
        var (materialId, first, second, _) = await SeedAsync();
        await using (var actDb = fixture.CreateDbContext())
        {
            actDb.CurrentUserId = fixture.SeededUserId;
            actDb.ConfirmatoryPlateObservations.AddRange(Plate(materialId, first), Plate(materialId, second));
            await actDb.SaveChangesAsync();
        }

        await using var assertDb = fixture.CreateDbContext();
        var plates = await assertDb.ConfirmatoryPlateObservations.AsNoTracking()
            .Where(p => p.MaterialId == materialId).OrderBy(p => p.LocationPathogenObservationId).ToListAsync();
        Assert.Equal(new int?[] { first.Id, second.Id }.Order(), plates.Select(p => p.LocationPathogenObservationId));
        Assert.All(plates, p => Assert.Null(p.WorkflowStepResultId));
    }

    [PostgresFact]
    public async Task DuplicateNonNullWorkflowStepAndMaterial_IsRejected()
    {
        var (materialId, _, _, stepId) = await SeedAsync();
        await using (var arrangeDb = fixture.CreateDbContext())
        {
            arrangeDb.CurrentUserId = fixture.SeededUserId;
            arrangeDb.ConfirmatoryPlateObservations.Add(Plate(materialId, stepId: stepId));
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = fixture.CreateDbContext())
        {
            actDb.CurrentUserId = fixture.SeededUserId;
            actDb.ConfirmatoryPlateObservations.Add(Plate(materialId, stepId: stepId));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => actDb.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal(WorkflowStepIndex, postgres.ConstraintName);
        }

        await using var assertDb = fixture.CreateDbContext();
        var stored = await assertDb.ConfirmatoryPlateObservations.AsNoTracking()
            .SingleAsync(p => p.WorkflowStepResultId == stepId && p.MaterialId == materialId);
        Assert.Equal(fixture.SeededUserId, stored.RecordedByUserId);
    }

    private const string WorkflowStepIndex = "IX_ConfirmatoryPlateObservations_WorkflowStepResultId_Material~";

    [PostgresFact]
    public async Task WorkflowStepIndex_HasTheNonNullFilter()
    {
        await using var db = fixture.CreateDbContext();
        var definition = await db.Database.SqlQuery<string>($"""
            SELECT indexdef AS "Value"
            FROM pg_indexes
            WHERE schemaname = current_schema()
              AND tablename = 'ConfirmatoryPlateObservations'
              AND indexname = {WorkflowStepIndex}
            """).SingleAsync();
        Assert.Contains("WHERE (\"WorkflowStepResultId\" IS NOT NULL)", definition, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task WorkflowStepColumn_IsNullable()
    {
        await using var db = fixture.CreateDbContext();
        var nullable = await db.Database.SqlQueryRaw<string>("""
            SELECT is_nullable AS "Value"
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = 'ConfirmatoryPlateObservations'
              AND column_name = 'WorkflowStepResultId'
            """).SingleAsync();
        Assert.Equal("YES", nullable);
    }

    [PostgresFact]
    public async Task MigratedTable_MatchesModelColumnsKeysAndIndexes()
    {
        await using var db = fixture.CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var table = model.FindTable("ConfirmatoryPlateObservations", null)!;

        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT column_name || ':' || udt_name || ':' || is_nullable AS "Value"
            FROM information_schema.columns
            WHERE table_schema = current_schema() AND table_name = 'ConfirmatoryPlateObservations'
            """).ToListAsync();
        var expectedColumns = table.Columns.Where(c => c.Name != "xmin").Select(c =>
            $"{c.Name}:{(c.StoreType == "integer" ? "int4" : c.StoreType == "timestamp with time zone" ? "timestamptz" : c.StoreType)}:{(c.IsNullable ? "YES" : "NO")}");
        Assert.Equal(expectedColumns.Order(StringComparer.Ordinal), columns.Order(StringComparer.Ordinal));

        var indexes = await db.Database.SqlQueryRaw<string>("""
            SELECT regexp_replace(indexdef, 'INDEX "[^"]+" ON', 'INDEX ON') AS "Value"
            FROM pg_indexes
            WHERE schemaname = current_schema() AND tablename = 'ConfirmatoryPlateObservations'
            """).ToListAsync();
        var expectedIndexes = table.Indexes.Select(i => IndexDefinition(i.Name, i.IsUnique, i.Columns, i.Filter))
            .Append(IndexDefinition(table.PrimaryKey!.Name, true, table.PrimaryKey.Columns, null));
        Assert.Equal(string.Join(Environment.NewLine, expectedIndexes.Order(StringComparer.Ordinal)), string.Join(Environment.NewLine, indexes.Order(StringComparer.Ordinal)));

        var foreignKeys = await db.Database.SqlQueryRaw<string>("""
            SELECT conname || ':' || pg_get_constraintdef(oid) AS "Value"
            FROM pg_constraint
            WHERE conrelid = '"ConfirmatoryPlateObservations"'::regclass AND contype = 'f'
            """).ToListAsync();
        var expectedForeignKeys = table.ForeignKeyConstraints.Select(f =>
            $"{f.Name}:FOREIGN KEY ({ColumnNames(f.Columns)}) REFERENCES \"{f.PrincipalTable.Name}\"({ColumnNames(f.PrincipalColumns)}) ON DELETE {f.OnDeleteAction.ToString().ToUpperInvariant()}");
        Assert.Equal(expectedForeignKeys.Order(StringComparer.Ordinal), foreignKeys.Order(StringComparer.Ordinal));
    }

    private static string IndexDefinition(string name, bool unique, IEnumerable<IColumn> columns, string? filter)
    {
        // Index names are not compared here: the hand-written location-links migration truncated one long name
        // one character shorter than the model name (cosmetic, unrelated to CONF-214). The WorkflowStep index
        // name is asserted exactly by WorkflowStepIndex_HasTheNonNullFilter.
        var uniqueness = unique ? "UNIQUE " : "";
        var predicate = filter is null ? "" : $" WHERE ({filter})";
        return $"CREATE {uniqueness}INDEX ON public.\"ConfirmatoryPlateObservations\" USING btree ({ColumnNames(columns)}){predicate}";
    }

    private static string ColumnNames(IEnumerable<IColumn> columns) =>
        string.Join(", ", columns.Select(c => $"\"{c.Name}\""));

    private ConfirmatoryPlateObservation Plate(int materialId, LocationPathogenObservation? location = null, int? stepId = null) => new()
    {
        MaterialId = materialId,
        WorkflowStepResultId = stepId,
        LocationPathogenObservationId = location?.Id,
        SampleLocationId = location?.SampleLocationId,
        MediumIndex = 1,
        Observation = GrowthObservation.GrowthConforming,
        ExpectedAppearanceSnapshot = "Test appearance",
        RecordedByUserId = fixture.SeededUserId,
        RecordedAtUtc = RecordedAt
    };

    private async Task<(int MaterialId, LocationPathogenObservation First, LocationPathogenObservation Second, int StepId)> SeedAsync()
    {
        await using var db = fixture.CreateDbContext();
        var actor = fixture.SeededUserId;
        db.CurrentUserId = actor;
        var section = await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var cause = new CauseOfTesting { Name = $"Plate test {tag}" };
        var sample = new Sample
        {
            ReferenceNumber = $"PLATE-{tag}",
            ControlNumber = $"PLATE-CTRL-{tag}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTesting = cause,
            ReceivedByUserId = actor,
            SampledBy = "Plate test"
        };
        var order = new TestOrder
        {
            Sample = sample,
            TestCode = "PLATE",
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Incubating,
            SectionId = section.Id
        };
        LocationPathogenObservation LocationObservation() => new()
        {
            SampleLocation = new SampleLocation
            {
                Sample = sample,
                TestOrder = order,
                LocationType = LocationType.Room,
                DilutionFactor = 1
            },
            TestOrder = order,
            GrowthObservation = GrowthObservation.GrowthConforming,
            ObservedByUserId = actor
        };

        var first = LocationObservation();
        var second = LocationObservation();
        db.LocationPathogenObservations.AddRange(first, second);

        var step = new WorkflowStepResult
        {
            TestOrder = order,
            Incubation = new Incubation { TestOrder = order, StepName = "Confirmatory plating", StartedAt = RecordedAt },
            StepName = "Confirmatory plating",
            StepType = StepType.ConfirmatoryPlating,
            SubmittedByUserId = actor,
            SubmittedAtUtc = RecordedAt
        };
        db.WorkflowStepResults.Add(step);

        var material = new Material
        {
            SectionId = section.Id,
            MaterialName = "Test medium",
            BatchNumber = tag,
            ReceivingDate = RecordedAt,
            CreatedByUserId = actor,
            LastModifiedByUserId = actor
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        return (material.Id, first, second, step.Id);
    }
}
