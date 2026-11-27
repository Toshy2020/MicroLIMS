using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class CompetencyRecordingPostgresTests(PostgresTestFixture fixture)
{
    private const string Password = "Recording-password";
    private static readonly DateTime AnalysedAt = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task Incubation_and_reading_refuse_the_actor_even_when_assigned_analyst_is_qualified()
    {
        var setup = await SeedAsync();
        var actor = setup.UnqualifiedId;
        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = actor;
            var engine = TestServiceFactory.TestWorkflow(act);
            Func<Task>[] records =
            [
                () => engine.SelectMediaAsync(setup.A.Id, "Read", 0, 0, actor),
                () => engine.StartStage2IncubationAsync(setup.A.Id, "Read", 0, actor),
                () => engine.RecordResultAsync(setup.A.Id, "Read", new CountTestPayload(["2"], 1), actor),
                () => engine.CloseCurrentIncubationWindowAsync(setup.A.Id, actor),
                () => engine.RecordBatchResultsAsync(setup.A.Id, [], actor),
                () => engine.RecordBatchPathogenResultsAsync(setup.A.Id, [], actor),
                () => engine.RecordWaterBatchReadingsAsync(setup.A.Id, [], actor),
                () => engine.RecordLocationResultsAsync(setup.A.Id, [], actor),
                () => engine.SubmitBrothAsync(setup.A.Id, "Read", null, actor),
                () => engine.StartSelectivePlatingIncubationAsync(setup.A.Id, "Read", 0, 0, null, actor),
                () => engine.SubmitSelectivePlatingObservationAsync(setup.A.Id, "Read", GrowthObservation.NoGrowth, null, actor),
                () => engine.SubmitConfirmatorySetupAsync(setup.A.Id, "Read", [], DateTime.UnixEpoch, DateTime.UnixEpoch, actor),
                () => engine.SubmitConfirmatoryObservationsAsync(setup.A.Id, "Read", [], actor),
                () => engine.RecordAnalystDecisionAsync(setup.A.Id, AnalystDecision.SubmitAsDetected, actor),
                () => engine.SubmitBiochemicalAsync(setup.A.Id, "Read", "Observation", null, false, actor),
            ];
            await PostgresCases.RunAsync(records, async record =>
            {
                var refused = await Assert.ThrowsAsync<BusinessRuleException>(record);
                Assert.Equal("Competency.NotQualified", refused.Code);
                Assert.Equal(Refusal(setup.A), refused.Message);
            });
            var headRefused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                engine.RecordResultAsync(setup.A.Id, "Read", new CountTestPayload(["2"], 1), fixture.SeededControllerUserId));
            Assert.Equal("Competency.NotQualified", headRefused.Code);
            Assert.Equal(Refusal(setup.A), headRefused.Message);
        }

        await using var check = fixture.CreateDbContext();
        Assert.False(await check.Results.AsNoTracking().AnyAsync(r => r.TestOrderId == setup.A.Id));
        Assert.False(await check.Incubations.AsNoTracking().AnyAsync(i => i.TestOrderId == setup.A.Id));
        Assert.Equal(setup.Signatures, await check.ElectronicSignatures.AsNoTracking().CountAsync());
        Assert.Equal(setup.A.Status, await check.TestOrders.AsNoTracking().Where(o => o.Id == setup.A.Id).Select(o => o.Status).SingleAsync());
        Assert.Equal(setup.Audits, await check.AuditLogs.AsNoTracking().CountAsync());
    }

    [PostgresFact]
    public async Task Completing_the_session_refuses_the_whole_sample_and_lists_all_missing_qualifications()
    {
        var setup = await SeedAsync();
        int audits;
        await using (var arrange = fixture.CreateDbContext())
        {
            var locations = await arrange.SampleLocations.Where(l => l.SampleId == setup.SampleId).ToListAsync();
            var point = new WaterSamplingPoint { Code = $"COMP-{setup.SampleId}", Location = "Shared point" };
            foreach (var location in locations)
            {
                location.WaterSamplingPoint = point;
                location.CFUResult = 5m;
                location.CalculatedResult = 5m;
                location.ReportedResult = "5 CFU";
                location.Status = ResultStatus.WithinLimits;
                location.EnteredByUserId = setup.QualifiedId;
            }
            await arrange.SaveChangesAsync();
            audits = await arrange.AuditLogs.AsNoTracking().CountAsync();
        }

        await using (var act = fixture.CreateDbContext())
        {
            var service = TestServiceFactory.PathogenSession(act);
            var session = await service.GetSessionAsync(setup.SampleId);
            Assert.NotNull(session);
            Assert.Empty(session.MissingResults);
            var partial = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CompleteSessionAsync(setup.SampleId, setup.QualifiedId));
            Assert.Equal(Refusal(setup.B), partial.Message);
            var all = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CompleteSessionAsync(setup.SampleId, setup.UnqualifiedId));
            Assert.Equal("Competency.RecordingRefused", all.Code);
            Assert.Contains(Refusal(setup.A), all.Message);
            Assert.Contains(Refusal(setup.B), all.Message);
            var details = Assert.IsType<List<MicroLIMS.Shared.Responses.ApiError>>(all.ErrorDetails);
            Assert.Equal(2, details.Count);
            Assert.All(details, detail => Assert.Equal("Competency.NotQualified", detail.Code));
            Assert.Equal(new[] { setup.A.TestCode, setup.B.TestCode },
                details.Select(detail => detail.Params!["test"]).Order(StringComparer.Ordinal));
        }

        await using var check = fixture.CreateDbContext();
        var orders = await check.TestOrders.AsNoTracking().Where(o => o.SampleId == setup.SampleId).ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.All(orders, order => Assert.Equal(ApprovalStatus.InProgress, order.Status));
        Assert.Equal(SampleStatus.InTesting,
            await check.Samples.AsNoTracking().Where(s => s.Id == setup.SampleId).Select(s => s.Status).SingleAsync());
        Assert.Equal(audits, await check.AuditLogs.AsNoTracking().CountAsync());
    }

    [PostgresFact]
    public async Task Session_matrix_saves_only_the_qualified_order_and_restores_prior_audit_reason()
    {
        var setup = await SeedAsync();
        const string prior = "Prior";
        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = setup.QualifiedId;
            act.CurrentReason = prior;
            var response = await TestServiceFactory.PathogenSession(act).SaveResultMatrixAsync(setup.SampleId,
                new([Cell(setup.A), Cell(setup.B)]), setup.QualifiedId);
            AssertSkip(setup.B, response.Skipped);
            Assert.Equal(prior, act.CurrentReason);
        }

        await using var check = fixture.CreateDbContext();
        var saved = await check.SampleLocations.AsNoTracking().SingleAsync(l => l.Id == LocationId(setup.A));
        Assert.Equal(5m, saved.CalculatedResult);
        Assert.Equal(setup.QualifiedId, saved.EnteredByUserId);
        var refused = await check.SampleLocations.AsNoTracking().SingleAsync(l => l.Id == LocationId(setup.B));
        Assert.Null(refused.EnteredAt);
        Assert.Null(refused.CalculatedResult);
        var recordId = saved.Id.ToString();
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(SampleLocation) && a.EntityId == recordId && a.Action == "Update");
        Assert.Equal(setup.QualifiedId, audit.UserId);
        Assert.Equal("Prior SOP not checked: the item has no SOP number.", audit.Reason);
        Assert.Contains(nameof(SampleLocation.CalculatedResult), audit.NewValue);
    }

    [PostgresFact]
    public async Task Session_primary_save_skips_refused_orders_and_rejects_an_all_refused_save()
    {
        var setup = await SeedAsync();
        PrimaryObservationInput Observation(TestOrder order) =>
            new(LocationId(order), order.TestCode, GrowthObservation.NoGrowth);

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = setup.QualifiedId;
            var service = TestServiceFactory.PathogenSession(act);
            var response = await service.SavePrimaryObservationsAsync(setup.SampleId,
                new([Observation(setup.A), Observation(setup.B)]), setup.QualifiedId);
            AssertSkip(setup.B, response.Skipped);
        }

        await using (var act = fixture.CreateDbContext())
        {
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => TestServiceFactory.PathogenSession(act)
                .SavePrimaryObservationsAsync(setup.SampleId, new([Observation(setup.B)]), setup.QualifiedId));
            Assert.Equal("Competency.NotQualified", refused.Code);
            Assert.Equal(Refusal(setup.B), refused.Message);
        }

        await using var check = fixture.CreateDbContext();
        var row = await check.LocationPathogenObservations.AsNoTracking()
            .SingleAsync(o => o.TestOrderId == setup.A.Id || o.TestOrderId == setup.B.Id);
        Assert.Equal(setup.A.Id, row.TestOrderId);
        Assert.Equal(LocationId(setup.A), row.SampleLocationId);
        Assert.Equal(GrowthObservation.NoGrowth, row.GrowthObservation);
        Assert.Equal(setup.QualifiedId, row.ObservedByUserId);
        var id = row.Id.ToString();
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(LocationPathogenObservation) && a.EntityId == id && a.Action == "Create");
        Assert.Equal(setup.QualifiedId, audit.UserId);
        Assert.Equal("SOP not checked: the item has no SOP number.", audit.Reason);
    }

    [PostgresFact]
    public async Task Session_confirmatory_readings_skip_refused_orders_and_their_supporting_comments()
    {
        var setup = await SeedAsync();
        int firstId;
        int secondId;
        int materialId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var material = new Material
            {
                SectionId = setup.A.SectionId, MaterialName = "Confirmatory plate",
                BatchNumber = Guid.NewGuid().ToString("N"),
            };
            var orders = await arrange.TestOrders.Where(o => o.Id == setup.A.Id || o.Id == setup.B.Id).ToDictionaryAsync(o => o.Id);
            var locations = await arrange.SampleLocations.Where(l => l.SampleId == setup.SampleId).ToDictionaryAsync(l => l.TestOrderId);
            LocationPathogenObservation Observation(int orderId) => new()
            {
                TestOrder = orders[orderId], SampleLocation = locations[orderId],
                GrowthObservation = GrowthObservation.GrowthConforming, ObservedByUserId = setup.QualifiedId,
            };
            var first = Observation(setup.A.Id);
            var second = Observation(setup.B.Id);
            arrange.ConfirmatoryPlateObservations.AddRange(
                PathogenConfirmatoryReadingScopePostgresTests.ExistingPlate(first, material, setup.QualifiedId),
                PathogenConfirmatoryReadingScopePostgresTests.ExistingPlate(second, material, setup.QualifiedId));
            await arrange.SaveChangesAsync();
            (firstId, secondId, materialId) = (first.Id, second.Id, material.Id);
        }

        const string comment = "Supporting readout";
        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = setup.QualifiedId;
            var response = await TestServiceFactory.PathogenSession(act).SaveBatchConfirmatoryPlateReadingsAsync(
                setup.SampleId,
                new([new(firstId, 0, materialId, GrowthObservation.NoGrowth),
                    new(secondId, 0, materialId, GrowthObservation.NoGrowth)], comment),
                setup.QualifiedId);
            AssertSkip(setup.B, response.Skipped);
        }

        await using var check = fixture.CreateDbContext();
        var rows = await check.ConfirmatoryPlateObservations.AsNoTracking()
            .Where(p => p.LocationPathogenObservationId == firstId || p.LocationPathogenObservationId == secondId).ToListAsync();
        var saved = Assert.Single(rows, p => p.LocationPathogenObservationId == firstId);
        Assert.Equal(GrowthObservation.NoGrowth, saved.Observation);
        Assert.Equal(setup.QualifiedId, saved.RecordedByUserId);
        Assert.Equal(GrowthObservation.GrowthConforming, Assert.Single(rows, p => p.LocationPathogenObservationId == secondId).Observation);
        var history = await check.WorkflowHistories.AsNoTracking()
            .SingleAsync(h => (h.TestOrderId == setup.A.Id || h.TestOrderId == setup.B.Id) && h.Note != null && h.Note.Contains(comment));
        Assert.Equal(setup.A.Id, history.TestOrderId);
        var id = saved.Id.ToString();
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(ConfirmatoryPlateObservation) && a.EntityId == id && a.Action == "Update");
        Assert.Equal(setup.QualifiedId, audit.UserId);
        Assert.Equal("SOP not checked: the item has no SOP number.", audit.Reason);
    }

    [PostgresFact]
    public async Task Preparation_and_confirmatory_setup_are_refused_without_writing()
    {
        var setup = await SeedAsync();
        var actor = setup.QualifiedId;
        await using (var act = fixture.CreateDbContext())
        {
            var preparation = TestServiceFactory.SamplePreparation(act);
            var session = TestServiceFactory.PathogenSession(act);
            Func<Task>[] actions =
            [
                () => preparation.PrepareAsync(new(setup.SampleId, 1, "Filtration", null, null, "", "", actor, "invalid")),
                () => preparation.ConfirmFromConfigurationAsync(new(setup.SampleId, actor, "invalid")),
                () => session.StartSharedConfirmatorySetupAsync(setup.SampleId,
                    new(setup.B.Id, [LocationId(setup.B)], [], null, 0, null), actor),
            ];
            await PostgresCases.RunAsync(actions, async action =>
            {
                var refused = await Assert.ThrowsAsync<BusinessRuleException>(action);
                Assert.Equal("Competency.NotQualified", refused.Code);
                Assert.Equal(Refusal(setup.B), refused.Message);
            });
        }

        await using var check = fixture.CreateDbContext();
        Assert.False(await check.SamplePreparations.AsNoTracking().AnyAsync(p => p.SampleId == setup.SampleId));
        Assert.False(await check.Incubations.AsNoTracking().AnyAsync(i => i.TestOrderId == setup.B.Id));
        Assert.Equal(setup.Signatures, await check.ElectronicSignatures.AsNoTracking().CountAsync());
        Assert.Equal(setup.Audits, await check.AuditLogs.AsNoTracking().CountAsync());
    }

    [PostgresFact]
    public async Task Grouped_preparation_skips_the_whole_sample_when_any_order_is_refused()
    {
        var setup = await SeedAsync();
        int configId;
        int auditCount;
        await using (var arrange = fixture.CreateDbContext())
        {
            var sample = await arrange.Samples.SingleAsync(s => s.Id == setup.SampleId);
            var config = new ItemPreparationConfiguration
            {
                ItemId = sample.ItemId!.Value,
                Amount = 1, Technique = "PourPlate", Diluent = "Configured diluent", Neutralizer = "Configured neutralizer",
                CreatedByUserId = setup.QualifiedId,
            };
            arrange.ItemPreparationConfigurations.Add(config);
            await arrange.SaveChangesAsync();
            configId = config.Id;
            auditCount = await arrange.AuditLogs.AsNoTracking().CountAsync();
        }

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = setup.QualifiedId;
            var response = await TestServiceFactory.SamplePreparation(act).ConfirmBatchFromConfigurationAsync(
                new([setup.SampleId], configId, Password), setup.QualifiedId);
            Assert.Empty(response.Succeeded);
            var skip = Assert.Single(response.Skipped);
            Assert.Equal(setup.SampleId, skip.SampleId);
            Assert.Equal("Competency.NotQualified", skip.Code);
            Assert.Equal(Refusal(setup.B), skip.Reason);
        }

        await using var check = fixture.CreateDbContext();
        Assert.False(await check.SamplePreparations.AsNoTracking().AnyAsync(p => p.SampleId == setup.SampleId));
        Assert.Equal(setup.Signatures, await check.ElectronicSignatures.AsNoTracking().CountAsync());
        Assert.Equal(auditCount, await check.AuditLogs.AsNoTracking().CountAsync());
    }

    [PostgresFact]
    public async Task Preparation_checks_only_covered_orders_and_audits_the_qualified_actor()
    {
        await PostgresCases.RunAsync(new[] { false, true }, async refuseCoveredOrder =>
        {
            var setup = await SeedAsync();
            await using (var arrange = fixture.CreateDbContext())
            {
                var sample = await arrange.Samples.SingleAsync(s => s.Id == setup.SampleId);
                sample.PreparationStatus = SamplePreparationStatus.NeedsPreparation;
                var fp = await arrange.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
                    ?? new DocumentSection { Code = "FP", Name = "Finished Products", DepartmentId = fixture.SeededDepartmentId };
                var other = new DocumentSection
                {
                    Code = $"OTHER-{setup.SampleId}", Name = $"Other lab {setup.SampleId}", DepartmentId = fixture.SeededDepartmentId,
                };
                if (fp.Id == 0) arrange.DocumentSections.Add(fp);
                arrange.DocumentSections.Add(other);
                await arrange.SaveChangesAsync();
                if (!await arrange.CompetencySectionSettings.AnyAsync(s => s.SectionId == fp.Id))
                    arrange.CompetencySectionSettings.Add(new CompetencySectionSetting { SectionId = fp.Id, GateEnabled = true });
                else
                    (await arrange.CompetencySectionSettings.SingleAsync(s => s.SectionId == fp.Id)).GateEnabled = true;
                arrange.CompetencySectionSettings.Add(new CompetencySectionSetting { SectionId = other.Id, GateEnabled = true });
                // The actor works in FP too, so only the FP rule (no preparation) keeps that order out of the covered set.
                arrange.UserOrgMemberships.Add(new UserOrgMembership
                {
                    UserId = setup.QualifiedId, SectionId = fp.Id, DepartmentId = fp.DepartmentId,
                });
                arrange.TestOrders.AddRange(
                    new TestOrder { SampleId = sample.Id, SectionId = fp.Id, TestCode = "FP-EXCLUDE", AssignedAnalystId = setup.UnqualifiedId },
                    new TestOrder { SampleId = sample.Id, SectionId = other.Id, TestCode = "OTHER-EXCLUDE" },
                    new TestOrder { SampleId = sample.Id, SectionId = setup.A.SectionId, TestCode = "CANCELLED-EXCLUDE",
                        Status = ApprovalStatus.Cancelled });
                var second = await arrange.TestOrders.SingleAsync(o => o.Id == setup.B.Id);
                second.Status = refuseCoveredOrder ? ApprovalStatus.Pending : ApprovalStatus.Approved;
                arrange.ItemPreparationConfigurations.Add(new ItemPreparationConfiguration
                {
                    ItemId = sample.ItemId!.Value, Amount = 1, Technique = "PourPlate",
                    Diluent = "Configured diluent", Neutralizer = "Configured neutralizer",
                    CreatedByUserId = setup.QualifiedId,
                });
                await arrange.SaveChangesAsync();
            }

            await using (var act = fixture.CreateDbContext())
            {
                act.CurrentUserId = setup.QualifiedId;
                var service = TestServiceFactory.SamplePreparation(act);
                var request = new ConfirmPreparationRequest(setup.SampleId, setup.QualifiedId, Password);
                if (refuseCoveredOrder)
                {
                    var refusal = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ConfirmFromConfigurationAsync(request));
                    Assert.Equal("Competency.NotQualified", refusal.Code);
                    Assert.Equal(Refusal(setup.B), refusal.Message);
                }
                else
                    await service.ConfirmFromConfigurationAsync(request);
            }

            await using var check = fixture.CreateDbContext();
            var status = await check.Samples.AsNoTracking().Where(s => s.Id == setup.SampleId)
                .Select(s => s.PreparationStatus).SingleAsync();
            var preparations = await check.SamplePreparations.AsNoTracking().Where(p => p.SampleId == setup.SampleId).ToListAsync();
            if (refuseCoveredOrder)
            {
                Assert.Equal(SamplePreparationStatus.NeedsPreparation, status);
                Assert.Empty(preparations);
            }
            else
            {
                Assert.Equal(SamplePreparationStatus.Ready, status);
                var prep = Assert.Single(preparations);
                Assert.Equal(setup.QualifiedId, prep.PreparedByUserId);
                Assert.True(prep.WasConfirmedFromConfig);
                var id = prep.Id.ToString();
                var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(SamplePreparation) &&
                    a.EntityId == id && a.Action == "Create");
                Assert.Equal(setup.QualifiedId, audit.UserId);
                Assert.Equal("SOP not checked: the item has no SOP number.", audit.Reason);
                Assert.Contains(nameof(SamplePreparation.PreparedByUserId), audit.NewValue);
            }
        });
    }

    [PostgresFact]
    public async Task Shared_start_and_matrix_reject_an_all_refused_batch()
    {
        var matrix = await SeedAsync();
        var shared = await SeedAsync();
        var (media, incubator) = await ArrangeSharedEnrichmentAsync(shared);
        await using (var act = fixture.CreateDbContext())
        {
            var service = TestServiceFactory.PathogenSession(act);
            var matrixError = await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveResultMatrixAsync(
                matrix.SampleId, new([Cell(matrix.B)]), matrix.QualifiedId));
            Assert.Equal(Refusal(matrix.B), matrixError.Message);
            var sharedError = await Assert.ThrowsAsync<BusinessRuleException>(() => service.StartSharedTsbAsync(
                shared.SampleId, new(media, incubator, AnalysedAt), shared.UnqualifiedId));
            Assert.Contains(Refusal(shared.A), sharedError.Message);
            Assert.Contains(Refusal(shared.B), sharedError.Message);
        }
        await using var check = fixture.CreateDbContext();
        Assert.False(await check.SampleLocations.AsNoTracking().AnyAsync(l => l.SampleId == matrix.SampleId && l.EnteredAt != null));
        Assert.False(await check.Incubations.AsNoTracking().AnyAsync(i => i.TestOrderId == shared.A.Id || i.TestOrderId == shared.B.Id));
    }

    [PostgresFact]
    public async Task Shared_TSB_start_keeps_the_refused_orders_existing_incubation()
    {
        var setup = await SeedAsync();
        var (mediaId, incubatorId) = await ArrangeSharedEnrichmentAsync(setup);
        int existingId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var existing = new Incubation
            {
                TestOrderId = setup.B.Id, StepName = "Broth Enrichment", StepNumber = 1, StartedAt = AnalysedAt,
                StartedByUserId = setup.UnqualifiedId, MediaId = mediaId, IncubatorEquipmentId = incubatorId,
            };
            arrange.Incubations.Add(existing);
            await arrange.SaveChangesAsync();
            existingId = existing.Id;
        }

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = setup.QualifiedId;
            var response = await TestServiceFactory.PathogenSession(act).StartSharedTsbAsync(setup.SampleId,
                new(mediaId, incubatorId, AnalysedAt), setup.QualifiedId);
            AssertSkip(setup.B, response.Skipped);
        }

        await using var check = fixture.CreateDbContext();
        await AssertOnlyQualifiedIncubationAsync(check, setup, mediaId, incubatorId);
        var kept = await check.Incubations.AsNoTracking().SingleAsync(i => i.TestOrderId == setup.B.Id);
        Assert.Equal(existingId, kept.Id);
        Assert.Equal(setup.UnqualifiedId, kept.StartedByUserId);
        var step = await check.WorkflowStepResults.AsNoTracking()
            .SingleAsync(r => r.TestOrderId == setup.A.Id || r.TestOrderId == setup.B.Id);
        Assert.Equal(setup.A.Id, step.TestOrderId);
        Assert.Equal(setup.QualifiedId, step.SubmittedByUserId);
    }

    [PostgresFact]
    public async Task Grouped_start_keeps_different_sop_warnings_on_their_own_orders()
    {
        var setup = await SeedAsync();
        var (mediaId, incubatorId) = await ArrangeSharedEnrichmentAsync(setup);
        int secondOrderId;
        var sop = $"UNFILED-{setup.SampleId}";
        await using (var arrange = fixture.CreateDbContext())
        {
            var source = await arrange.Samples.AsNoTracking().SingleAsync(s => s.Id == setup.SampleId);
            var item = await ItemSopResolverPostgresTests.AddItemAsync(arrange, sop);
            var sample = new Sample
            {
                ReferenceNumber = $"REC-SOP-{setup.SampleId}", Category = source.Category, ItemId = item.Id,
                CauseOfTestingId = source.CauseOfTestingId, ReceivedByUserId = setup.QualifiedId,
                PreparationStatus = SamplePreparationStatus.Ready, Status = SampleStatus.InTesting,
            };
            var second = new TestOrder
            {
                Sample = sample, SectionId = setup.A.SectionId, TestCode = setup.A.TestCode,
                AssignedAnalystId = setup.QualifiedId,
            };
            arrange.TestOrders.Add(second);
            arrange.SamplePreparations.Add(new SamplePreparation { Sample = sample, PreparedByUserId = setup.QualifiedId });
            await arrange.SaveChangesAsync();
            secondOrderId = second.Id;
        }
        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = setup.QualifiedId;
            var response = await TestServiceFactory.GroupedTestAction(act).ExecuteBatchSelectMediaAsync(
                new([setup.A.Id, secondOrderId], "Broth Enrichment", mediaId, incubatorId), setup.QualifiedId, RoleType.Analyst);
            Assert.Equal(2, response.SucceededCount);
            Assert.Empty(response.Skipped);
            Assert.Null(act.CurrentReason);
        }
        await using var check = fixture.CreateDbContext();
        await AssertOnlyQualifiedIncubationAsync(check, setup, mediaId, incubatorId);
        var secondIncubation = await check.Incubations.AsNoTracking().SingleAsync(i => i.TestOrderId == secondOrderId);
        var id = secondIncubation.Id.ToString();
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == nameof(Incubation) &&
            a.EntityId == id && a.Action == "Create");
        Assert.Equal(setup.QualifiedId, audit.UserId);
        Assert.Equal($"SOP not checked: {sop} was not found in Document Control.", audit.Reason);
    }

    [PostgresFact]
    public async Task Grouped_TSB_start_skips_an_unqualified_sibling_after_one_recording()
    {
        var setup = await SeedAsync();
        var (mediaId, incubatorId) = await ArrangeSharedEnrichmentAsync(setup);
        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = setup.QualifiedId;
            var response = await TestServiceFactory.GroupedTestAction(act).ExecuteBatchSelectMediaAsync(
                new([setup.A.Id, setup.B.Id], "Broth Enrichment", mediaId, incubatorId), setup.QualifiedId, RoleType.Analyst);
            Assert.Equal(setup.A.Id, Assert.Single(response.Succeeded).TestOrderId);
            var skip = Assert.Single(response.Skipped);
            Assert.Equal(setup.B.Id, skip.TestOrderId);
            Assert.Equal("Competency.NotQualified", skip.Code);
            Assert.Equal(Refusal(setup.B), skip.Reason);
            Assert.Null(act.CurrentReason);
        }

        await using var check = fixture.CreateDbContext();
        await AssertOnlyQualifiedIncubationAsync(check, setup, mediaId, incubatorId);
        Assert.False(await check.Incubations.AsNoTracking().AnyAsync(i => i.TestOrderId == setup.B.Id));
        Assert.False(await check.WorkflowStepResults.AsNoTracking().AnyAsync(r => r.TestOrderId == setup.B.Id));
    }

    private async Task<(int MediaId, int IncubatorId)> ArrangeSharedEnrichmentAsync(Setup setup)
    {
        await using var arrange = fixture.CreateDbContext();
        var orderIds = new[] { setup.A.Id, setup.B.Id };
        var orders = await arrange.TestOrders.Where(o => orderIds.Contains(o.Id)).ToListAsync();
        foreach (var order in orders)
        {
            order.CurrentStep = WorkflowStep.Waiting;
            order.Status = ApprovalStatus.Pending;
        }
        var sample = await arrange.Samples.SingleAsync(s => s.Id == setup.SampleId);
        sample.PreparationStatus = SamplePreparationStatus.Ready;
        arrange.SamplePreparations.Add(new SamplePreparation
        {
            SampleId = setup.SampleId, PreparedByUserId = setup.QualifiedId, PreparedAt = AnalysedAt,
        });
        var tag = setup.SampleId.ToString();
        var product = new MediaProduct { Code = $"C{tag}", Name = $"Competency test medium {tag}" };
        var media = new Media
        {
            Material = new Material
            {
                SectionId = setup.A.SectionId, BatchNumber = tag, MaterialName = product.Name, MediaProduct = product,
            },
            LotNumber = $"COMP-{tag}", IsReleasedForUse = true, ApprovalStatus = ApprovalGateStatus.Approved,
            PreparedAt = AnalysedAt, ExpiryDate = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PreparedByUserId = setup.QualifiedId,
        };
        var incubator = new Equipment
        {
            Type = EquipmentType.Incubator, SectionId = setup.A.SectionId, Code = $"COMP-E-{tag}",
            Name = $"Competency incubator {tag}", SetPointTemperature = 32,
        };
        arrange.AddRange(media, incubator);
        var codes = orders.Select(o => o.TestCode).ToList();
        var definitions = await arrange.TestDefinitions.Include(d => d.Steps)
            .Where(d => codes.Contains(d.Code)).ToListAsync();
        foreach (var definition in definitions)
        {
            definition.WorkflowType = WorkflowType.Observation;
            var step = Assert.Single(definition.Steps);
            step.StepName = "Broth Enrichment";
            step.StepType = StepType.BrothEnrichment;
            step.StepMedia.Add(new TestWorkflowStepMedia
            {
                MediaProduct = product, IsRequired = true,
                IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 30, TempMax = 35,
            });
        }
        await arrange.SaveChangesAsync();
        return (media.Id, incubator.Id);
    }

    private static async Task AssertOnlyQualifiedIncubationAsync(
        MicroLIMS.Persistence.DbContext.MicroLimsDbContext check, Setup setup, int mediaId, int incubatorId)
    {
        var row = await check.Incubations.AsNoTracking().SingleAsync(i => i.TestOrderId == setup.A.Id);
        Assert.Equal(mediaId, row.MediaId);
        Assert.Equal(incubatorId, row.IncubatorEquipmentId);
        Assert.Equal(setup.QualifiedId, row.StartedByUserId);
        Assert.Equal(WorkflowStep.Incubating, await check.TestOrders.AsNoTracking().Where(o => o.Id == setup.A.Id)
            .Select(o => o.CurrentStep).SingleAsync());
        var id = row.Id.ToString();
        var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(Incubation) && a.EntityId == id && a.Action == "Create");
        Assert.Equal(setup.QualifiedId, audit.UserId);
        Assert.Equal("SOP not checked: the item has no SOP number.", audit.Reason);
    }

    private static void AssertSkip(TestOrder expected, List<CompetencySkipDto>? skips)
    {
        var skip = Assert.Single(Assert.IsType<List<CompetencySkipDto>>(skips));
        Assert.Equal(expected.Id, skip.TestOrderId);
        Assert.Equal(expected.TestCode, skip.TestCode);
        Assert.Equal("Competency.NotQualified", skip.Code);
        Assert.Equal(Refusal(expected), skip.Reason);
    }

    private static string Refusal(TestOrder order) => $"You are not qualified for {order.TestCode}.";

    [PostgresFact]
    public async Task Stage_recorders_check_section_scope_before_competency()
    {
        await PostgresCases.RunAsync(new[] { WorkflowType.Dissolution, WorkflowType.Disintegration, WorkflowType.WeightVariation }, async type =>
        {
            var setup = await SeedAsync(type);
            await using (var arrange = fixture.CreateDbContext())
            {
                arrange.UserOrgMemberships.RemoveRange(await arrange.UserOrgMemberships
                    .Where(m => m.UserId == setup.UnqualifiedId).ToListAsync());
                await arrange.SaveChangesAsync();
                setup = setup with { Audits = await arrange.AuditLogs.AsNoTracking().CountAsync() };
            }
            await using (var act = fixture.CreateDbContext())
            {
                var engine = TestServiceFactory.TestWorkflow(act);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => type switch
                {
                    WorkflowType.Dissolution => engine.RecordDissolutionStageAsync(setup.A.Id, new([1], Password), setup.UnqualifiedId),
                    WorkflowType.Disintegration => engine.RecordDisintegrationStageAsync(setup.A.Id, new([1], Password), setup.UnqualifiedId),
                    _ => engine.RecordWeightVariationStageAsync(setup.A.Id, new([new(1)], Password), setup.UnqualifiedId),
                });
            }
            await AssertNoAnalysisAsync(setup);
        });
    }

    [PostgresFact]
    public async Task Analysis_and_stage_recorders_refuse_before_signature_or_persistence()
    {
        var cases = new[]
        {
            WorkflowType.Measurement, WorkflowType.Gravimetric, WorkflowType.Qualitative,
            WorkflowType.Titration, WorkflowType.UvSpectrophotometry, WorkflowType.Dissolution,
            WorkflowType.Disintegration, WorkflowType.WeightVariation,
        };
        await PostgresCases.RunAsync(cases, async type =>
        {
            var setup = await SeedAsync(type);
            var orderId = setup.A.Id;
            var actor = setup.UnqualifiedId;
            var spec = setup.SpecificationId;
            await using (var act = fixture.CreateDbContext())
            {
                act.CurrentUserId = actor;
                var engine = TestServiceFactory.TestWorkflow(act);
                var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => type switch
                {
                    WorkflowType.Measurement => engine.RecordMeasurementResultAsync(
                        orderId, new(AnalysedAt, null, [new(spec, [5m])], "invalid password"), actor),
                    WorkflowType.Gravimetric => engine.RecordGravimetricResultAsync(
                        orderId, new(AnalysedAt, null, [], [new(spec, [])], Password), actor),
                    WorkflowType.Qualitative => engine.RecordQualitativeResultAsync(
                        orderId, new(AnalysedAt, null, [new(spec, true, null)], Password), actor),
                    WorkflowType.Titration => engine.RecordTitrationResultAsync(orderId,
                        new(AnalysedAt, null, 0, null, null, null, null, null, null, [spec], [], Password, null), actor),
                    WorkflowType.UvSpectrophotometry => engine.RecordUvResultAsync(orderId,
                        new(AnalysedAt, setup.EquipmentId, null, null, null, [new(spec, 254, null, [])], Password), actor),
                    WorkflowType.Dissolution => engine.RecordDissolutionResultAsync(orderId,
                        new(AnalysedAt, null, null, 100, null, [1, 1, 1, 1, 1, 1], Password), actor),
                    WorkflowType.Disintegration => engine.RecordDisintegrationResultAsync(orderId,
                        new(AnalysedAt, null, null, [1], Password), actor),
                    WorkflowType.WeightVariation => engine.RecordWeightVariationResultAsync(orderId,
                        new(AnalysedAt, null, null, [new(1)], Password), actor),
                    _ => throw new ArgumentOutOfRangeException(nameof(type)),
                });
                Assert.Equal("Competency.NotQualified", refused.Code);
                Assert.Equal(Refusal(setup.A), refused.Message);

                Func<Task>? stage = type switch
                {
                    WorkflowType.Dissolution => () => engine.RecordDissolutionStageAsync(orderId, new([1], Password), actor),
                    WorkflowType.Disintegration => () => engine.RecordDisintegrationStageAsync(orderId, new([1], Password), actor),
                    WorkflowType.WeightVariation => () => engine.RecordWeightVariationStageAsync(orderId, new([new(1)], Password), actor),
                    _ => null,
                };
                if (stage is not null)
                {
                    var stageRefused = await Assert.ThrowsAsync<BusinessRuleException>(stage);
                    Assert.Equal("Competency.NotQualified", stageRefused.Code);
                    Assert.Equal(Refusal(setup.A), stageRefused.Message);
                }
            }

            await AssertNoAnalysisAsync(setup);
        });
    }

    [PostgresFact]
    public async Task Instrument_submissions_refuse_the_actor_before_signature_or_persistence()
    {
        await PostgresCases.RunAsync(new[] { WorkflowType.HplcMethodAssay, WorkflowType.IcpMethodAssay }, async workflow =>
        {
            var setup = await SeedAsync(workflow);
            var actor = setup.UnqualifiedId;
            var hplc = workflow == WorkflowType.HplcMethodAssay;
            int runSampleId;
            await using (var arrange = fixture.CreateDbContext())
            {
                var tag = setup.A.TestCode;
                var equipment = new Equipment
                {
                    SectionId = setup.A.SectionId, Code = tag, Name = "Assay instrument",
                    Type = hplc ? EquipmentType.Hplc : EquipmentType.IcpOes,
                };
                if (hplc)
                {
                    var method = new HplcMethod
                    {
                        SectionId = setup.A.SectionId, Abbreviation = tag, Name = "Assay method",
                        EffectiveDate = AnalysedAt,
                        DiluentSolution = new SolutionMaster
                        {
                            SectionId = setup.A.SectionId, Type = SolutionType.Diluent, Name = "Assay diluent",
                            CreatedByUserId = setup.QualifiedId, LastModifiedByUserId = setup.QualifiedId,
                            CreatedAt = AnalysedAt, LastModifiedAt = AnalysedAt,
                        },
                    };
                    var sample = new HplcRunSample
                    {
                        TestOrderId = setup.A.Id, AssignedAt = AnalysedAt, AssignedByUserId = setup.QualifiedId,
                        HplcRun = new HplcRun
                        {
                            Code = tag, SectionId = setup.A.SectionId, Equipment = equipment,
                            HplcMethod = method, StartedAt = AnalysedAt, AnalystUserId = setup.QualifiedId,
                            MethodSnapshotJson = System.Text.Json.JsonSerializer.Serialize(
                                HplcMethodResponse.From(method), SnapshotJson.Options),
                            ChromatographyColumn = new ChromatographyColumn
                            {
                                SectionId = setup.A.SectionId, Code = tag, Name = "Assay column",
                                CreatedByUserId = setup.QualifiedId, LastModifiedByUserId = setup.QualifiedId,
                            },
                        },
                    };
                    arrange.HplcRunSamples.Add(sample);
                    await arrange.SaveChangesAsync();
                    runSampleId = sample.Id;
                }
                else
                {
                    var sample = new IcpRunSample
                    {
                        TestOrderId = setup.A.Id, AssignedAt = AnalysedAt, AssignedByUserId = setup.QualifiedId,
                        IcpRun = new IcpRun
                        {
                            Code = tag, SectionId = setup.A.SectionId, Equipment = equipment,
                            StartedAt = AnalysedAt, AnalystUserId = setup.QualifiedId,
                        },
                    };
                    arrange.IcpRunSamples.Add(sample);
                    await arrange.SaveChangesAsync();
                    runSampleId = sample.Id;
                }
                setup = setup with { Audits = await arrange.AuditLogs.AsNoTracking().CountAsync() };
            }

            await using (var act = fixture.CreateDbContext())
            {
                var engine = TestServiceFactory.TestWorkflow(act);
                var refusal = await Assert.ThrowsAsync<BusinessRuleException>(() => hplc
                    ? engine.SubmitHplcMethodAssayAsync(runSampleId, Password, null, actor)
                    : engine.SubmitIcpMethodAssayAsync(runSampleId, Password, null, actor));
                Assert.Equal("Competency.NotQualified", refusal.Code);
                Assert.Equal(Refusal(setup.A), refusal.Message);
            }
            await AssertNoAnalysisAsync(setup);
        });
    }

    [PostgresFact]
    public async Task Outsourced_result_refuses_the_actor_before_signature_or_persistence()
    {
        var setup = await SeedAsync(WorkflowType.Measurement, outsourced: true);
        await using (var act = fixture.CreateDbContext())
        {
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => TestServiceFactory.TestWorkflow(act)
                .RecordOutsourcedResultAsync(setup.A.Id,
                    new(AnalysedAt, "External report", 0, [new(setup.SpecificationId, 5, null, null)], Password),
                    setup.UnqualifiedId));
            Assert.Equal("Competency.NotQualified", refused.Code);
            Assert.Equal(Refusal(setup.A), refused.Message);
        }
        await AssertNoAnalysisAsync(setup);
    }

    [PostgresFact]
    public async Task Measurement_signs_and_audits_the_actor_with_gate_on_or_off()
    {
        await PostgresCases.RunAsync(new[] { true, false }, async enabled =>
        {
            var setup = await SeedAsync(WorkflowType.Measurement, gateEnabled: enabled);
            var actor = enabled ? setup.QualifiedId : setup.UnqualifiedId;
            await using (var act = fixture.CreateDbContext())
            {
                Assert.Null(act.CurrentReason);
                await TestServiceFactory.TestWorkflow(act).RecordMeasurementResultAsync(setup.A.Id,
                    new(AnalysedAt, null, [new(setup.SpecificationId, [5m])], Password), actor);
                Assert.Null(act.CurrentReason);
            }

            await using var check = fixture.CreateDbContext();
            var analysis = await check.TestAnalyses.AsNoTracking().Include(a => a.ParameterResults)
                .SingleAsync(a => a.TestOrderId == setup.A.Id);
            Assert.Equal(actor, analysis.EnteredByUserId);
            Assert.Equal(AnalysedAt, analysis.AnalysedAt);
            Assert.Equal(5m, Assert.Single(analysis.ParameterResults).ReportedValue);
            var signature = await check.ElectronicSignatures.AsNoTracking()
                .SingleAsync(s => s.EntityType == nameof(TestOrder) && s.EntityId == setup.A.Id);
            Assert.Equal(actor, signature.UserId);
            Assert.Equal(SignatureMeaning.ResultRecorded, signature.MeaningOfSignature);
            var order = await check.TestOrders.AsNoTracking().SingleAsync(o => o.Id == setup.A.Id);
            Assert.Equal(WorkflowStep.Ready, order.CurrentStep);
            Assert.NotEqual(actor, order.AssignedAnalystId);
            var id = analysis.Id.ToString();
            var audit = await check.AuditLogs.AsNoTracking().SingleAsync(a =>
                a.EntityName == nameof(TestAnalysis) && a.EntityId == id && a.Action == "Create");
            Assert.Equal(actor, audit.UserId);
            Assert.Equal(enabled ? "SOP not checked: the item has no SOP number." : null, audit.Reason);
            Assert.Contains(nameof(TestAnalysis.EnteredByUserId), audit.NewValue);
        });
    }

    private async Task AssertNoAnalysisAsync(Setup setup)
    {
        await using var check = fixture.CreateDbContext();
        Assert.False(await check.TestAnalyses.AsNoTracking().AnyAsync(r => r.TestOrderId == setup.A.Id));
        Assert.False(await check.Results.AsNoTracking().AnyAsync(r => r.TestOrderId == setup.A.Id));
        Assert.False(await check.Incubations.AsNoTracking().AnyAsync(i => i.TestOrderId == setup.A.Id));
        Assert.Equal(setup.A.Status, await check.TestOrders.AsNoTracking().Where(o => o.Id == setup.A.Id).Select(o => o.Status).SingleAsync());
        Assert.Equal(setup.Signatures, await check.ElectronicSignatures.AsNoTracking().CountAsync());
        Assert.Equal(setup.Audits, await check.AuditLogs.AsNoTracking().CountAsync());
    }

    private static int LocationId(TestOrder order) => order.Sample!.Locations.Single(l => l.TestOrderId == order.Id).Id;

    private static MatrixCellInput Cell(TestOrder order) =>
        new(LocationId(order), order.TestCode, "5", "5 CFU", 5, "Quantitative");

    private sealed record Setup(int SampleId, TestOrder A, TestOrder B, int QualifiedId, int UnqualifiedId,
        int Audits, int Signatures, int SpecificationId, int? EquipmentId);

    private async Task<Setup> SeedAsync(WorkflowType workflow = WorkflowType.CountTest, bool gateEnabled = true, bool outsourced = false)
    {
        await using var db = fixture.CreateDbContext();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var roleId = await db.Roles.Where(role => role.Type == RoleType.Analyst)
            .OrderBy(role => role.Id).Select(role => role.Id).FirstAsync();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);
        User Analyst(string label) => new()
        {
            FullName = label, Username = $"{label}-{tag}", PasswordHash = passwordHash, RoleId = roleId, IsActive = true,
        };
        var qualified = Analyst("Qualified");
        var unqualified = Analyst("Unqualified");
        var section = new DocumentSection { Code = $"COMP-{tag}", Name = $"Competency lab {tag}", DepartmentId = fixture.SeededDepartmentId };
        var cause = new CauseOfTesting { Name = $"Routine-{tag}", IsActive = true };
        db.AddRange(section, cause, qualified, unqualified);
        await db.SaveChangesAsync();
        foreach (var userId in new[] { qualified.Id, unqualified.Id, fixture.SeededControllerUserId })
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = userId, SectionId = section.Id, DepartmentId = fixture.SeededDepartmentId });
        db.CompetencySectionSettings.Add(new CompetencySectionSetting { SectionId = section.Id, GateEnabled = gateEnabled });
        TestDefinition Definition(string prefix) => new()
        {
            Code = $"{prefix}-{tag}", DisplayName = $"Test {prefix}", SectionId = section.Id,
            WorkflowType = workflow, ReplicateCount = 1, EvaluationBasis = MeasurementEvaluationBasis.Mean,
            Steps = [new TestWorkflowStep { StepName = "Read", StepOrder = 1, StepType = StepType.PlateCount }],
        };
        var first = Definition("A");
        var second = Definition("B");
        db.TestDefinitions.AddRange(first, second);
        var item = await ItemSopResolverPostgresTests.AddItemAsync(db, null);
        var spec = new Specification
        {
            ItemId = item.Id, TestCode = first.Code, ParameterName = "Reading", LowerLimit = 1, UpperLimit = 10,
            LimitType = workflow switch
            {
                WorkflowType.Dissolution => LimitType.DissolutionQ,
                WorkflowType.Disintegration => LimitType.DisintegrationTime,
                WorkflowType.WeightVariation => LimitType.WeightVariation,
                _ => LimitType.Range,
            },
        };
        db.Specifications.Add(spec);
        Equipment? equipment = null;
        if (workflow == WorkflowType.UvSpectrophotometry)
        {
            var fp = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
                ?? new DocumentSection { Code = "FP", Name = "Finished Products", DepartmentId = fixture.SeededDepartmentId };
            equipment = new Equipment { Section = fp, Code = $"UV-{tag}", Name = "UV instrument", Type = EquipmentType.UvVis };
            db.Equipment.Add(equipment);
        }
        var sample = new Sample
        {
            ReferenceNumber = $"REC-{tag}", Category = SampleCategory.FinishedProduct, ItemId = item.Id,
            ReceivedByUserId = qualified.Id, CauseOfTestingId = cause.Id, Status = SampleStatus.InTesting,
        };
        TestOrder AddOrder(string code)
        {
            var order = new TestOrder
            {
                Sample = sample, TestCode = code, SectionId = section.Id, AssignedAnalystId = qualified.Id,
                Status = ApprovalStatus.InProgress,
                CurrentStep = workflow == WorkflowType.CountTest ? WorkflowStep.Ready : WorkflowStep.Running,
            };
            var location = new SampleLocation { Sample = sample, TestOrder = order, LocationType = LocationType.Room };
            db.SampleLocations.Add(location);
            return order;
        }
        var a = AddOrder(first.Code);
        var b = AddOrder(second.Code);
        if (outsourced)
            a.OutsourcedToLab = "External lab";
        if (workflow == WorkflowType.Measurement && !outsourced)
            a.AssignedAnalystId = gateEnabled ? unqualified.Id : qualified.Id;
        await db.SaveChangesAsync();

        var id = await fixture.CreateSequenceHelper(db).ReserveIdAsync<AnalystQualification>();
        db.AnalystQualifications.Add(new AnalystQualification
        {
            Id = id, UserId = qualified.Id, TestDefinitionId = first.Id,
            Basis = QualificationBasis.ObservedPractice, Evidence = "Observed the method",
            QualifiedOn = new DateOnly(2026, 1, 1), SignedByUserId = fixture.SeededControllerUserId,
            Signature = new ElectronicSignature
            {
                UserId = fixture.SeededControllerUserId, EntityType = nameof(AnalystQualification), EntityId = id,
                MeaningOfSignature = SignatureMeaning.AnalystQualified,
            },
        });
        await db.SaveChangesAsync();
        return new(sample.Id, a, b, qualified.Id, unqualified.Id, await db.AuditLogs.AsNoTracking().CountAsync(),
            await db.ElectronicSignatures.AsNoTracking().CountAsync(), spec.Id, equipment?.Id);
    }

    [PostgresFact]
    public async Task Completing_session_marks_only_active_orders_and_skips_cancelled_and_superseded()
    {
        var setup = await SeedAsync();
        int cancelledOrderId;

        await using (var arrange = fixture.CreateDbContext())
        {
            var point = new WaterSamplingPoint { Code = $"C217-{Guid.NewGuid():N}"[..16], Location = "Shared point" };
            var locations = await arrange.SampleLocations.Where(l => l.SampleId == setup.SampleId).ToListAsync();
            foreach (var location in locations)
            {
                location.WaterSamplingPoint = point;
                if (location.TestOrderId == setup.A.Id)
                {
                    location.CFUResult = 5m;
                    location.CalculatedResult = 5m;
                    location.ReportedResult = "5 CFU";
                    location.Status = ResultStatus.WithinLimits;
                    location.EnteredByUserId = setup.QualifiedId;
                }
            }

            var supersededOrder = await arrange.TestOrders.SingleAsync(o => o.Id == setup.B.Id);
            supersededOrder.IsSuperseded = true;

            var cancelledOrder = new TestOrder
            {
                SampleId = setup.SampleId,
                TestCode = $"CAN-{Guid.NewGuid():N}"[..12],
                SectionId = setup.A.SectionId,
                AssignedAnalystId = setup.QualifiedId,
                Status = ApprovalStatus.Cancelled,
                CurrentStep = WorkflowStep.Running
            };
            arrange.TestOrders.Add(cancelledOrder);
            await arrange.SaveChangesAsync();
            cancelledOrderId = cancelledOrder.Id;
        }

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = setup.QualifiedId;
            await TestServiceFactory.PathogenSession(act).CompleteSessionAsync(setup.SampleId, setup.QualifiedId);
        }

        await using var check = fixture.CreateDbContext();
        var orders = await check.TestOrders.AsNoTracking().Where(o => o.SampleId == setup.SampleId).ToDictionaryAsync(o => o.Id);
        Assert.Equal(ApprovalStatus.ResultEntered, orders[setup.A.Id].Status);
        Assert.Equal(WorkflowStep.Ready, orders[setup.A.Id].CurrentStep);
        Assert.Equal(ApprovalStatus.Cancelled, orders[cancelledOrderId].Status);
        Assert.Equal(ApprovalStatus.InProgress, orders[setup.B.Id].Status);

        var activeAudit = await check.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == nameof(TestOrder) && a.EntityId == setup.A.Id.ToString() && a.Action == "Update");
        Assert.Equal(setup.QualifiedId, activeAudit.UserId);
        using var newOrder = System.Text.Json.JsonDocument.Parse(Assert.IsType<string>(activeAudit.NewValue));
        Assert.Equal((int)ApprovalStatus.ResultEntered,
            newOrder.RootElement.GetProperty(nameof(TestOrder.Status)).GetInt32());
        Assert.False(await check.AuditLogs.AsNoTracking().AnyAsync(a =>
            a.EntityName == nameof(TestOrder) &&
            a.UserId == setup.QualifiedId &&
            (a.EntityId == cancelledOrderId.ToString() || a.EntityId == setup.B.Id.ToString()) &&
            a.Action == "Update"));
    }
}
