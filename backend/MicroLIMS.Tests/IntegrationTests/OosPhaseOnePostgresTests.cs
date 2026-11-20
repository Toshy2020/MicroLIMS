using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

public sealed class OosPhaseOnePostgresTests(PostgresTestFixture fixture) : IClassFixture<PostgresTestFixture>
{
    private readonly ActionGuardScenarios _seed = new(fixture);
    private const string Statement = "Recorded the preparation and measurement.";
    private const string Key = "81732fe5-3b67-48aa-8d9f-c00f8c93c3eb";
    private const string EntityType = nameof(OosInvestigation);

    [PostgresFact]
    public async Task Template_save_is_audited_and_stale_version_is_refused()
    {
        var section = await _seed.SectionAsync();
        var head = await HeadAsync(section);
        await using (var act = fixture.CreateDbContext())
            await Templates(act).SaveAsync(section.Id, OosInvestigationKind.Oos, Template("Original"), 0, head.Id);

        uint version;
        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.OosInvestigationTemplates.AsNoTracking().SingleAsync(t => t.SectionId == section.Id);
            var item = await verify.OosChecklistItems.AsNoTracking().SingleAsync(i => i.TemplateId == row.Id);
            Assert.Equal("Original", item.Text);
            Assert.Equal(Key, item.Key);
            Assert.Equal(2, row.PhaseOneDueWorkingDays);
            version = row.Version;
            var audit = await AuditAsync(verify, nameof(OosChecklistItem), item.Id, head.Id, "Create");
            Assert.Null(audit.PreviousValue);
            Assert.Equal("Original", Field(audit.NewValue, "Text").GetString());
        }

        await using (var act = fixture.CreateDbContext())
            await Templates(act).SaveAsync(section.Id, OosInvestigationKind.Oos, Template("Updated"), version, head.Id);
        await using (var act = fixture.CreateDbContext())
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Templates(act).SaveAsync(section.Id, OosInvestigationKind.Oos, Template("Stale"), version, head.Id));

        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.OosInvestigationTemplates.AsNoTracking().SingleAsync(t => t.SectionId == section.Id);
            var item = await verify.OosChecklistItems.AsNoTracking().SingleAsync(i => i.TemplateId == row.Id);
            Assert.NotEqual(version, row.Version);
            Assert.Equal("Updated", item.Text);
            var audit = await AuditAsync(verify, nameof(OosChecklistItem), item.Id, head.Id, "Update");
            Assert.Equal("Original", Field(audit.PreviousValue, "Text").GetString());
            Assert.Equal("Updated", Field(audit.NewValue, "Text").GetString());
        }
    }

    [PostgresFact]
    public async Task Investigation_keeps_its_snapshot_after_template_edit()
    {
        var world = await OpenAsync();
        string before;
        uint version;
        await using (var verify = fixture.CreateDbContext())
        {
            before = (await ReadAsync(verify, world.Id)).TemplateSnapshotJson;
            version = await verify.OosInvestigationTemplates.AsNoTracking().Where(t => t.SectionId == world.Section)
                .Select(t => t.Version).SingleAsync();
        }
        await using (var act = fixture.CreateDbContext())
            await Templates(act).SaveAsync(world.Section, OosInvestigationKind.Oos, Template("Replacement"), version, world.Head);
        await using (var verify = fixture.CreateDbContext())
            Assert.Equal(before, (await ReadAsync(verify, world.Id)).TemplateSnapshotJson);
    }

    [PostgresFact]
    public async Task Phase_one_refused_with_empty_template() =>
        await RefusesAsync(await OpenAsync(template: false), "Oos.PhaseOneTemplateEmpty");

    [PostgresFact]
    public async Task Only_performing_analyst_can_append_statement()
    {
        var world = await OpenAsync(statement: false, answer: false);
        var section = await SectionAsync(world.Section);
        var other = await _seed.UserAsync(RoleType.Analyst, [PermissionConstants.OosContribute], section);
        await using (var act = fixture.CreateDbContext())
            Assert.Equal("Oos.StatementPerformerRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Phase(act, other.Id).AddAnalystStatementAsync(world.Id, Statement, other.Id))).Code);

        await using (var act = fixture.CreateDbContext())
            await Phase(act, world.Analyst).AddAnalystStatementAsync(world.Id, Statement, world.Analyst);

        await using var verify = fixture.CreateDbContext();
        var entry = await verify.OosInvestigationEntrys.AsNoTracking().SingleAsync(e => e.InvestigationId == world.Id);
        Assert.Equal(Statement, entry.Text);
        Assert.Equal(world.Analyst, entry.RecordedByUserId);
        Assert.Equal(OosEntryKind.AnalystStatement, entry.Kind);
        Assert.Equal(1, entry.Phase);
        Assert.Equal(Now.GetUtcNow().UtcDateTime, entry.RecordedAtUtc);
        var audit = await AuditAsync(verify, nameof(OosInvestigationEntry), entry.Id, world.Analyst, "Create");
        Assert.Null(audit.PreviousValue);
        Assert.Equal(Statement, Field(audit.NewValue, "Text").GetString());
    }

    [PostgresFact]
    public async Task Unanswered_active_item_refuses_signature() =>
        await RefusesAsync(await OpenAsync(answer: false), "Oos.PhaseOneChecklistIncomplete");

    [PostgresFact]
    public async Task Missing_analyst_statement_refuses_signature() =>
        await RefusesAsync(await OpenAsync(statement: false), "Oos.PhaseOneStatementRequired");

    [PostgresFact]
    public async Task Incorrect_password_leaves_phase_and_results_unchanged()
    {
        var world = await OpenAsync();
        await RefusesAsync(world, "Oos.PhaseOnePasswordFailed", password: "incorrect");
        await using var verify = fixture.CreateDbContext();
        Assert.True(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityId == $"OosInvestigation:{world.Id}" && a.UserId == world.Head && a.Action == "SignatureFailed"));
    }

    [PostgresFact]
    public async Task Signer_who_performed_the_test_is_refused_with_exact_message()
    {
        var world = await OpenAsync();
        await _seed.UpdateAsync<TestOrder>(world.Order, o => o.AssignedAnalystId = world.Head);
        var error = await RefusesAsync(world, "Oos.SignerPerformedTest");
        Assert.Equal("You performed a test in this investigation, so you cannot sign it.", error.Message);
    }

    [PostgresFact]
    public async Task Other_sections_head_cannot_sign()
    {
        var world = await OpenAsync();
        var outsider = await HeadAsync(await _seed.SectionAsync());
        await RefusesAsync<CodedAccessDeniedException>(world, "Oos.SectionAccessDenied", signer: outsider.Id);
    }

    [PostgresFact]
    public async Task No_cause_found_cannot_confirm_laboratory_error()
    {
        var world = await OpenAsync();
        await using var verify = fixture.CreateDbContext();
        var id = await verify.OosCauseCategories.AsNoTracking().Where(c => c.IsNoCauseFound).Select(c => c.Id).SingleAsync();
        await RefusesAsync(world, "Oos.PhaseOneAssignableCauseRequired", category: id);
    }

    [PostgresFact]
    public async Task Category_deactivated_after_opening_is_refused_at_signing()
    {
        var editor = await HeadAsync(await _seed.SectionAsync(), master: true);
        int id;
        var name = $"Retired {Guid.NewGuid():N}";
        await using (var act = fixture.CreateDbContext())
            id = (await Templates(act).SaveAsync(null, new(name, true, "Add cause"), editor.Id)).Id;
        var world = await OpenAsync();
        await using (var act = fixture.CreateDbContext())
            await Templates(act).SaveAsync(id, new(name, false, "Retire cause"), editor.Id);
        await RefusesAsync(world, "Oos.PhaseOneCauseInactive", category: id);
    }

    [PostgresFact]
    public async Task Laboratory_error_invalidates_links_and_preserves_original_results()
    {
        var world = await OpenAsync();
        var before = await ResultRowsAsync(world.Order);
        await SignAsync(world, category: 1);
        Assert.Equal(before, await ResultRowsAsync(world.Order));
        await AssertSignedAsync(world, OosInvestigationStatus.AwaitingRetest, OosPhaseOneAssessment.LaboratoryErrorConfirmed, 1);
    }

    [PostgresFact]
    public async Task No_error_starts_phase_two_and_second_signature_is_refused()
    {
        var world = await OpenAsync();
        await SignAsync(world);
        await AssertSignedAsync(world, OosInvestigationStatus.PhaseTwo, OosPhaseOneAssessment.NoLaboratoryErrorFound);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => SignAsync(world));
        Assert.Equal("Oos.PhaseOneClosed", error.Code);
        await AssertSignedAsync(world, OosInvestigationStatus.PhaseTwo, OosPhaseOneAssessment.NoLaboratoryErrorFound);
    }

    [PostgresFact]
    public async Task Category_edit_requires_both_permissions_and_audits_the_reason()
    {
        var section = await _seed.SectionAsync();
        var head = await HeadAsync(section);
        var editor = await HeadAsync(section, master: true);
        var name = $"Assignable cause {Guid.NewGuid():N}";
        var request = new SaveOosCategoryRequest(name, true, "New approved cause");
        await using (var act = fixture.CreateDbContext())
            Assert.Equal("Oos.CategoryAccessDenied", (await Assert.ThrowsAsync<CodedAccessDeniedException>(() =>
                Templates(act).SaveAsync(null, request, head.Id))).Code);
        await using (var verify = fixture.CreateDbContext())
            Assert.False(await verify.OosCauseCategories.AsNoTracking().AnyAsync(c => c.Name == name));

        int id;
        await using (var act = fixture.CreateDbContext())
            id = (await Templates(act).SaveAsync(null, request, editor.Id)).Id;
        await using (var act = fixture.CreateDbContext())
            await Templates(act).SaveAsync(id, new(name + " revised", false, "Retired after review"), editor.Id);
        await using (var verify = fixture.CreateDbContext())
        {
            var row = await verify.OosCauseCategories.AsNoTracking().SingleAsync(c => c.Id == id);
            Assert.Equal(name + " revised", row.Name);
            Assert.False(row.IsActive);
            var audit = await AuditAsync(verify, nameof(OosCauseCategory), id, editor.Id, "Update");
            Assert.Equal(name, Field(audit.PreviousValue, "Name").GetString());
            Assert.Equal(name + " revised", Field(audit.NewValue, "Name").GetString());
            Assert.Equal("Retired after review", audit.Reason);
        }
    }

    [PostgresFact]
    public async Task No_cause_found_cannot_be_renamed_or_deactivated()
    {
        var editor = await HeadAsync(await _seed.SectionAsync(), master: true);
        int id;
        await using (var verify = fixture.CreateDbContext())
            id = await verify.OosCauseCategories.AsNoTracking().Where(c => c.IsNoCauseFound).Select(c => c.Id).SingleAsync();
        foreach (var request in new SaveOosCategoryRequest[] { new("Other", true, "Reason"), new("No cause found", false, "Reason") })
        {
            await using var act = fixture.CreateDbContext();
            Assert.Equal("Oos.CategoryFixed", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Templates(act).SaveAsync(id, request, editor.Id))).Code);
        }
        await using (var verify = fixture.CreateDbContext())
        {
            var category = await verify.OosCauseCategories.AsNoTracking().SingleAsync(c => c.Id == id);
            Assert.Equal("No cause found", category.Name);
            Assert.True(category.IsActive);
            Assert.False(await verify.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.EntityName == nameof(OosCauseCategory) && a.EntityId == id.ToString() && a.UserId == editor.Id));
        }
    }

    [PostgresFact]
    public async Task Category_change_preserves_a_4000_character_audit_reason()
    {
        var editor = await HeadAsync(await _seed.SectionAsync(), master: true);
        var reason = new string('x', 4000);
        var name = $"Documented cause {Guid.NewGuid():N}";
        int id;
        await using (var act = fixture.CreateDbContext())
            id = (await Templates(act).SaveAsync(null, new(name, true, reason), editor.Id)).Id;

        await using var verify = fixture.CreateDbContext();
        Assert.Equal(name, await verify.OosCauseCategories.AsNoTracking().Where(c => c.Id == id).Select(c => c.Name).SingleAsync());
        var audit = await AuditAsync(verify, nameof(OosCauseCategory), id, editor.Id, "Create");
        Assert.Null(audit.PreviousValue);
        Assert.Equal(name, Field(audit.NewValue, "Name").GetString());
        Assert.Equal(reason, await verify.Set<AuditEventChange>().AsNoTracking()
            .Where(c => c.AuditLogId == audit.Id && c.FieldName == "Reason").Select(c => c.NewValue).SingleAsync());
    }

    [PostgresFact]
    public async Task Checklist_answers_append_with_audit_and_stale_signing_is_refused()
    {
        var world = await OpenAsync();
        uint oldVersion;
        await using (var verify = fixture.CreateDbContext())
            oldVersion = (await ReadAsync(verify, world.Id)).Version;
        await using (var act = fixture.CreateDbContext(Now))
            await Phase(act, world.Analyst).AnswerChecklistAsync(world.Id, 1, Key, OosChecklistAnswer.No, "Rechecked", world.Analyst);

        await using (var verify = fixture.CreateDbContext())
        {
            var current = await ReadAsync(verify, world.Id);
            Assert.NotEqual(oldVersion, current.Version);
            var answers = await verify.OosInvestigationEntrys.AsNoTracking()
                .Where(e => e.InvestigationId == world.Id && e.ChecklistItemKey == Key).OrderBy(e => e.Id).ToListAsync();
            Assert.Collection(answers, answer => Assert.Equal(OosChecklistAnswer.Yes, answer.Answer),
                answer => Assert.Equal(OosChecklistAnswer.No, answer.Answer));
            var last = answers[1];
            Assert.Equal(Now.GetUtcNow().UtcDateTime, last.RecordedAtUtc);
            var audit = await AuditAsync(verify, nameof(OosInvestigationEntry), last.Id, world.Analyst, "Create");
            Assert.Null(audit.PreviousValue);
            Assert.Equal("Rechecked", Field(audit.NewValue, "Text").GetString());
        }
        await using (var act = fixture.CreateDbContext(Now))
        {
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Phase(act, world.Head).SignPhaseOneAsync(world.Id,
                new(OosPhaseOneAssessment.NoLaboratoryErrorFound, null, null, ActionGuardScenarios.Password), oldVersion, world.Head, null));
            Assert.Equal("Oos.VersionConflict", error.Code);
        }
        await using (var verify = fixture.CreateDbContext())
        {
            Assert.Equal(OosInvestigationStatus.PhaseOne, (await ReadAsync(verify, world.Id)).Status);
            Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == EntityType && s.EntityId == world.Id));
        }
    }

    [PostgresFact]
    public async Task Signing_locks_investigation_before_password_verification()
    {
        var world = await OpenAsync();
        await using var hold = fixture.CreateDbContext();
        await using var transaction = await hold.Database.BeginTransactionAsync();
        await hold.Database.ExecuteSqlInterpolatedAsync(
            $"""SELECT 1 FROM "OosInvestigations" WHERE "Id" = {world.Id} FOR UPDATE""");
        await using var act = fixture.CreateDbContext(Now);
        await act.Database.OpenConnectionAsync();
        await act.Database.ExecuteSqlRawAsync("SET lock_timeout = '400ms'");
        var current = await ReadAsync(act, world.Id);
        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            Phase(act, world.Head).SignPhaseOneAsync(world.Id,
                new(OosPhaseOneAssessment.NoLaboratoryErrorFound, null, null, "incorrect"), current.Version, world.Head, null));
        var databaseError = Assert.IsType<Npgsql.PostgresException>(error.GetBaseException());
        Assert.Equal(Npgsql.PostgresErrorCodes.LockNotAvailable, databaseError.SqlState);
        await transaction.RollbackAsync();
    }

    [PostgresFact]
    public async Task Facts_return_recorded_incubation_and_result_audit_and_omit_missing_data()
    {
        var world = await OpenAsync();
        var started = Now.GetUtcNow().UtcDateTime;
        await using (var act = fixture.CreateDbContext())
        {
            var facts = Assert.Single((await Phase(act, world.Analyst).GetFactsAsync(world.Id)).Orders);
            Assert.Empty(facts.Incubations);
            Assert.Empty(facts.Equipment);
            Assert.Empty(facts.Media);
            Assert.Empty(facts.Solutions);
            Assert.Empty(facts.Standards);
            Assert.Equal(1, facts.ResultAuditCount);
        }
        await using (var arrange = fixture.CreateDbContext())
        {
            var testCode = await arrange.TestOrders.AsNoTracking().Where(o => o.Id == world.Order).Select(o => o.TestCode).SingleAsync();
            arrange.TestWorkflowSteps.Add(new()
            {
                TestDefinition = new() { SectionId = world.Section, Code = testCode, DisplayName = "Incubated test" },
                StepOrder = 1, StepName = "Incubation", IncubationMinHours = 2, IncubationMaxHours = 4
            });
            arrange.Incubations.Add(new() { TestOrderId = world.Order, StepNumber = 1,
                StartedAt = started, CompletedAt = started.AddHours(2),
                IncubationStartUtc = started, IncubationEndUtc = started.AddHours(3) });
            await arrange.SaveChangesAsync();
        }
        await using (var act = fixture.CreateDbContext())
        {
            var facts = Assert.Single((await Phase(act, world.Analyst).GetFactsAsync(world.Id)).Orders);
            var incubation = Assert.Single(facts.Incubations);
            Assert.Equal(started, incubation.StartedAt);
            Assert.Equal(started.AddHours(2), incubation.CompletedAt);
            Assert.Equal(started.AddHours(3), incubation.IncubationEndUtc);
            Assert.Equal(2, incubation.CurrentMinimumHours);
            Assert.Equal(4, incubation.CurrentMaximumHours);
        }
    }

    [PostgresFact]
    public async Task Facts_return_linked_equipment_media_solutions_and_standards()
    {
        var world = await OpenAsync();
        var started = Now.GetUtcNow().UtcDateTime;
        var section = await SectionAsync(world.Section);
        var media = (await _seed.MediaAsync(section, world.Analyst, evaluation: null)).Media;
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.MediaUsages.Add(new()
            {
                MediaId = media.Id, TestOrderId = world.Order, UsedAt = started, UsedByUserId = world.Analyst
            });
            var recipe = new SolutionMaster
            {
                SectionId = world.Section, Name = "Run recipe", CreatedByUserId = world.Analyst,
                LastModifiedByUserId = world.Analyst, CreatedAt = started, LastModifiedAt = started
            };
            var preparation = new SolutionPreparation
            {
                SectionId = world.Section, Code = "SOLUTION-1", SolutionMaster = recipe, StartedAt = started,
                StartedByUserId = world.Analyst, ReleasedAt = started, ExpiresAt = started.AddDays(1),
                ApprovalStatus = ApprovalGateStatus.Approved
            };
            var instrument = new Equipment
            {
                SectionId = world.Section, Name = "Run instrument", Code = "EQUIPMENT-1", Type = EquipmentType.Hplc,
                CalibrationDueDate = started.AddDays(1)
            };
            var column = new ChromatographyColumn
            {
                SectionId = world.Section, Name = "Run column", Code = "COLUMN-1",
                CreatedByUserId = world.Analyst, LastModifiedByUserId = world.Analyst
            };
            var method = new HplcMethod
            {
                SectionId = world.Section, Name = "Run method", Abbreviation = "RUN",
                EffectiveDate = started, DiluentSolution = recipe
            };
            var standardEntry = new MaterialMasterEntry
            {
                SectionId = world.Section, Code = "STD-MASTER", Name = "Run standard", Category = MaterialMasterCategory.ReferenceStandard,
                CreatedByUserId = world.Analyst, LastModifiedByUserId = world.Analyst
            };
            var analyte = new HplcMethodAnalyte { Name = "Target", StandardEntry = standardEntry };
            method.Analytes.Add(analyte);
            arrange.HplcMethods.Add(method);
            await arrange.SaveChangesAsync();
            var standard = new Material
            {
                SectionId = world.Section, MaterialName = "Run standard", BatchNumber = "STANDARD-1",
                MaterialType = MaterialType.ReferenceStandard, MaterialMasterEntry = standardEntry, ReceivingDate = started, ExpiryDate = started.AddDays(30)
            };
            var run = new HplcRun
            {
                SectionId = world.Section, Code = "RUN-1", Equipment = instrument, HplcMethod = method,
                ChromatographyColumn = column, AnalystUserId = world.Analyst, StartedAt = started,
                MobilePhases = [new() { Channel = "A", SolutionPreparation = preparation }],
                Samples = [new() { TestOrderId = world.Order, AssignedAt = started, AssignedByUserId = world.Analyst }],
                Sst = new()
                {
                    Code = "SST-1",
                    Analytes = [new() { HplcMethodAnalyteId = analyte.Id, StandardMaterial = standard, StandardPurityPercent = 99m }]
                }
            };
            arrange.HplcRuns.Add(run);
            arrange.IcpRuns.Add(new()
            {
                SectionId = world.Section, Code = "ICP-1", Equipment = instrument,
                StartedAt = started, AnalystUserId = world.Analyst,
                Calibration = new() { Code = "CAL-1", Status = IcpCalibrationStatus.Confirmed,
                    ConfirmedAt = started, CalibrationStandardMaterial = standard },
                Samples = [new() { TestOrderId = world.Order, AssignedAt = started.AddMinutes(1), AssignedByUserId = world.Analyst }]
            });
            await arrange.SaveChangesAsync();
        }
        await using (var act = fixture.CreateDbContext())
        {
            var facts = Assert.Single((await Phase(act, world.Analyst).GetFactsAsync(world.Id)).Orders);
            var hplc = Assert.Single(facts.Equipment, e => e.RunCalibrationStatus is null);
            Assert.Equal(started, hplc.UsedAt);
            var icp = Assert.Single(facts.Equipment, e => e.RunCalibrationStatus is not null);
            Assert.Equal(IcpCalibrationStatus.Confirmed, icp.RunCalibrationStatus);
            Assert.Equal(started, icp.RunCalibrationConfirmedAt);
            var lot = Assert.Single(facts.Media);
            Assert.Equal(media.LotNumber, lot.LotNumber);
            Assert.Equal(media.ExpiryDate, lot.ExpiryDate);
            Assert.Equal(media.IsReleasedForUse, lot.IsReleasedForUse);
            var solution = Assert.Single(facts.Solutions);
            Assert.Equal("SOLUTION-1", solution.Code);
            Assert.Equal(started, solution.ReleasedAt);
            Assert.Equal(started.AddDays(1), solution.ExpiresAt);
            Assert.Equal(99m, Assert.Single(facts.Standards, s => s.PurityPercent is not null).PurityPercent);
            Assert.Single(facts.Standards, s => s.PurityPercent is null);
        }
    }

    [PostgresFact]
    public async Task Facts_include_titration_lots_recorded_in_analysis_snapshot()
    {
        var world = await OpenAsync();
        var started = Now.GetUtcNow().UtcDateTime;
        int preparationId;
        int materialId;
        User analyst;
        await using (var arrange = fixture.CreateDbContext())
        {
            analyst = await arrange.Users.AsNoTracking().SingleAsync(u => u.Id == world.Analyst);
            var preparation = new SolutionPreparation
            {
                SectionId = world.Section, Code = "VS-1", Type = SolutionType.Titrant,
                SolutionMaster = new()
                {
                    SectionId = world.Section, Name = "Titrant recipe", Type = SolutionType.Titrant,
                    CreatedByUserId = world.Analyst, LastModifiedByUserId = world.Analyst, CreatedAt = started, LastModifiedAt = started
                },
                StartedAt = started, StartedByUserId = world.Analyst, ExpiresAt = started.AddDays(2)
            };
            var material = new Material
            {
                SectionId = world.Section, MaterialName = "Titration standard", BatchNumber = "STD-1",
                ReceivingDate = started, ExpiryDate = started.AddDays(30), MaterialType = MaterialType.ReferenceStandard
            };
            arrange.SolutionPreparations.Add(preparation);
            arrange.Materials.Add(material);
            await arrange.SaveChangesAsync();
            preparationId = preparation.Id;
            materialId = material.Id;
        }

        await _seed.AnalysisAsync(world.Order, analyst, JsonSerializer.Serialize(new
        {
            titrant = new { preparationId },
            standard = new { lotId = materialId, purityPercent = 98m, moisturePercent = 1m }
        }));

        await using var act = fixture.CreateDbContext();
        var facts = Assert.Single((await Phase(act, world.Analyst).GetFactsAsync(world.Id)).Orders);
        var solution = Assert.Single(facts.Solutions);
        Assert.Equal(preparationId, solution.SolutionPreparationId);
        Assert.Equal("VS-1", solution.Code);
        Assert.Equal(started.AddDays(2), solution.ExpiresAt);
        Assert.Null(solution.ReleasedAt);
        var standard = Assert.Single(facts.Standards);
        Assert.Equal(materialId, standard.MaterialId);
        Assert.Equal(98m, standard.PurityPercent);
        Assert.Equal(1m, standard.MoisturePercent);
        Assert.Equal(2, facts.ResultAuditCount);
    }

    private Task<BusinessRuleException> RefusesAsync(World world, string code, int? signer = null,
        int? category = null, string? password = null) =>
        RefusesAsync<BusinessRuleException>(world, code, signer, category, password);

    private async Task<T> RefusesAsync<T>(World world, string code, int? signer = null,
        int? category = null, string? password = null) where T : Exception, MicroLIMS.Shared.Messages.ICodedMessage
    {
        OosInvestigation before;
        await using (var verify = fixture.CreateDbContext())
            before = await ReadAsync(verify, world.Id);
        var error = await Assert.ThrowsAsync<T>(() => SignAsync(world, signer, category, password));
        Assert.Equal(code, error.Code);
        await using (var verify = fixture.CreateDbContext())
        {
            var current = await ReadAsync(verify, world.Id);
            Assert.Equal(before.Version, current.Version);
            Assert.Equal(OosInvestigationStatus.PhaseOne, current.Status);
            Assert.Null(current.PhaseOneAssessment);
            Assert.Null(current.PhaseOneSignatureId);
            Assert.False(await verify.ElectronicSignatures.AsNoTracking().AnyAsync(s => s.EntityType == EntityType && s.EntityId == world.Id));
            Assert.False(await verify.OosInvestigationResults.AsNoTracking().AnyAsync(r => r.InvestigationId == world.Id && r.IsInvalidated));
        }
        return error;
    }

    private async Task AssertSignedAsync(World world, OosInvestigationStatus status, OosPhaseOneAssessment assessment, int? category = null)
    {
        await using var verify = fixture.CreateDbContext();
        var row = await ReadAsync(verify, world.Id);
        Assert.Equal(status, row.Status);
        Assert.Equal(assessment, row.PhaseOneAssessment);
        Assert.Equal(category, row.PhaseOneCauseCategoryId);
        Assert.Equal(world.Head, row.PhaseOneSignedByUserId);
        Assert.Equal(Now.GetUtcNow().UtcDateTime, row.PhaseOneSignedAtUtc);
        var signature = await verify.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.EntityType == EntityType && s.EntityId == world.Id);
        Assert.Equal(signature.Id, row.PhaseOneSignatureId);
        Assert.Equal(world.Head, signature.UserId);
        Assert.Equal(SignatureMeaning.OosPhaseOneConcluded, signature.MeaningOfSignature);
        var link = await verify.OosInvestigationResults.AsNoTracking().SingleAsync(r => r.InvestigationId == world.Id);
        Assert.Equal(category.HasValue, link.IsInvalidated);
        if (category.HasValue)
        {
            Assert.Equal(row.PhaseOneSignedAtUtc, link.InvalidatedAtUtc);
            var linkAudit = await AuditAsync(verify, nameof(OosInvestigationResult), link.Id, world.Head, "Update");
            Assert.False(Field(linkAudit.PreviousValue, "IsInvalidated").GetBoolean());
            Assert.True(Field(linkAudit.NewValue, "IsInvalidated").GetBoolean());
        }
        var audit = await AuditAsync(verify, EntityType, row.Id, world.Head, "Update");
        Assert.Equal((int)OosInvestigationStatus.PhaseOne, Field(audit.PreviousValue, "Status").GetInt32());
        Assert.Equal((int)status, Field(audit.NewValue, "Status").GetInt32());
    }

    private async Task SignAsync(World world, int? signer = null, int? category = null, string? password = null)
    {
        OosInvestigation investigation;
        await using (var read = fixture.CreateDbContext())
            investigation = await ReadAsync(read, world.Id);
        var actor = signer ?? world.Head;
        await using var act = fixture.CreateDbContext(Now);
        await Phase(act, actor).SignPhaseOneAsync(world.Id, new(
            category is null ? OosPhaseOneAssessment.NoLaboratoryErrorFound : OosPhaseOneAssessment.LaboratoryErrorConfirmed,
            category, category is null ? null : "Documented cause", password ?? ActionGuardScenarios.Password),
            investigation.Version, actor, null);
    }

    private async Task<World> OpenAsync(bool template = true, bool statement = true, bool answer = true)
    {
        var section = await _seed.SectionAsync();
        var analyst = await _seed.UserAsync(RoleType.Analyst, [PermissionConstants.OosContribute, PermissionConstants.OosView], section);
        var head = await HeadAsync(section);
        var reviewer = await _seed.ReviewerAsync(section);
        if (template)
        {
            await using var act = fixture.CreateDbContext();
            await Templates(act).SaveAsync(section.Id, OosInvestigationKind.Oos, Template("Review observations"), 0, head.Id);
        }
        var sample = await _seed.UnderReviewAsync(section, analystId: analyst.Id);
        var order = Assert.Single(sample.Orders);
        await using (var arrange = fixture.CreateDbContext())
        {
            arrange.Results.Add(new Result { TestOrderId = order.Id, EnteredByUserId = analyst.Id, InterpretedValue = nameof(ResultStatus.OutOfSpecification) });
            await arrange.SaveChangesAsync();
        }
        await _seed.CompleteReviewAsync(new(reviewer.Id, sample.Sample.Id, section.Id));
        int id;
        await using (var verify = fixture.CreateDbContext())
            id = await verify.OosInvestigations.AsNoTracking().Where(i => i.SampleId == sample.Sample.Id).Select(i => i.Id).SingleAsync();
        if (statement)
        {
            await using var act = fixture.CreateDbContext(Now);
            await Phase(act, analyst.Id).AddAnalystStatementAsync(id, Statement, analyst.Id);
        }
        if (template && answer)
        {
            await using var act = fixture.CreateDbContext(Now);
            await Phase(act, analyst.Id).AnswerChecklistAsync(id, 1, Key, OosChecklistAnswer.Yes, "Checked", analyst.Id);
        }
        return new(id, section.Id, order.Id, analyst.Id, head.Id);
    }

    private async Task<string[]> ResultRowsAsync(int order)
    {
        await using var verify = fixture.CreateDbContext();
        return await verify.Database.SqlQuery<string>(
            $"""SELECT r.xmin::text || ':' || row_to_json(r)::text AS "Value" FROM "Results" r WHERE r."TestOrderId" = {order} ORDER BY r."Id" """).ToArrayAsync();
    }

    private async Task<DocumentSection> SectionAsync(int id)
    {
        await using var verify = fixture.CreateDbContext();
        return await verify.DocumentSections.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    private Task<User> HeadAsync(DocumentSection section, bool master = false) =>
        _seed.UserAsync(RoleType.SectionHead, master
            ? [PermissionConstants.OosManage, PermissionConstants.MasterDataManage]
            : [PermissionConstants.OosManage], section);

    private static Task<OosInvestigation> ReadAsync(MicroLimsDbContext db, int id) =>
        db.OosInvestigations.AsNoTracking().SingleAsync(i => i.Id == id);

    private static Task<AuditLog> AuditAsync(MicroLimsDbContext db, string entity, int id, int actor, string action) =>
        db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == entity && a.EntityId == id.ToString() && a.UserId == actor && a.Action == action);

    private static JsonElement Field(string? json, string property)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(json));
        return document.RootElement.GetProperty(property).Clone();
    }

    private static SaveOosTemplateRequest Template(string text) => new(2, 5, false, [new(Key, 1, 1, text, true)]);
    private static OosTemplateService Templates(MicroLimsDbContext db) => new(db, new UserSectionScopeService(db), new PermissionService(db));
    private static OosInvestigationService Phase(MicroLimsDbContext db, int actor) =>
        TestServiceFactory.OosInvestigation(db, Now, actor);

    private static readonly FixedTimeProvider Now = new(new DateTimeOffset(2041, 9, 2, 10, 0, 0, TimeSpan.Zero));
    private sealed record World(int Id, int Section, int Order, int Analyst, int Head);
}
