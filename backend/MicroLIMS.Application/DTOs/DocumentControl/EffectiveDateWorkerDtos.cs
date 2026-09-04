namespace MicroLIMS.Application.DTOs.DocumentControl;

public record EffectiveDateTransitionDetail(
    int DocumentMasterId,
    string CompanyDocumentCode,
    int ActivatedRevisionId,
    string ActivatedRevisionNumber,
    DateTime EffectiveDate,
    bool IsDelayedExecution,
    int? SupersededRevisionId,
    string? SupersededRevisionNumber
);

public record EffectiveDateFailureDetail(
    int DocumentMasterId,
    int DocumentRevisionId,
    string CompanyDocumentCode,
    string ErrorMessage,
    string? ExceptionType
);

public record EffectiveDateProcessingResultDto(
    DateTime ExecutionTimestampUtc,
    int EligibleRevisionsCount,
    int SuccessfullyActivatedCount,
    int FailedRevisionsCount,
    IReadOnlyList<EffectiveDateTransitionDetail> ActivatedTransitions,
    IReadOnlyList<EffectiveDateFailureDetail> Failures
);
