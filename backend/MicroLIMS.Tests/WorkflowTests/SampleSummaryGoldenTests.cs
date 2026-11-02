using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Characterization (golden-output) test for SampleSummaryService.GetSummaryAsync.
// One fixed dataset exercises every branch of the method; the serialized DTO
// is compared with a checked-in golden file, so a refactor of the service can
// be proven behaviour-neutral. Regenerate with UPDATE_GOLDEN=1 (a missing
// golden file is also written instead of failing).
public class SampleSummaryGoldenTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    // Some DTO text is formatted with the current culture (e.g. the lock reason's date),
    // so the golden is pinned to the invariant culture whatever machine runs it.
    private sealed class InvariantCulture : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentCulture;
        public InvariantCulture() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        public void Dispose() => CultureInfo.CurrentCulture = _previous;
    }

    private static DateTime D(int day, int hour = 0, int minute = 0) => new(2026, 3, day, hour, minute, 0, DateTimeKind.Utc);

    private static DbContextOptions<MicroLimsDbContext> NewOptions() =>
        new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    // A second context over the same store reads it back, as a request would
    // (nothing seeded is left tracked, so no navigation fix-up from the seed).
    private static SampleSummaryService Service(MicroLimsDbContext db) =>
        new(db, new PdfGenerator(), new MicroLIMS.Infrastructure.Word.WordGenerator(),
            TestServiceFactory.ReviewGate(db), new FixedTimeProvider(new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero)));

    private static MicroLimsDbContext Seed(DbContextOptions<MicroLimsDbContext> options)
    {
        using var db = new MicroLimsDbContext(options);

        db.Users.AddRange(
            new User { Id = 1, FullName = "Alice Analyst", Username = "alice", PasswordHash = "x" },
            new User { Id = 2, FullName = "Bob Reviewer", Username = "bob", PasswordHash = "x" },
            new User { Id = 3, FullName = "Carol Approver", Username = "carol", PasswordHash = "x" });

        db.DocumentDepartments.Add(new DocumentDepartment { Id = 1, Code = "QC", Name = "Quality Control" });
        db.DocumentSections.AddRange(
            new DocumentSection { Id = 1, Code = "MICRO", Name = "Microbiology", DepartmentId = 1 },
            new DocumentSection { Id = 2, Code = "PCHEM", Name = "Physicochemical", DepartmentId = 1 });

        db.Items.Add(new Item { Id = 1, Name = "Talc Powder", Code = "TP-1", Category = SampleCategory.FinishedProduct, SopNumber = "SOP-1" });
        db.CausesOfTesting.Add(new CauseOfTesting { Id = 1, Name = "Routine" });
        db.Departments.Add(new Department { Id = 1, Name = "Filling Hall", Class = "Grade C", TestingFrequency = "Monthly" });
        db.Machines.Add(new Machine { Id = 1, Name = "Mixer M-1" });
        db.MachineParts.Add(new MachinePart { Id = 1, MachineId = 1, Name = "Paddle" });
        db.MachinePartConfigurations.Add(new MachinePartConfiguration { Id = 1, MachinePartId = 1, TestType = "Swab", TestCode = "TAMC", AlertLimit = "5", ActionLimit = "10", SpecLimit = "20", Unit = "CFU/swab" });
        db.Rooms.Add(new Room { Id = 1, Name = "Room 101", DepartmentId = 1, GradeClassification = "Grade C" });
        db.RoomTestConfigurations.Add(new RoomTestConfiguration { Id = 1, RoomId = 1, TestType = "Settle", TestCode = "EM_TAMC", AlertLimit = "5", ActionLimit = "10", SpecLimit = "20", Unit = "CFU/plate" });
        db.WaterSamplingPoints.AddRange(
            new WaterSamplingPoint { Id = 1, Code = "WTU-1", Location = "Block A", TestingFrequency = "Weekly" },
            new WaterSamplingPoint { Id = 2, Code = "WTU-2", Location = "Block B", TestingFrequency = "Weekly" });
        db.SamplingConfigurations.Add(new SamplingConfiguration { Id = 1, WaterSamplingPointId = 2, TestCode = "WATER_TAMC", AlertLimit = "50", ActionLimit = "100", SpecLimit = "200", Unit = "CFU/mL" });

        db.Materials.Add(new Material { Id = 1, MediaProductId = 1, SectionId = 1, MaterialType = MaterialType.DehydratedMedia, MaterialName = "Tryptic Soy Agar", ReceivingDate = D(1), Unit = MaterialUnit.Gram });
        db.Media.Add(new Media { Id = 1, MaterialId = 1, LotNumber = "TSA/001/26", PreparedAt = D(1), ExpiryDate = D(28) });
        db.Equipment.AddRange(
            new Equipment { Id = 1, Name = "Incubator 30-35", Code = "INC-001", Type = EquipmentType.Incubator, SectionId = 1 },
            new Equipment { Id = 2, Name = "pH Meter", Code = "PH-001", Type = EquipmentType.PhMeter, SectionId = 2 });

        // Test definitions: TAMC (count), ECOLI (pathogen with a TSB step), PH (measurement), TYMC (retest).
        db.TestDefinitions.AddRange(
            new TestDefinition { Id = 1, Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", SectionId = 1, WorkflowType = WorkflowType.CountTest },
            new TestDefinition { Id = 2, Code = "ECOLI", DisplayName = "E. coli", SectionId = 1, WorkflowType = WorkflowType.Observation },
            new TestDefinition { Id = 3, Code = "PH", DisplayName = "pH", SectionId = 2, WorkflowType = WorkflowType.Measurement },
            new TestDefinition { Id = 4, Code = "TYMC", DisplayName = "Total Yeast and Mold Count", SectionId = 1, WorkflowType = WorkflowType.CountTest });
        db.TestWorkflowSteps.AddRange(
            new TestWorkflowStep { Id = 1, TestDefinitionId = 2, StepOrder = 1, StepName = "TSB Enrichment", StepType = StepType.BrothEnrichment, IncubationMinHours = 18, IncubationMaxHours = 24, TemperatureMin = 30, TemperatureMax = 35 },
            new TestWorkflowStep { Id = 2, TestDefinitionId = 2, StepOrder = 2, StepName = "Selective Plating", StepType = StepType.SelectivePlating, IncubationMinHours = 24, IncubationMaxHours = 48, TemperatureMin = 30, TemperatureMax = 35, IsFinalStep = true },
            new TestWorkflowStep { Id = 3, TestDefinitionId = 1, StepOrder = 1, StepName = "Plate Count", StepType = StepType.PlateCount, IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35, IsFinalStep = true });

        // Specifications (TAMC has three parameters, ECOLI a presence/absence row).
        db.Specifications.AddRange(
            new Specification { Id = 1, ItemId = 1, TestCode = "TAMC", ParameterName = "Bacteria", DisplayOrder = 1, LimitType = LimitType.NotMoreThan, SpecLimit = "100", Unit = "CFU/g" },
            new Specification { Id = 2, ItemId = 1, TestCode = "TAMC", ParameterName = "Fungi", DisplayOrder = 2, LimitType = LimitType.CountTiered, SpecLimit = "10-50", Unit = "CFU/g" },
            new Specification { Id = 3, ItemId = 1, TestCode = "TAMC", ParameterName = "Total", DisplayOrder = 3, LimitType = LimitType.Qualitative, ExpectedResultText = "Conforms" },
            new Specification { Id = 4, ItemId = 1, TestCode = "ECOLI", ParameterName = "E. coli", LimitType = LimitType.PresenceAbsence, ExpectedState = ExpectedPresence.Absence, SampleQuantity = 1, SampleQuantityUnit = "g" },
            new Specification { Id = 5, ItemId = 1, TestCode = "PH", ParameterName = "pH", LimitType = LimitType.Range, SpecLimit = "5.0-7.0" });

        // ---- Main sample (id 1): finished product, two sections, with a retest chain.
        db.Samples.Add(new Sample
        {
            Id = 1, ReferenceNumber = "S-2026-0001", Category = SampleCategory.FinishedProduct, ItemId = 1, CauseOfTestingId = 1,
            ProductionStage = "Bulk", BatchNumber = "B-100", ControlNumber = "CTRL-1", SampleQuantity = "100 g", SampledBy = "Dan Sampler",
            MfgDate = D(1), ExpDate = D(28), ReceivedByUserId = 1, ReceivedAt = D(2, 9),
            Status = SampleStatus.Approved, StorageCondition = "Refrigerator", StorageTimeHours = 6,
            ReviewedByUserId = 2, ReviewedAt = D(10, 9), ApprovedByUserId = 3, ApprovedAt = D(11, 9),
            ApprovalDecision = ApprovalDecision.Approve, CertificateRemarks = "Released.", OosGroupCode = "OOS-0001"
        });
        db.SamplePreparations.Add(new SamplePreparation
        {
            Id = 1, SampleId = 1, Amount = 10m, Technique = "Filtration", FiltrationVolume = 100m, WashingVolume = 300m,
            Diluent = "Buffered peptone", Neutralizer = "Polysorbate 80", PreparedByUserId = 2, PreparedAt = D(2, 10)
        });
        db.SampleSectionSignoffs.AddRange(
            new SampleSectionSignoff
            {
                Id = 1, SampleId = 1, SectionId = 1, Status = SectionSignoffStatus.Approved,
                ReviewedByUserId = 2, ReviewedAt = D(10, 9), ApprovedByUserId = 3, ApprovedAt = D(11, 9),
                ApprovalDecision = ApprovalDecision.Approve, CertificateRemarks = "Micro released."
            },
            new SampleSectionSignoff
            {
                Id = 2, SampleId = 1, SectionId = 2, Status = SectionSignoffStatus.Cancelled,
                ClosedByUserId = 2, ClosedAt = D(9, 15), CloseReason = "Closed with the sample."
            });
        db.ElectronicSignatures.AddRange(
            new ElectronicSignature { Id = 1, UserId = 2, UserFullNameSnapshot = "Bob Reviewer", UsernameSnapshot = "bob", RoleSnapshot = "Reviewer", MeaningOfSignature = SignatureMeaning.Reviewed, EntityType = "Sample", EntityId = 1, SignedAt = D(10, 9), Comment = "Looks fine" },
            new ElectronicSignature { Id = 2, UserId = 3, UserFullNameSnapshot = "Carol Approver", UsernameSnapshot = "carol", RoleSnapshot = "Approver", MeaningOfSignature = SignatureMeaning.Approved, EntityType = "Sample", EntityId = 1, SignedAt = D(11, 9) },
            new ElectronicSignature { Id = 3, UserId = 1, UserFullNameSnapshot = "Alice Analyst", UsernameSnapshot = "alice", RoleSnapshot = "Analyst", MeaningOfSignature = SignatureMeaning.ResultRecorded, EntityType = "TestOrder", EntityId = 3, SignedAt = D(5, 9) });
        db.ReviewWorkflowEvents.AddRange(
            new ReviewWorkflowEvent { Id = 1, EntityType = "Sample", EntityId = 1, EventType = ReviewWorkflowEventType.SubmittedForReview, PerformedByUserId = 1, PerformedByNameSnapshot = "Alice Analyst", Timestamp = D(9, 8) },
            new ReviewWorkflowEvent { Id = 2, EntityType = "Sample", EntityId = 1, EventType = ReviewWorkflowEventType.ReviewCompleted, PerformedByUserId = 2, PerformedByNameSnapshot = "Bob Reviewer", Timestamp = D(10, 9), Comment = "Looks fine" },
            new ReviewWorkflowEvent { Id = 3, EntityType = "Sample", EntityId = 1, EventType = ReviewWorkflowEventType.ApprovalDecisionMade, PerformedByUserId = 3, PerformedByNameSnapshot = "Carol Approver", Timestamp = D(11, 9), Decision = ApprovalDecision.Approve },
            new ReviewWorkflowEvent { Id = 4, EntityType = "Media", EntityId = 1, EventType = ReviewWorkflowEventType.SubmittedForReview, PerformedByUserId = 1, PerformedByNameSnapshot = "Alice Analyst", Timestamp = D(1) });

        // Retest chain: sample 1 (TYMC superseded) -> sample 2 (TYMC superseded, own OOS group) -> sample 3 (TYMC live).
        db.Samples.AddRange(
            new Sample { Id = 2, ReferenceNumber = "S-2026-0001-R1", Category = SampleCategory.FinishedProduct, ItemId = 1, CauseOfTestingId = 1, ControlNumber = "CTRL-1R1", SampledBy = "Dan Sampler", ReceivedByUserId = 1, ReceivedAt = D(12, 9), Status = SampleStatus.Approved, OriginSampleId = 1, OosGroupCode = "OOS-0001" },
            new Sample { Id = 3, ReferenceNumber = "S-2026-0001-R2", Category = SampleCategory.FinishedProduct, ItemId = 1, CauseOfTestingId = 1, ControlNumber = "CTRL-1R2", SampledBy = "Dan Sampler", ReceivedByUserId = 1, ReceivedAt = D(14, 9), Status = SampleStatus.UnderReview, OriginSampleId = 2 });

        db.TestOrders.AddRange(
            // TO1 - count test, staged incubations, readings.
            new TestOrder { Id = 1, SampleId = 1, TestCode = "TAMC", SectionId = 1, Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved, AssignedAnalystId = 1 },
            // TO2 - pathogen test with per-location observations.
            new TestOrder { Id = 2, SampleId = 1, TestCode = "ECOLI", SectionId = 1, Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Reviewed },
            // TO3 - measurement test (section 2): analysis, parameter results, a Result.
            new TestOrder { Id = 3, SampleId = 1, TestCode = "PH", SectionId = 2, Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved, AssignedAnalystId = 2 },
            // TO4 - completed incubation resolved through a PathogenObservation; biochemical result.
            new TestOrder { Id = 4, SampleId = 1, TestCode = "SALM", SectionId = 1, Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Ready },
            // TO5 - completed incubation resolved through the last WorkflowHistory row.
            new TestOrder { Id = 5, SampleId = 1, TestCode = "PSEUD", SectionId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating },
            // TO6 - completed incubation with no recorded actor at all.
            new TestOrder { Id = 6, SampleId = 1, TestCode = "BCC", SectionId = 2, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running },
            // TO7 - superseded on sample 1, retested on sample 2/3.
            new TestOrder { Id = 7, SampleId = 1, TestCode = "TYMC", SectionId = 1, Status = ApprovalStatus.RetestRequested, CurrentStep = WorkflowStep.Incubating, IsSuperseded = true, CancelledAtStep = WorkflowStep.Incubating, CancelledAtStage = 1 },
            new TestOrder { Id = 8, SampleId = 2, TestCode = "TYMC", SectionId = 1, Status = ApprovalStatus.RetestRequested, CurrentStep = WorkflowStep.Ready, IsSuperseded = true, AssignedAnalystId = 3 },
            new TestOrder { Id = 9, SampleId = 3, TestCode = "TYMC", SectionId = 1, Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready },
            // Not superseded on the child, and a code sample 1 never superseded: must not be pulled through.
            new TestOrder { Id = 10, SampleId = 2, TestCode = "TAMC", SectionId = 1, Status = ApprovalStatus.Approved, CurrentStep = WorkflowStep.Approved });

        // ---- Incubations.
        db.Incubations.AddRange(
            // TO1: stage 1 -> stage 2 linked by ParentIncubationId; stage 1 completed, stage 2 not.
            new Incubation { Id = 1, TestOrderId = 1, StepNumber = 1, StepName = "Plate Count", StageNumber = 1, StartedAt = D(3, 8), CompletedAt = D(3, 20), StartedByUserId = 1, MediaId = 1, IncubatorEquipmentId = 1, Temperature = "30-35", Duration = "72h", ExpectedReadingAt = D(6, 8), Outcome = "Transferred" },
            new Incubation { Id = 2, TestOrderId = 1, StepNumber = 1, StepName = "Plate Count", StageNumber = 2, ParentIncubationId = 1, StartedAt = D(3, 21), StartedByUserId = 2, MediaId = 1, IncubatorEquipmentId = 1, Temperature = "30-35", Duration = "48h", ExpectedReadingAt = D(5, 21) },
            // TO1: a completed single-stage step whose actor comes from its CountTestReading.
            new Incubation { Id = 3, TestOrderId = 1, StepNumber = 2, StepName = "Yeast Count", StageNumber = 1, StartedAt = D(3, 8), CompletedAt = D(6, 8), StartedByUserId = 1, Temperature = "20-25", Duration = "120h", Outcome = "Read" },
            // TO1: stage pair linked by step name only (no ParentIncubationId); same analyst on stage 2? no - different.
            new Incubation { Id = 4, TestOrderId = 1, StepNumber = 3, StepName = "Mold Count", StageNumber = 1, StartedAt = D(3, 8), CompletedAt = D(3, 18), StartedByUserId = 1 },
            new Incubation { Id = 5, TestOrderId = 1, StepNumber = 3, StepName = "Mold Count", StageNumber = 2, StartedAt = D(3, 19), CompletedAt = D(5, 19), StartedByUserId = 3 },
            // TO2: completed, actor from the latest SampleLocation entry.
            new Incubation { Id = 6, TestOrderId = 2, StepNumber = 2, StepName = "Selective Plating", StageNumber = 1, StartedAt = D(4, 8), CompletedAt = D(5, 8), StartedByUserId = 1, MediaId = 1, IncubatorEquipmentId = 1, Temperature = "35", Duration = "24h" },
            // TO3: completed, actor from the Result.
            new Incubation { Id = 7, TestOrderId = 3, StepNumber = 1, StepName = "Measure", StageNumber = 1, StartedAt = D(5, 8), CompletedAt = D(5, 9), StartedByUserId = 2 },
            // TO4: completed, actor from the PathogenObservation of the same step.
            new Incubation { Id = 8, TestOrderId = 4, StepNumber = 1, StepName = "Selective Plating", StageNumber = 1, StartedAt = D(4, 8), CompletedAt = D(5, 8), StartedByUserId = 1 },
            // TO5: completed, actor from the last WorkflowHistory row.
            new Incubation { Id = 9, TestOrderId = 5, StepNumber = 1, StepName = "Enrichment", StageNumber = 1, StartedAt = D(4, 8), CompletedAt = D(5, 8) },
            // TO6: completed, no actor anywhere; also a stage-2 child whose parent started under another user.
            new Incubation { Id = 10, TestOrderId = 6, StepNumber = 1, StepName = "Enrichment", StageNumber = 1, StartedAt = D(4, 8), CompletedAt = D(5, 8) },
            new Incubation { Id = 11, TestOrderId = 6, StepNumber = 2, StepName = "Plating", StageNumber = 2, StartedAt = D(5, 9), StartedByUserId = 2 },
            // Retest orders.
            new Incubation { Id = 12, TestOrderId = 7, StepNumber = 1, StepName = "Yeast Count", StageNumber = 1, StartedAt = D(3, 8), StartedByUserId = 1 },
            new Incubation { Id = 13, TestOrderId = 8, StepNumber = 1, StepName = "Yeast Count", StageNumber = 1, StartedAt = D(12, 8), CompletedAt = D(15, 8), StartedByUserId = 1 },
            new Incubation { Id = 14, TestOrderId = 9, StepNumber = 1, StepName = "Yeast Count", StageNumber = 1, StartedAt = D(14, 8), CompletedAt = D(17, 8), StartedByUserId = 2 });

        // ---- Count readings (one inactive, one for the non-first step).
        db.CountTestReadings.AddRange(
            new CountTestReading { Id = 1, TestOrderId = 1, StepName = "Plate Count", PlateReadings = "10,12", DilutionFactor = 1m, Average = 11m, CalculatedResult = 11m, ReportedResult = "11", AlertLimit = "10", ActionLimit = "50", SpecLimit = "100", Status = ResultStatus.AlertLimitExceeded, EnteredByUserId = 1, EnteredAt = D(6, 9) },
            new CountTestReading { Id = 2, TestOrderId = 1, StepName = "Plate Count", PlateReadings = "9,9", DilutionFactor = 1m, Average = 9m, CalculatedResult = 9m, ReportedResult = "9", Status = ResultStatus.WithinLimits, IsActive = false, EnteredByUserId = 2, EnteredAt = D(6, 8) },
            new CountTestReading { Id = 3, TestOrderId = 1, StepName = "Yeast Count", PlateReadings = "0,0", DilutionFactor = 10m, Average = 0m, CalculatedResult = 0m, ReportedResult = "<10", SpecLimit = "100", Status = ResultStatus.WithinLimits, EnteredByUserId = 3, EnteredAt = D(6, 10) },
            new CountTestReading { Id = 4, TestOrderId = 8, StepName = "Yeast Count", PlateReadings = "120,130", DilutionFactor = 1m, Average = 125m, CalculatedResult = 125m, ReportedResult = "125", SpecLimit = "100", Status = ResultStatus.OutOfSpecification, EnteredByUserId = 1, EnteredAt = D(15, 9) },
            new CountTestReading { Id = 5, TestOrderId = 9, StepName = "Yeast Count", PlateReadings = "30,32", DilutionFactor = 1m, Average = 31m, CalculatedResult = 31m, ReportedResult = "31", SpecLimit = "100", Status = ResultStatus.WithinLimits, EnteredByUserId = 2, EnteredAt = D(17, 9) });

        // ---- TO2 locations (every ResolveLocationKey branch except None) and observations.
        SampleLocation Loc(int id, string reported, ResultStatus? status, int? entered = 1, int day = 5) => new()
        {
            Id = id, SampleId = 1, TestOrderId = 2, LocationType = LocationType.Room, DilutionFactor = 1m,
            ReportedResult = reported, Status = status, EnteredByUserId = entered, EnteredAt = D(day, 8 + id)
        };
        var l1 = Loc(1, "Detected", ResultStatus.Detected); l1.RoomTestConfigurationId = 1;
        var l2 = Loc(2, "Absent", ResultStatus.Absent); l2.LocationType = LocationType.MachinePart; l2.MachinePartConfigurationId = 1;
        var l3 = Loc(3, "Detected", ResultStatus.Detected); l3.LocationType = LocationType.WaterSamplingPoint; l3.WaterSamplingPointId = 1;
        var l4 = Loc(4, "Detected", ResultStatus.Detected); l4.LocationType = LocationType.WaterSamplingPoint; l4.SamplingConfigurationId = 1;
        var l5 = Loc(5, "Absent", ResultStatus.Absent); l5.RoomTestConfigurationId = 99;
        var l6 = Loc(6, "", null, null); l6.LocationType = LocationType.MachinePart; l6.MachinePartConfigurationId = 98;
        var l7 = Loc(7, "x", ResultStatus.PendingConfirmation);
        var l8 = Loc(8, " ", null);
        var l9 = Loc(9, "10 CFU", ResultStatus.AlertLimitExceeded); l9.RoomTestConfigurationId = 1; l9.AlertLimit = "5"; l9.ActionLimit = "10"; l9.SpecLimit = "20"; l9.CFUResult = 10m; l9.CalculatedResult = 10m; l9.Unit = "CFU/plate";
        var l10 = Loc(10, "OK", null);
        db.SampleLocations.AddRange(l1, l2, l3, l4, l5, l6, l7, l8, l9, l10);

        LocationPathogenObservation Lpo(int id, int locId, GrowthObservation g, int user, int minute) => new()
        {
            Id = id, SampleLocationId = locId, TestOrderId = 2, GrowthObservation = g, ObservedByUserId = user, ObservedAt = D(5, 12, minute), CreatedAt = D(5, 12, minute)
        };
        db.LocationPathogenObservations.AddRange(
            Lpo(1, 1, GrowthObservation.GrowthConforming, 1, 1),   // confirmatory: all conforming
            Lpo(2, 2, GrowthObservation.GrowthConforming, 2, 2),   // confirmatory: all no growth
            Lpo(3, 3, GrowthObservation.GrowthConforming, 3, 3),   // confirmatory: mixed
            Lpo(4, 4, GrowthObservation.NoGrowth, 1, 4),           // SampleLocation reported: Detected
            Lpo(5, 5, GrowthObservation.NoGrowth, 2, 5),           // SampleLocation reported: Absent
            Lpo(6, 6, GrowthObservation.NoGrowth, 3, 6),           // plain NoGrowth
            Lpo(7, 7, GrowthObservation.GrowthNonConforming, 1, 7),// PendingConfirmation -> plain NonConforming
            Lpo(8, 8, GrowthObservation.GrowthConforming, 2, 8),   // blank reported -> plain Conforming
            Lpo(9, 9, GrowthObservation.NoGrowth, 3, 9),           // SampleLocation reported: status passed through
            Lpo(10, 10, GrowthObservation.NoGrowth, 1, 10));       // reported, status null -> WithinLimits
        ConfirmatoryPlateObservation Plate(int id, int obsId, int idx, GrowthObservation g) => new()
        {
            Id = id, LocationPathogenObservationId = obsId, MaterialId = 1, MediumIndex = idx, Observation = g, RecordedByUserId = 1, RecordedAtUtc = D(5, 13)
        };
        db.ConfirmatoryPlateObservations.AddRange(
            Plate(1, 1, 0, GrowthObservation.GrowthConforming), Plate(2, 1, 1, GrowthObservation.GrowthConforming),
            Plate(3, 2, 0, GrowthObservation.NoGrowth), Plate(4, 2, 1, GrowthObservation.NoGrowth),
            Plate(5, 3, 0, GrowthObservation.GrowthConforming), Plate(6, 3, 1, GrowthObservation.NoGrowth));

        // ---- Plain pathogen observations, result, analysis, biochemical, history.
        db.PathogenObservations.AddRange(
            new PathogenObservation { Id = 1, TestOrderId = 2, StepName = "TSB Enrichment", StepOrder = 1, Observation = GrowthObservation.NoGrowth, ObservedByUserId = 3, ObservedAt = D(4, 9) },
            new PathogenObservation { Id = 2, TestOrderId = 4, StepName = "Selective Plating", StepOrder = 2, Observation = GrowthObservation.GrowthConforming, ObservedByUserId = 2, ObservedAt = D(5, 9) },
            new PathogenObservation { Id = 3, TestOrderId = 4, StepName = "Selective Plating", StepOrder = 2, Observation = GrowthObservation.GrowthNonConforming, ObservedByUserId = 3, ObservedAt = D(5, 10) });
        db.Results.AddRange(
            new Result { Id = 1, TestOrderId = 3, RawValue = "6.2", InterpretedValue = "6.2", Type = ResultType.Numeric, EnteredByUserId = 1, EnteredAt = D(5, 10) },
            new Result { Id = 2, TestOrderId = 4, RawValue = "Absent", Type = ResultType.GrowthNoGrowth, EnteredByUserId = 3, EnteredAt = D(5, 11) });
        db.WorkflowStepResults.AddRange(
            new WorkflowStepResult { Id = 1, IncubationId = 8, TestOrderId = 4, StepName = "Biochemical", StepType = StepType.BiochemicalTest, BiochemicalResultText = "Oxidase negative", BiochemicalOrganismDetected = true, SubmittedByUserId = 2, SubmittedAtUtc = D(5, 14) },
            new WorkflowStepResult { Id = 2, IncubationId = 8, TestOrderId = 4, StepName = "Biochemical 2", StepType = StepType.BiochemicalTest, BiochemicalResultText = "Indole positive", BiochemicalOrganismDetected = false, SubmittedByUserId = 3, SubmittedAtUtc = D(5, 15) },
            new WorkflowStepResult { Id = 3, IncubationId = 8, TestOrderId = 4, StepName = "Biochemical 3", StepType = StepType.BiochemicalTest, BiochemicalResultText = "Inconclusive", SubmittedByUserId = 1, SubmittedAtUtc = D(5, 16) },
            new WorkflowStepResult { Id = 4, IncubationId = 8, TestOrderId = 4, StepName = "No biochemical text", StepType = StepType.SelectivePlating, SubmittedByUserId = 1, SubmittedAtUtc = D(5, 17) });
        db.WorkflowHistories.AddRange(
            new WorkflowHistory { Id = 1, TestOrderId = 5, FromStep = WorkflowStep.Waiting, ToStep = WorkflowStep.Running, Note = "Started", PerformedByUserId = 1, Timestamp = D(4, 8) },
            new WorkflowHistory { Id = 2, TestOrderId = 5, FromStep = WorkflowStep.Running, ToStep = WorkflowStep.Incubating, PerformedByUserId = 3, Timestamp = D(5, 8) },
            new WorkflowHistory { Id = 3, TestOrderId = 4, FromStep = WorkflowStep.Waiting, ToStep = WorkflowStep.Ready, Note = "Done", PerformedByUserId = 2, Timestamp = D(5, 18) });

        db.TestAnalyses.AddRange(
            new TestAnalysis
            {
                Id = 1, TestOrderId = 3, AnalysisType = WorkflowType.Measurement, EquipmentId = 2, AnalysedAt = D(5, 9), UnitAmount = 1.5m,
                ConditionsJson = "{\"temp\":\"25\"}", ValidityRecordType = "Buffer", ValidityRecordId = 7, IsActive = true,
                EnteredByUserId = 1, EnteredAt = D(5, 10), SignatureId = 3, Comment = "pH check"
            },
            new TestAnalysis { Id = 2, TestOrderId = 3, AnalysisType = WorkflowType.Measurement, AnalysedAt = D(4, 9), IsActive = false, EnteredByUserId = 2, EnteredAt = D(4, 10), SignatureId = 3 });
        db.ParameterResults.AddRange(
            new ParameterResult
            {
                Id = 1, TestAnalysisId = 1, TestOrderId = 3, SpecificationId = 5, ParameterName = "pH", ReportedValue = 6.2m, ReportedDisplay = "6.2",
                Unit = "pH", SpecLimit = "5.0-7.0", ResultBasis = ResultBasis.PercentAsIs, ComparisonStatus = ResultStatus.WithinLimits,
                OverRange = false, BelowLoq = true, CalculationJson = "{\"k\":1}", StageReached = 1
            },
            new ParameterResult { Id = 2, TestAnalysisId = 1, TestOrderId = 3, SpecificationId = 5, ParameterName = "inactive", ReportedDisplay = "-", ComparisonStatus = ResultStatus.WithinLimits, IsActive = false },
            new ParameterResult { Id = 3, TestAnalysisId = 2, TestOrderId = 3, SpecificationId = 5, ParameterName = "old", ReportedDisplay = "-", ComparisonStatus = ResultStatus.WithinLimits });
        db.ResultReadings.AddRange(
            new ResultReading { Id = 1, ParameterResultId = 1, Kind = ReadingKind.Replicate, Index = 2, Value1 = 6.3m, ComputedValue = 6.3m, Passed = true },
            new ResultReading { Id = 2, ParameterResultId = 1, Kind = ReadingKind.Replicate, Index = 1, Stage = 1, TimePointMinutes = 5m, Value1 = 6.1m, Value2 = 6.2m, Value3 = 6.3m, Text = "ok", ComputedValue = 6.2m, Passed = null });

        // ---- Extra samples covering the other DisplayName / category branches.
        db.Samples.AddRange(
            new Sample { Id = 11, ReferenceNumber = "S-2026-0011", Category = SampleCategory.AfterCleaning, MachineId = 1, CauseOfTestingId = 1, ControlNumber = "CTRL-11", SampledBy = "Dan Sampler", BatchNumber = "B-77", PreviousProductName = "Zinc Cream", ReceivedByUserId = 1, ReceivedAt = D(2, 9), Status = SampleStatus.InTesting },
            new Sample { Id = 12, ReferenceNumber = "S-2026-0012", Category = SampleCategory.Water, WaterSamplingPointId = 2, CauseOfTestingId = 1, ControlNumber = "CTRL-12", SampledBy = "Dan Sampler", ReceivedByUserId = 2, ReceivedAt = D(2, 9), Status = SampleStatus.Rejected, ReviewedByUserId = 2, ReviewedAt = D(3), ApprovedByUserId = 3, ApprovedAt = D(4), ApprovalDecision = ApprovalDecision.Reject },
            new Sample { Id = 13, ReferenceNumber = "S-2026-0013", Category = SampleCategory.EnvironmentalMonitoring, DepartmentId = 1, CauseOfTestingId = 1, ControlNumber = "CTRL-13", SampledBy = "Dan Sampler", ReceivedByUserId = 3, ReceivedAt = D(2, 9), Status = SampleStatus.UnderReview },
            new Sample { Id = 14, ReferenceNumber = "S-2026-0014", Category = SampleCategory.RawMaterial, CauseOfTestingId = 1, ControlNumber = "CTRL-14", SampledBy = "Dan Sampler", ReceivedByUserId = 77, ReceivedAt = D(2, 9), Status = SampleStatus.Received });
        db.TestOrders.Add(new TestOrder { Id = 11, SampleId = 13, TestCode = "EM_TAMC", SectionId = 1, Status = ApprovalStatus.ResultEntered, CurrentStep = WorkflowStep.Ready });
        db.SampleLocations.Add(new SampleLocation { Id = 11, SampleId = 13, TestOrderId = 11, LocationType = LocationType.Room, RoomTestConfigurationId = 1, DilutionFactor = 1m, ReportedResult = "3", Status = ResultStatus.WithinLimits, EnteredByUserId = 1, EnteredAt = D(5) });

        // ---- Sample 15: still in testing, so the clock-driven workflow states resolve (fixed clock 2026-03-20 12:00 UTC).
        db.TestWorkflowStepMedias.AddRange(
            new TestWorkflowStepMedia { Id = 1, TestWorkflowStepId = 1, MediaProductId = 1, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 30, TempMax = 35, IsRequired = true },
            new TestWorkflowStepMedia { Id = 2, TestWorkflowStepId = 3, MediaProductId = 1, IncubationMinHours = 72, IncubationMaxHours = 120, TempMin = 30, TempMax = 35, IsRequired = true });
        db.Samples.Add(new Sample { Id = 15, ReferenceNumber = "S-2026-0015", Category = SampleCategory.FinishedProduct, ItemId = 1, CauseOfTestingId = 1, ControlNumber = "CTRL-15", SampledBy = "Dan Sampler", ReceivedByUserId = 1, ReceivedAt = D(10, 9), Status = SampleStatus.InTesting });
        db.TestOrders.AddRange(
            new TestOrder { Id = 12, SampleId = 15, TestCode = "TAMC", SectionId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating },   // count incubation still inside its minimum window
            new TestOrder { Id = 13, SampleId = 15, TestCode = "TAMC", SectionId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating },   // count incubation past its minimum window
            new TestOrder { Id = 14, SampleId = 15, TestCode = "ECOLI", SectionId = 1, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting },        // TSB test, no TSB started
            new TestOrder { Id = 15, SampleId = 15, TestCode = "ECOLI", SectionId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating },   // TSB incubating
            new TestOrder { Id = 16, SampleId = 15, TestCode = "ECOLI", SectionId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating },   // TSB window elapsed
            new TestOrder { Id = 17, SampleId = 15, TestCode = "TYMC", SectionId = 1, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Incubating });    // no medium window configured
        db.Incubations.AddRange(
            new Incubation { Id = 15, TestOrderId = 12, StepNumber = 1, StepName = "Plate Count", StageNumber = 1, StartedAt = D(20, 8), IncubationStartUtc = D(20, 8), IncubationEndUtc = D(25, 8), StartedByUserId = 1, MediaId = 1, IncubatorEquipmentId = 1, Temperature = "30-35", Duration = "72-120 hours" },
            new Incubation { Id = 16, TestOrderId = 13, StepNumber = 1, StepName = "Plate Count", StageNumber = 1, StartedAt = D(10, 8), IncubationStartUtc = D(10, 8), IncubationEndUtc = D(15, 8), StartedByUserId = 2, MediaId = 1, IncubatorEquipmentId = 1 },
            new Incubation { Id = 17, TestOrderId = 15, StepNumber = 1, StepName = "TSB Enrichment", StageNumber = 1, StartedAt = D(20, 6), IncubationStartUtc = D(20, 6), IncubationEndUtc = D(21, 6), StartedByUserId = 1, MediaId = 1, IncubatorEquipmentId = 1 },
            new Incubation { Id = 18, TestOrderId = 16, StepNumber = 1, StepName = "TSB Enrichment", StageNumber = 1, StartedAt = D(18, 6), IncubationStartUtc = D(18, 6), IncubationEndUtc = D(19, 6), StartedByUserId = 1, MediaId = 1, IncubatorEquipmentId = 1 },
            new Incubation { Id = 19, TestOrderId = 17, StepNumber = 1, StepName = "Yeast Count", StageNumber = 1, StartedAt = D(20, 8), IncubationStartUtc = D(20, 8), StartedByUserId = 3 });

        db.SaveChanges();
        return new MicroLimsDbContext(options);
    }

    private static string GoldenPath(string name, [CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Golden", name);

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd('\n') + "\n";

    private static void AssertGolden(string name, object value)
    {
        var actual = Normalize(JsonSerializer.Serialize(value, Json));
        var path = GoldenPath(name);
        if (!File.Exists(path) || Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }
        Assert.Equal(Normalize(File.ReadAllText(path)), actual);
    }

    [Fact]
    public async Task GetSummaryAsync_Unrestricted_MatchesGolden()
    {
        using var _ = new InvariantCulture();
        await using var db = Seed(NewOptions());
        var summary = await Service(db).GetSummaryAsync(1, null);
        Assert.NotNull(summary);
        AssertGolden("SampleSummary_all.json", summary!);
    }

    [Fact]
    public async Task GetSummaryAsync_ScopedToOneSection_MatchesGolden()
    {
        using var _ = new InvariantCulture();
        await using var db = Seed(NewOptions());
        var summary = await Service(db).GetSummaryAsync(1, new[] { 1 });
        Assert.NotNull(summary);
        AssertGolden("SampleSummary_scoped.json", summary!);
    }

    [Fact]
    public async Task GetSummaryAsync_OtherCategories_MatchesGolden()
    {
        using var _ = new InvariantCulture();
        await using var db = Seed(NewOptions());
        var service = Service(db);
        var all = new SortedDictionary<int, SampleSummaryDto?>();
        foreach (var id in new[] { 11, 12, 13, 14, 15, 999 })
            all[id] = await service.GetSummaryAsync(id, null);
        Assert.Null(all[999]);
        AssertGolden("SampleSummary_categories.json", all);
    }
}
