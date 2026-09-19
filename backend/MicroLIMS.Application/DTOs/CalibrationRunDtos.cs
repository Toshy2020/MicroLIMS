using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record CreateCalibrationRunCheckRequest(
    CalibrationCheckType CheckType,
    int SequencePosition,
    decimal? NominalMgPerL,
    decimal MeasuredMgPerL);

public record CreateCalibrationRunAnalyteRequest(
    int TestAnalyteId,
    decimal CorrelationValue,
    CorrelationType CorrelationType,
    int NumberOfStandards,
    decimal LowestStandardMgPerL,
    decimal HighestStandardMgPerL,
    List<CreateCalibrationRunCheckRequest>? Checks);

public record CreateCalibrationRunRequest(
    int TestDefinitionId,
    int EquipmentId,
    int CalibrationStandardMaterialId,
    int? IcvStandardMaterialId,
    DateTime CalibrationAt = default,
    string Password = "",
    string? Comment = null,
    List<CreateCalibrationRunAnalyteRequest>? Analytes = null);

public record PreviewCalibrationRunRequest(
    int TestDefinitionId,
    int EquipmentId,
    int CalibrationStandardMaterialId,
    int? IcvStandardMaterialId,
    DateTime CalibrationAt = default,
    string? Comment = null,
    List<CreateCalibrationRunAnalyteRequest>? Analytes = null);


public record WithdrawCalibrationRunRequest(
    string Reason,
    string Password);

public record CalibrationRunFilter(
    int? TestDefinitionId = null,
    string? TestCode = null,
    bool? Passed = null,
    CalibrationRunStatus? Status = null,
    DateTime? Date = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null);

public record CalibrationRunCheckView(
    int Id,
    int CalibrationRunAnalyteId,
    CalibrationCheckType CheckType,
    int SequencePosition,
    decimal? NominalMgPerL,
    decimal MeasuredMgPerL,
    decimal? RecoveryPercent,
    bool Passed)
{
    public static CalibrationRunCheckView From(CalibrationRunCheck c) => new(
        c.Id,
        c.CalibrationRunAnalyteId,
        c.CheckType,
        c.SequencePosition,
        c.NominalMgPerL,
        c.MeasuredMgPerL,
        c.RecoveryPercent,
        c.Passed);
}

public record CalibrationRunAnalyteView(
    int Id,
    int CalibrationRunId,
    int TestAnalyteId,
    string Element,
    decimal WavelengthNm,
    AnalyteView View,
    decimal CorrelationValue,
    CorrelationType CorrelationType,
    int NumberOfStandards,
    decimal LowestStandardMgPerL,
    decimal HighestStandardMgPerL,
    bool Passed,
    string? FailureReasons,
    bool IsUsable,
    List<CalibrationRunCheckView> Checks)
{
    public static CalibrationRunAnalyteView From(CalibrationRunAnalyte a)
    {
        var runActive = a.CalibrationRun == null || a.CalibrationRun.Status == CalibrationRunStatus.Active;
        return new(
            a.Id,
            a.CalibrationRunId,
            a.TestAnalyteId,
            a.Element,
            a.WavelengthNm,
            a.View,
            a.CorrelationValue,
            a.CorrelationType,
            a.NumberOfStandards,
            a.LowestStandardMgPerL,
            a.HighestStandardMgPerL,
            a.Passed,
            a.FailureReasons,
            a.Passed && runActive,
            a.Checks?.OrderBy(c => c.SequencePosition).Select(CalibrationRunCheckView.From).ToList() ?? new List<CalibrationRunCheckView>());
    }
}

public record CalibrationRunDocumentView(
    int Id,
    int CalibrationRunId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string ContentSha256,
    int UploadedByUserId,
    string? UploadedByName,
    DateTime UploadedAt)
{
    public static CalibrationRunDocumentView From(CalibrationRunDocument d) => new(
        d.Id,
        d.CalibrationRunId,
        d.OriginalFileName,
        d.ContentType,
        d.SizeBytes,
        d.ContentSha256,
        d.UploadedByUserId,
        d.UploadedByUser?.FullName,
        d.UploadedAt);
}

public record CalibrationRunView(
    int Id,
    string Code,
    int TestDefinitionId,
    string? TestCode,
    string? TestDisplayName,
    string? MethodAbbreviation,
    int SectionId,
    string? SectionName,
    int EquipmentId,
    string? EquipmentCode,
    string? EquipmentName,
    int CalibrationStandardMaterialId,
    string? CalibrationStandardMaterialName,
    string? CalibrationStandardBatchNumber,
    DateTime? CalibrationStandardExpiryDate,
    int? IcvStandardMaterialId,
    string? IcvStandardMaterialName,
    string? IcvStandardBatchNumber,
    DateTime? IcvStandardExpiryDate,
    DateTime CalibrationAt,
    int PerformedByUserId,
    string? PerformedByName,
    DateTime PerformedAt,
    int SignatureId,
    string? Comment,
    int AnalytesPassed,
    int AnalytesTotal,
    bool Passed,
    CalibrationRunStatus Status,
    DateTime? WithdrawnAt,
    int? WithdrawnByUserId,
    string? WithdrawnByName,
    string? WithdrawalReason,
    int? WithdrawalSignatureId,
    CalibrationRunDocumentView? Document,
    List<CalibrationRunAnalyteView> Analytes)
{
    public static CalibrationRunView From(CalibrationRun r) => new(
        r.Id,
        r.Code,
        r.TestDefinitionId,
        r.TestDefinition?.Code,
        r.TestDefinition?.DisplayName,
        r.TestDefinition?.MethodAbbreviation,
        r.SectionId,
        r.Section?.Name,
        r.EquipmentId,
        r.Equipment?.Code,
        r.Equipment?.Name,
        r.CalibrationStandardMaterialId,
        r.CalibrationStandardMaterial?.MaterialName,
        r.CalibrationStandardMaterial?.BatchNumber,
        r.CalibrationStandardMaterial?.ExpiryDate,
        r.IcvStandardMaterialId,
        r.IcvStandardMaterial?.MaterialName,
        r.IcvStandardMaterial?.BatchNumber,
        r.IcvStandardMaterial?.ExpiryDate,
        r.CalibrationAt,
        r.PerformedByUserId,
        r.PerformedByUser?.FullName ?? r.Signature?.UserFullNameSnapshot,
        r.PerformedAt,
        r.SignatureId,
        r.Comment,
        r.AnalytesPassed,
        r.AnalytesTotal,
        r.Passed,
        r.Status,
        r.WithdrawnAt,
        r.WithdrawnByUserId,
        r.WithdrawnByUser?.FullName ?? r.WithdrawalSignature?.UserFullNameSnapshot,
        r.WithdrawalReason,
        r.WithdrawalSignatureId,
        r.Document != null ? CalibrationRunDocumentView.From(r.Document) : null,
        r.Analytes?.Select(CalibrationRunAnalyteView.From).ToList() ?? new List<CalibrationRunAnalyteView>());
}

public record CalibrationRunReportDetailsDto(
    CalibrationEntryMode? CalibrationEntryMode,
    decimal? CalMinCorrelation,
    CorrelationType? CalCorrelationType,
    int? CalMinStandards,
    decimal? CalCheckRecoveryLowPercent,
    decimal? CalCheckRecoveryHighPercent,
    decimal? CalBlankMax,
    decimal? CalIsRecoveryLowPercent,
    decimal? CalIsRecoveryHighPercent,
    bool? CalRequireBlank,
    bool? CalRequireIcv,
    bool? CalRequireCcv,
    bool? CalRequireInternalStandard,
    ReportedConcentrationBasis? ReportedConcentrationBasis,
    int? CalMaxRunAgeHours,
    DateTime CalibrationAt,
    CalibrationRunStatus Status,
    DateTime? WithdrawnAt,
    string? WithdrawnByName,
    string? WithdrawalReason,
    SignatureDto? WithdrawalSignature,
    string? EquipmentVendor,
    string? CdsSoftware,
    SignatureDto? Signature,
    CalibrationRunDocumentView? Document,
    List<CalibrationRunAnalyteView> Analytes);

public record CalibrationRunPreviewCheckResult(
    CalibrationCheckType CheckType,
    int SequencePosition,
    decimal? NominalMgPerL,
    decimal MeasuredMgPerL,
    decimal? RecoveryPercent,
    bool Passed);

public record CalibrationRunPreviewAnalyteResult(
    int TestAnalyteId,
    string Element,
    decimal WavelengthNm,
    AnalyteView View,
    decimal CorrelationValue,
    CorrelationType CorrelationType,
    int NumberOfStandards,
    decimal LowestStandardMgPerL,
    decimal HighestStandardMgPerL,
    bool Passed,
    string? FailureReasons,
    bool IsUsable,
    List<CalibrationRunPreviewCheckResult> Checks);

public record CalibrationRunPreviewResult(
    int TestDefinitionId,
    int EquipmentId,
    int CalibrationStandardMaterialId,
    int? IcvStandardMaterialId,
    DateTime CalibrationAt,
    bool StandardsExpiredOrMissing,
    string? StandardsExpiryFailureReason,
    int AnalytesPassed,
    int AnalytesTotal,
    bool Passed,
    List<CalibrationRunPreviewAnalyteResult> Analytes);
