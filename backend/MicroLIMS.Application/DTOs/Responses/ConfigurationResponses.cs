using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs.Responses;

// Response types for laboratory configuration: sections, equipment,
// chromatography columns, test definitions, specifications and media
// products. Related records appear only where the endpoint loads them.

public class DocumentSectionResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public int DepartmentId { get; init; }
    public bool IsActive { get; init; }

    public static DocumentSectionResponse From(DocumentSection e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        Code = e.Code,
        DepartmentId = e.DepartmentId,
        IsActive = e.IsActive,
    };
}

public class EquipmentResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public EquipmentType Type { get; init; }
    public string? Location { get; init; }
    public decimal? SetPointTemperature { get; init; }
    public DateTime? CalibrationDueDate { get; init; }
    public int SectionId { get; init; }
    public DocumentSectionResponse? Section { get; init; }
    public string? Vendor { get; init; }
    public CdsSoftware? CdsSoftware { get; init; }
    public string? ConnectionSettings { get; init; }
    public List<EquipmentCompatibleColumnResponse> CompatibleColumns { get; init; } = new();

    public static EquipmentResponse From(Equipment e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        Code = e.Code,
        Type = e.Type,
        Location = e.Location,
        SetPointTemperature = e.SetPointTemperature,
        CalibrationDueDate = e.CalibrationDueDate,
        SectionId = e.SectionId,
        Section = e.Section is null ? null : DocumentSectionResponse.From(e.Section),
        Vendor = e.Vendor,
        CdsSoftware = e.CdsSoftware,
        ConnectionSettings = e.ConnectionSettings,
        CompatibleColumns = e.CompatibleColumns.Select(EquipmentCompatibleColumnResponse.From).ToList(),
    };
}

public class EquipmentCompatibleColumnResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? SerialNumber { get; init; }
    public int SectionId { get; init; }
    public DocumentSectionResponse? Section { get; init; }
    public bool IsActive { get; init; }
    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }

    public static EquipmentCompatibleColumnResponse From(ChromatographyColumn e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Code = e.Code,
        Name = e.Name,
        SerialNumber = e.SerialNumber,
        SectionId = e.SectionId,
        Section = e.Section is null ? null : DocumentSectionResponse.From(e.Section),
        IsActive = e.IsActive,
        CreatedByUserId = e.CreatedByUserId,
        CreatedAt = e.CreatedAt,
        LastModifiedByUserId = e.LastModifiedByUserId,
        LastModifiedAt = e.LastModifiedAt,
    };
}

public class ChromatographyColumnResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? SerialNumber { get; init; }
    public int SectionId { get; init; }
    public DocumentSectionResponse? Section { get; init; }
    public bool IsActive { get; init; }
    public int CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public int LastModifiedByUserId { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public List<ColumnCompatibleEquipmentResponse> CompatibleEquipment { get; init; } = new();

    public static ChromatographyColumnResponse From(ChromatographyColumn e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Code = e.Code,
        Name = e.Name,
        SerialNumber = e.SerialNumber,
        SectionId = e.SectionId,
        Section = e.Section is null ? null : DocumentSectionResponse.From(e.Section),
        IsActive = e.IsActive,
        CreatedByUserId = e.CreatedByUserId,
        CreatedAt = e.CreatedAt,
        LastModifiedByUserId = e.LastModifiedByUserId,
        LastModifiedAt = e.LastModifiedAt,
        CompatibleEquipment = e.CompatibleEquipment.Select(ColumnCompatibleEquipmentResponse.From).ToList(),
    };
}

public class ColumnCompatibleEquipmentResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public EquipmentType Type { get; init; }
    public string? Location { get; init; }
    public decimal? SetPointTemperature { get; init; }
    public DateTime? CalibrationDueDate { get; init; }
    public int SectionId { get; init; }
    public DocumentSectionResponse? Section { get; init; }
    public string? Vendor { get; init; }
    public CdsSoftware? CdsSoftware { get; init; }
    public string? ConnectionSettings { get; init; }

    public static ColumnCompatibleEquipmentResponse From(Equipment e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        Code = e.Code,
        Type = e.Type,
        Location = e.Location,
        SetPointTemperature = e.SetPointTemperature,
        CalibrationDueDate = e.CalibrationDueDate,
        SectionId = e.SectionId,
        Section = e.Section is null ? null : DocumentSectionResponse.From(e.Section),
        Vendor = e.Vendor,
        CdsSoftware = e.CdsSoftware,
        ConnectionSettings = e.ConnectionSettings,
    };
}

public class TestDefinitionResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Code { get; init; } = null!;
    public string DisplayName { get; init; } = null!;
    public int SectionId { get; init; }
    public DocumentSectionResponse? Section { get; init; }
    public WorkflowType WorkflowType { get; init; }
    public bool IsActive { get; init; }
    public EquationType EquationType { get; init; }
    public bool RequiresSystemSuitability { get; init; }
    public string? MethodAbbreviation { get; init; }
    public decimal? SstMaxRsdPercent { get; init; }
    public decimal? SstMinResolution { get; init; }
    public decimal? SstMaxTailingFactor { get; init; }
    public decimal? SstMinTheoreticalPlates { get; init; }
    public CalibrationEntryMode? CalibrationEntryMode { get; init; }
    public decimal? CalMinCorrelation { get; init; }
    public CorrelationType? CalCorrelationType { get; init; }
    public int? CalMinStandards { get; init; }
    public decimal? CalCheckRecoveryLowPercent { get; init; }
    public decimal? CalCheckRecoveryHighPercent { get; init; }
    public decimal? CalBlankMax { get; init; }
    public decimal? CalIsRecoveryLowPercent { get; init; }
    public decimal? CalIsRecoveryHighPercent { get; init; }
    public bool? CalRequireBlank { get; init; }
    public bool? CalRequireIcv { get; init; }
    public bool? CalRequireCcv { get; init; }
    public bool? CalRequireInternalStandard { get; init; }
    public ReportedConcentrationBasis? ReportedConcentrationBasis { get; init; }
    public int? CalMaxRunAgeHours { get; init; }
    public EquipmentType? CalInstrumentType { get; init; }
    public string? CalStandardLevelsMgPerL { get; init; }
    public int? ReplicateCount { get; init; }
    public MeasurementEvaluationBasis? EvaluationBasis { get; init; }
    public string? ConditionFields { get; init; }
    public bool? UsesTare { get; init; }
    public decimal? DissolutionS1Offset { get; init; }
    public decimal? DissolutionS2MinOffset { get; init; }
    public decimal? DissolutionS3MinOffset { get; init; }
    public decimal? DissolutionS3MaxBelowS2Min { get; init; }
    public int? DisintegrationStage1Units { get; init; }
    public int? DisintegrationStage2Units { get; init; }
    public int? DisintegrationMaxStage1Failures { get; init; }
    public int? DisintegrationMinPassTotal { get; init; }
    public int? WvUnitCount { get; init; }
    public decimal? WvTabletBand1MaxMg { get; init; }
    public decimal? WvTabletBand1Percent { get; init; }
    public decimal? WvTabletBand2MaxMg { get; init; }
    public decimal? WvTabletBand2Percent { get; init; }
    public decimal? WvTabletBand3Percent { get; init; }
    public int? WvTabletMaxOutside { get; init; }
    public decimal? WvCapsuleInnerPercent { get; init; }
    public decimal? WvCapsuleOuterPercent { get; init; }
    public int? WvCapsuleS1MaxOutside { get; init; }
    public int? WvCapsuleS1MaxForRetest { get; init; }
    public int? WvCapsuleS2ExtraUnits { get; init; }
    public int? WvCapsuleS2MaxOutside { get; init; }
    public decimal? HplcMaxPreparationRsdPercent { get; init; }
    public ResponseMode ResponseMode { get; init; }
    public int? HplcMethodId { get; init; }

    public static TestDefinitionResponse From(TestDefinition e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Code = e.Code,
        DisplayName = e.DisplayName,
        SectionId = e.SectionId,
        Section = e.Section is null ? null : DocumentSectionResponse.From(e.Section),
        WorkflowType = e.WorkflowType,
        IsActive = e.IsActive,
        EquationType = e.EquationType,
        RequiresSystemSuitability = e.RequiresSystemSuitability,
        MethodAbbreviation = e.MethodAbbreviation,
        SstMaxRsdPercent = e.SstMaxRsdPercent,
        SstMinResolution = e.SstMinResolution,
        SstMaxTailingFactor = e.SstMaxTailingFactor,
        SstMinTheoreticalPlates = e.SstMinTheoreticalPlates,
        CalibrationEntryMode = e.CalibrationEntryMode,
        CalMinCorrelation = e.CalMinCorrelation,
        CalCorrelationType = e.CalCorrelationType,
        CalMinStandards = e.CalMinStandards,
        CalCheckRecoveryLowPercent = e.CalCheckRecoveryLowPercent,
        CalCheckRecoveryHighPercent = e.CalCheckRecoveryHighPercent,
        CalBlankMax = e.CalBlankMax,
        CalIsRecoveryLowPercent = e.CalIsRecoveryLowPercent,
        CalIsRecoveryHighPercent = e.CalIsRecoveryHighPercent,
        CalRequireBlank = e.CalRequireBlank,
        CalRequireIcv = e.CalRequireIcv,
        CalRequireCcv = e.CalRequireCcv,
        CalRequireInternalStandard = e.CalRequireInternalStandard,
        ReportedConcentrationBasis = e.ReportedConcentrationBasis,
        CalMaxRunAgeHours = e.CalMaxRunAgeHours,
        CalInstrumentType = e.CalInstrumentType,
        CalStandardLevelsMgPerL = e.CalStandardLevelsMgPerL,
        ReplicateCount = e.ReplicateCount,
        EvaluationBasis = e.EvaluationBasis,
        ConditionFields = e.ConditionFields,
        UsesTare = e.UsesTare,
        DissolutionS1Offset = e.DissolutionS1Offset,
        DissolutionS2MinOffset = e.DissolutionS2MinOffset,
        DissolutionS3MinOffset = e.DissolutionS3MinOffset,
        DissolutionS3MaxBelowS2Min = e.DissolutionS3MaxBelowS2Min,
        DisintegrationStage1Units = e.DisintegrationStage1Units,
        DisintegrationStage2Units = e.DisintegrationStage2Units,
        DisintegrationMaxStage1Failures = e.DisintegrationMaxStage1Failures,
        DisintegrationMinPassTotal = e.DisintegrationMinPassTotal,
        WvUnitCount = e.WvUnitCount,
        WvTabletBand1MaxMg = e.WvTabletBand1MaxMg,
        WvTabletBand1Percent = e.WvTabletBand1Percent,
        WvTabletBand2MaxMg = e.WvTabletBand2MaxMg,
        WvTabletBand2Percent = e.WvTabletBand2Percent,
        WvTabletBand3Percent = e.WvTabletBand3Percent,
        WvTabletMaxOutside = e.WvTabletMaxOutside,
        WvCapsuleInnerPercent = e.WvCapsuleInnerPercent,
        WvCapsuleOuterPercent = e.WvCapsuleOuterPercent,
        WvCapsuleS1MaxOutside = e.WvCapsuleS1MaxOutside,
        WvCapsuleS1MaxForRetest = e.WvCapsuleS1MaxForRetest,
        WvCapsuleS2ExtraUnits = e.WvCapsuleS2ExtraUnits,
        WvCapsuleS2MaxOutside = e.WvCapsuleS2MaxOutside,
        HplcMaxPreparationRsdPercent = e.HplcMaxPreparationRsdPercent,
        ResponseMode = e.ResponseMode,
        HplcMethodId = e.HplcMethodId,
    };
}

public class SpecificationResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int ItemId { get; init; }
    public string TestCode { get; init; } = null!;
    public string AlertLimit { get; init; } = null!;
    public string ActionLimit { get; init; } = null!;
    public string SpecLimit { get; init; } = null!;
    public string Unit { get; init; } = null!;
    public decimal? DilutionFactor { get; init; }
    public string ParameterName { get; init; } = null!;
    public int DisplayOrder { get; init; }
    public LimitType LimitType { get; init; }
    public string? ReferenceStandard { get; init; }
    public decimal? LowerLimit { get; init; }
    public decimal? UpperLimit { get; init; }
    public bool LowerInclusive { get; init; }
    public bool UpperInclusive { get; init; }
    public decimal? Target { get; init; }
    public decimal? Tolerance { get; init; }
    public ToleranceMode? ToleranceMode { get; init; }
    public string? ExpectedResultText { get; init; }
    public ExpectedPresence? ExpectedState { get; init; }
    public decimal? SampleQuantity { get; init; }
    public string? SampleQuantityUnit { get; init; }
    public int? TestAnalyteId { get; init; }
    public ResultBasis? ResultBasis { get; init; }
    public SampleMatrix? SampleMatrix { get; init; }
    public decimal? LabelClaim { get; init; }
    public string? LabelClaimUnit { get; init; }
    public decimal ConversionFactor { get; init; }
    public DosageForm? DosageForm { get; init; }
    public int? HplcMethodAnalyteId { get; init; }
    public List<SpecificationStageResponse> Stages { get; init; } = new();

    public static SpecificationResponse From(Specification e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        ItemId = e.ItemId,
        TestCode = e.TestCode,
        AlertLimit = e.AlertLimit,
        ActionLimit = e.ActionLimit,
        SpecLimit = e.SpecLimit,
        Unit = e.Unit,
        DilutionFactor = e.DilutionFactor,
        ParameterName = e.ParameterName,
        DisplayOrder = e.DisplayOrder,
        LimitType = e.LimitType,
        ReferenceStandard = e.ReferenceStandard,
        LowerLimit = e.LowerLimit,
        UpperLimit = e.UpperLimit,
        LowerInclusive = e.LowerInclusive,
        UpperInclusive = e.UpperInclusive,
        Target = e.Target,
        Tolerance = e.Tolerance,
        ToleranceMode = e.ToleranceMode,
        ExpectedResultText = e.ExpectedResultText,
        ExpectedState = e.ExpectedState,
        SampleQuantity = e.SampleQuantity,
        SampleQuantityUnit = e.SampleQuantityUnit,
        TestAnalyteId = e.TestAnalyteId,
        ResultBasis = e.ResultBasis,
        SampleMatrix = e.SampleMatrix,
        LabelClaim = e.LabelClaim,
        LabelClaimUnit = e.LabelClaimUnit,
        ConversionFactor = e.ConversionFactor,
        DosageForm = e.DosageForm,
        HplcMethodAnalyteId = e.HplcMethodAnalyteId,
        Stages = e.Stages.Select(SpecificationStageResponse.From).ToList(),
    };
}

public class SpecificationStageResponse
{
    public int Id { get; init; }
    public int SpecificationId { get; init; }
    public int StageNumber { get; init; }
    public string StageLabel { get; init; } = null!;
    public string AcceptanceCriteriaText { get; init; } = null!;

    public static SpecificationStageResponse From(SpecificationStage e) => new()
    {
        Id = e.Id,
        SpecificationId = e.SpecificationId,
        StageNumber = e.StageNumber,
        StageLabel = e.StageLabel,
        AcceptanceCriteriaText = e.AcceptanceCriteriaText,
    };
}

public class MediaProductResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;

    public static MediaProductResponse From(MediaProduct e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        Name = e.Name,
        Code = e.Code,
    };
}

public class MediaIncubationConditionResponse
{
    public int Id { get; init; }
    public uint Version { get; init; }
    public int MediaProductId { get; init; }
    public MediaProductResponse? MediaProduct { get; init; }
    public int IncubationMinHours { get; init; }
    public int IncubationMaxHours { get; init; }
    public decimal TemperatureMin { get; init; }
    public decimal TemperatureMax { get; init; }

    public static MediaIncubationConditionResponse From(MediaIncubationCondition e) => new()
    {
        Id = e.Id,
        Version = e.Version,
        MediaProductId = e.MediaProductId,
        MediaProduct = e.MediaProduct is null ? null : MediaProductResponse.From(e.MediaProduct),
        IncubationMinHours = e.IncubationMinHours,
        IncubationMaxHours = e.IncubationMaxHours,
        TemperatureMin = e.TemperatureMin,
        TemperatureMax = e.TemperatureMax,
    };
}
