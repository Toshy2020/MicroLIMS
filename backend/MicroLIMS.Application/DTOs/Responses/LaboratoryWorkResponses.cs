using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

// Response types for prepared media, media evaluations, reference
// cryovials, incubations and review returns.

public class CryovialResponse
{
    public int Id { get; init; }
    public string Code { get; init; } = null!;
    public int MaterialId { get; init; }
    public MaterialResponse? Material { get; init; }
    public int OrganismId { get; init; }
    public OrganismResponse? Organism { get; init; }
    public string OrganismNameSnapshot { get; init; } = null!;
    public string ManufacturerName { get; init; } = null!;
    public DateTime ExpiryDate { get; init; }
    public int NumberOfVialsPrepared { get; init; }
    public int VialsRemaining { get; init; }
    public string StorageCondition { get; init; } = null!;
    public bool PhysicalCheckConfirmed { get; init; }
    public string PhysicalCheckText { get; init; } = null!;
    public DateTime PreparedAt { get; init; }
    public int PreparedByUserId { get; init; }
    public ApprovalGateStatus ApprovalStatus { get; init; }
    public int? ApprovedByUserId { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public bool IsDestroyed { get; init; }
    public List<IdentityConfirmationEntryResponse> IdentityConfirmations { get; init; } = new();
    public List<ThawEventResponse> ThawHistory { get; init; } = new();

    public static CryovialResponse From(Cryovial e) => new()
    {
        Id = e.Id,
        Code = e.Code,
        MaterialId = e.MaterialId,
        Material = e.Material is null ? null : MaterialResponse.From(e.Material),
        OrganismId = e.OrganismId,
        Organism = e.Organism is null ? null : OrganismResponse.From(e.Organism),
        OrganismNameSnapshot = e.OrganismNameSnapshot,
        ManufacturerName = e.ManufacturerName,
        ExpiryDate = e.ExpiryDate,
        NumberOfVialsPrepared = e.NumberOfVialsPrepared,
        VialsRemaining = e.VialsRemaining,
        StorageCondition = e.StorageCondition,
        PhysicalCheckConfirmed = e.PhysicalCheckConfirmed,
        PhysicalCheckText = e.PhysicalCheckText,
        PreparedAt = e.PreparedAt,
        PreparedByUserId = e.PreparedByUserId,
        ApprovalStatus = e.ApprovalStatus,
        ApprovedByUserId = e.ApprovedByUserId,
        ApprovedAt = e.ApprovedAt,
        IsDestroyed = e.IsDestroyed,
        IdentityConfirmations = e.IdentityConfirmations.Select(IdentityConfirmationEntryResponse.From).ToList(),
        ThawHistory = e.ThawHistory.Select(ThawEventResponse.From).ToList(),
    };
}

public class IdentityConfirmationEntryResponse
{
    public int Id { get; init; }
    public int CryovialId { get; init; }
    public int MediaId { get; init; }
    public int IncubatorEquipmentId { get; init; }
    public DateTime IncubationStart { get; init; }
    public DateTime IncubationEnd { get; init; }
    public string ObservationText { get; init; } = null!;

    public static IdentityConfirmationEntryResponse From(IdentityConfirmationEntry e) => new()
    {
        Id = e.Id,
        CryovialId = e.CryovialId,
        MediaId = e.MediaId,
        IncubatorEquipmentId = e.IncubatorEquipmentId,
        IncubationStart = e.IncubationStart,
        IncubationEnd = e.IncubationEnd,
        ObservationText = e.ObservationText,
    };
}

public class ThawEventResponse
{
    public int Id { get; init; }
    public int CryovialId { get; init; }
    public DateTime ThawedAt { get; init; }
    public int ThawedByUserId { get; init; }
    public string? Notes { get; init; }

    public static ThawEventResponse From(ThawEvent e) => new()
    {
        Id = e.Id,
        CryovialId = e.CryovialId,
        ThawedAt = e.ThawedAt,
        ThawedByUserId = e.ThawedByUserId,
        Notes = e.Notes,
    };
}

public class MediaResponse
{
    public int Id { get; init; }
    public int MaterialId { get; init; }
    public MaterialResponse? Material { get; init; }
    public string LotNumber { get; init; } = null!;
    public string ManufacturerLot { get; init; } = null!;
    public string ManufacturerName { get; init; } = null!;
    public decimal TotalWeight { get; init; }
    public string TotalVolume { get; init; } = null!;
    public int? AutoclaveEquipmentId { get; init; }
    public EquipmentResponse? AutoclaveEquipment { get; init; }
    public string AutoclaveProgram { get; init; } = null!;
    public string LoadType { get; init; } = null!;
    public decimal Temperature { get; init; }
    public int CycleTime { get; init; }
    public int CycleNumber { get; init; }
    public decimal Ph { get; init; }
    public DateTime ExpiryDate { get; init; }
    public DateTime PreparedAt { get; init; }
    public int PreparedByUserId { get; init; }
    public MediaStatus Status { get; init; }
    public ApprovalGateStatus ApprovalStatus { get; init; }
    public int? ApprovedByUserId { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public bool IsReleasedForUse { get; init; }

    public static MediaResponse From(Media e) => new()
    {
        Id = e.Id,
        MaterialId = e.MaterialId,
        Material = e.Material is null ? null : MaterialResponse.From(e.Material),
        LotNumber = e.LotNumber,
        ManufacturerLot = e.ManufacturerLot,
        ManufacturerName = e.ManufacturerName,
        TotalWeight = e.TotalWeight,
        TotalVolume = e.TotalVolume,
        AutoclaveEquipmentId = e.AutoclaveEquipmentId,
        AutoclaveEquipment = e.AutoclaveEquipment is null ? null : EquipmentResponse.From(e.AutoclaveEquipment),
        AutoclaveProgram = e.AutoclaveProgram,
        LoadType = e.LoadType,
        Temperature = e.Temperature,
        CycleTime = e.CycleTime,
        CycleNumber = e.CycleNumber,
        Ph = e.Ph,
        ExpiryDate = e.ExpiryDate,
        PreparedAt = e.PreparedAt,
        PreparedByUserId = e.PreparedByUserId,
        Status = e.Status,
        ApprovalStatus = e.ApprovalStatus,
        ApprovedByUserId = e.ApprovedByUserId,
        ApprovedAt = e.ApprovedAt,
        IsReleasedForUse = e.IsReleasedForUse,
    };
}

public class MediaEvaluationResponse
{
    public int Id { get; init; }
    public int MediaId { get; init; }
    public MediaResponse? Media { get; init; }
    public EvaluationType EvaluationType { get; init; }
    public MediaEvaluationStatus Status { get; init; }
    public EvaluationOutcome? Outcome { get; init; }
    public DateTime AssignedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? CompletedByUserId { get; init; }
    public List<MediaEvaluationChallengeResponse> Challenges { get; init; } = new();

    public static MediaEvaluationResponse From(MediaEvaluation e) => new()
    {
        Id = e.Id,
        MediaId = e.MediaId,
        Media = e.Media is null ? null : MediaResponse.From(e.Media),
        EvaluationType = e.EvaluationType,
        Status = e.Status,
        Outcome = e.Outcome,
        AssignedAt = e.AssignedAt,
        CompletedAt = e.CompletedAt,
        CompletedByUserId = e.CompletedByUserId,
        Challenges = e.Challenges.Select(MediaEvaluationChallengeResponse.From).ToList(),
    };
}

public class MediaEvaluationChallengeResponse
{
    public int Id { get; init; }
    public int MediaEvaluationId { get; init; }
    public int OrganismId { get; init; }
    public OrganismResponse? Organism { get; init; }
    public int? CryovialId { get; init; }
    public CryovialResponse? Cryovial { get; init; }
    public int? LyophilizedDiskId { get; init; }
    public MaterialResponse? LyophilizedDisk { get; init; }
    public ChallengeRole? ChallengeRole { get; init; }
    public string InitialInoculum { get; init; } = null!;
    public int? IncubationId { get; init; }
    public IncubationResponse? Incubation { get; init; }
    public decimal? OldMediaCount { get; init; }
    public decimal? NewMediaCount { get; init; }
    public decimal? RecoveryPercent { get; init; }
    public int? ReferenceMediaId { get; init; }
    public MediaResponse? ReferenceMedia { get; init; }
    public string? ReferenceMediaLabel { get; init; }
    public bool? GrowthObserved { get; init; }
    public string? ObservedDescription { get; init; }
    public string? ExpectedDescription { get; init; }
    public bool? IsTurbid { get; init; }
    public EvaluationOutcome? Outcome { get; init; }
    public DateTime? ReadAt { get; init; }
    public int? ReadByUserId { get; init; }

    public static MediaEvaluationChallengeResponse From(MediaEvaluationChallenge e) => new()
    {
        Id = e.Id,
        MediaEvaluationId = e.MediaEvaluationId,
        OrganismId = e.OrganismId,
        Organism = e.Organism is null ? null : OrganismResponse.From(e.Organism),
        CryovialId = e.CryovialId,
        Cryovial = e.Cryovial is null ? null : CryovialResponse.From(e.Cryovial),
        LyophilizedDiskId = e.LyophilizedDiskId,
        LyophilizedDisk = e.LyophilizedDisk is null ? null : MaterialResponse.From(e.LyophilizedDisk),
        ChallengeRole = e.ChallengeRole,
        InitialInoculum = e.InitialInoculum,
        IncubationId = e.IncubationId,
        Incubation = e.Incubation is null ? null : IncubationResponse.From(e.Incubation),
        OldMediaCount = e.OldMediaCount,
        NewMediaCount = e.NewMediaCount,
        RecoveryPercent = e.RecoveryPercent,
        ReferenceMediaId = e.ReferenceMediaId,
        ReferenceMedia = e.ReferenceMedia is null ? null : MediaResponse.From(e.ReferenceMedia),
        ReferenceMediaLabel = e.ReferenceMediaLabel,
        GrowthObserved = e.GrowthObserved,
        ObservedDescription = e.ObservedDescription,
        ExpectedDescription = e.ExpectedDescription,
        IsTurbid = e.IsTurbid,
        Outcome = e.Outcome,
        ReadAt = e.ReadAt,
        ReadByUserId = e.ReadByUserId,
    };
}

public class IncubationResponse
{
    public int Id { get; init; }
    public int? TestOrderId { get; init; }
    public int StepNumber { get; init; }
    public string StepName { get; init; } = null!;
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? CompletedByUserId { get; init; }
    public string? Outcome { get; init; }
    public int? MediaId { get; init; }
    public MediaResponse? Media { get; init; }
    public int? IncubatorEquipmentId { get; init; }
    public EquipmentResponse? IncubatorEquipment { get; init; }
    public string? Temperature { get; init; }
    public string? Duration { get; init; }
    public DateTime? ExpectedReadingAt { get; init; }
    public DateTime? IncubationStartUtc { get; init; }
    public DateTime? IncubationEndUtc { get; init; }
    public DateTime? WindowReceivedAtUtc { get; init; }
    public int? StartedByUserId { get; init; }
    public int? ParentIncubationId { get; init; }
    public int StageNumber { get; init; }
    public int? MinimumDurationOverriddenByUserId { get; init; }
    public DateTime? MinimumDurationOverriddenAt { get; init; }
    public bool IsIncubationComplete { get; init; }

    public static IncubationResponse From(Incubation e) => new()
    {
        Id = e.Id,
        TestOrderId = e.TestOrderId,
        StepNumber = e.StepNumber,
        StepName = e.StepName,
        StartedAt = e.StartedAt,
        CompletedAt = e.CompletedAt,
        CompletedByUserId = e.CompletedByUserId,
        Outcome = e.Outcome,
        MediaId = e.MediaId,
        Media = e.Media is null ? null : MediaResponse.From(e.Media),
        IncubatorEquipmentId = e.IncubatorEquipmentId,
        IncubatorEquipment = e.IncubatorEquipment is null ? null : EquipmentResponse.From(e.IncubatorEquipment),
        Temperature = e.Temperature,
        Duration = e.Duration,
        ExpectedReadingAt = e.ExpectedReadingAt,
        IncubationStartUtc = e.IncubationStartUtc,
        IncubationEndUtc = e.IncubationEndUtc,
        WindowReceivedAtUtc = e.WindowReceivedAtUtc,
        StartedByUserId = e.StartedByUserId,
        ParentIncubationId = e.ParentIncubationId,
        StageNumber = e.StageNumber,
        MinimumDurationOverriddenByUserId = e.MinimumDurationOverriddenByUserId,
        MinimumDurationOverriddenAt = e.MinimumDurationOverriddenAt,
        IsIncubationComplete = e.IsIncubationComplete,
    };
}

public class TestReturnEventResponse
{
    public int Id { get; init; }
    public int TestOrderId { get; init; }
    public int ReviewerUserId { get; init; }
    public int? AssignedAnalystId { get; init; }
    public string? Reason { get; init; }
    public DateTime ReturnedAt { get; init; }

    public static TestReturnEventResponse From(TestReturnEvent e) => new()
    {
        Id = e.Id,
        TestOrderId = e.TestOrderId,
        ReviewerUserId = e.ReviewerUserId,
        AssignedAnalystId = e.AssignedAnalystId,
        Reason = e.Reason,
        ReturnedAt = e.ReturnedAt,
    };
}

// One sampling location of a test order, with its limits resolved from the
// location's own override or its room / machine part / water point
// configuration.
public record TestOrderLocationResponse(
    int Id,
    string LocationType,
    string LocationName,
    string? GradeClassification,
    string? AlertLimit,
    string? ActionLimit,
    string? SpecLimit,
    decimal? CFUResult,
    decimal? CalculatedResult,
    string? ReportedResult,
    string? RawReadings,
    string? Status,
    DateTime? EnteredAt)
{
    public static TestOrderLocationResponse From(SampleLocation l) => new(
        l.Id,
        l.LocationType.ToString(),
        l.RoomTestConfiguration?.Room?.Name ?? l.MachinePartConfiguration?.MachinePart?.Name ?? l.WaterSamplingPoint?.Code ?? string.Empty,
        l.RoomTestConfiguration?.Room?.GradeClassification,
        l.AlertLimit ?? l.RoomTestConfiguration?.AlertLimit ?? l.MachinePartConfiguration?.AlertLimit ?? l.SamplingConfiguration?.AlertLimit,
        l.ActionLimit ?? l.RoomTestConfiguration?.ActionLimit ?? l.MachinePartConfiguration?.ActionLimit ?? l.SamplingConfiguration?.ActionLimit,
        l.SpecLimit ?? l.RoomTestConfiguration?.SpecLimit ?? l.MachinePartConfiguration?.SpecLimit ?? l.SamplingConfiguration?.SpecLimit,
        l.CFUResult,
        l.CalculatedResult,
        l.ReportedResult,
        l.RawReadings,
        l.Status,
        l.EnteredAt);
}

// A recorded sample preparation (Start Testing).
public record SamplePreparationResponse(
    int Id,
    int SampleId,
    decimal Amount,
    string Technique,
    decimal? FiltrationVolume,
    decimal? WashingVolume,
    string Diluent,
    string Neutralizer,
    int PreparedByUserId,
    DateTime PreparedAt,
    int? SourceConfigurationId,
    bool WasConfirmedFromConfig)
{
    public static SamplePreparationResponse From(SamplePreparation p) => new(
        p.Id, p.SampleId, p.Amount, p.Technique, p.FiltrationVolume, p.WashingVolume,
        p.Diluent, p.Neutralizer, p.PreparedByUserId, p.PreparedAt,
        p.SourceConfigurationId, p.WasConfirmedFromConfig);
}
