using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

public sealed record WorkflowProfileVersionDto(
    int Id, WorkflowType WorkflowType, string? TestCode, string? TestName,
    decimal? WorkUnits, decimal? TargetAnalystDays, DateOnly EffectiveFrom, WorkflowProfileStatus Status,
    string ReasonForChange, int? ElectronicSignatureId, int CreatedByUserId, string CreatedByName,
    DateTime CreatedAt, DateTime? SignedAt, uint Version);

// WorkUnits / TargetAnalystDays are spelled identically on the wire and here: the
// legacy PUT bound "weight" to a differently named property and silently saved 0.
public sealed record CreateWorkflowProfileVersionRequest(
    WorkflowType WorkflowType, string? TestCode, decimal? WorkUnits,
    decimal? TargetAnalystDays, DateOnly EffectiveFrom, string ReasonForChange, string Password);

public sealed record SignWorkflowProfileDraftRequest(
    decimal? WorkUnits, decimal? TargetAnalystDays,
    DateOnly EffectiveFrom, string ReasonForChange, string Password);

public enum ProfileCoverage { Configured, Partial, NeedsConfiguration }

public sealed record WorkflowProfileTestRowDto(
    string TestCode, string TestName, decimal? EffectiveWorkUnits,
    decimal? EffectiveTargetAnalystDays, bool HasOverride, ProfileCoverage Coverage);

public sealed record WorkflowProfileTypeRowDto(
    WorkflowType WorkflowType, decimal? EffectiveWorkUnits,
    decimal? EffectiveTargetAnalystDays, DateOnly? LastEffectiveFrom, DateTime? LastChangeAt, string? LastChangeByName,
    ProfileCoverage Coverage, int DraftCount, IReadOnlyList<WorkflowProfileTestRowDto> Tests);

public sealed record WorkflowProfileOverviewDto(IReadOnlyList<WorkflowProfileTypeRowDto> Types);

public sealed record WorkflowProfileCoverageDto(
    IReadOnlyList<WorkflowType> TypesWithoutUnits, IReadOnlyList<WorkflowType> TypesWithoutTarget,
    IReadOnlyList<WorkflowProfileTestRowDto> TestsNeedingConfiguration);
