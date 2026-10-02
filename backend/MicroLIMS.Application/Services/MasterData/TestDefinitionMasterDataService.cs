using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterData;

// Master data: test definition. Behind TestDefinitionMasterDataController
// (api/masterdata/...); the controller only maps HTTP to these calls.
public class TestDefinitionMasterDataService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public TestDefinitionMasterDataService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    // The canonical Code/DisplayName list backing every TestCode picker
    // in the app (Items, Water Sampling Points, Room Test Configurations,
    // Machine Part Configurations). See TestDefinition.cs for why this
    // exists.
    public async Task<List<TestDefinitionResponse>> GetTestDefinitionsAsync() =>
        (await _db.TestDefinitions.AsNoTracking().Include(t => t.Section).OrderBy(t => t.Code).ToListAsync()).Select(TestDefinitionResponse.From).ToList();

    public async Task<TestDefinitionResponse> CreateTestDefinitionAsync(int currentUserId, CreateTestDefinitionRequest request)
    {
        if (await _db.TestDefinitions.AnyAsync(t => t.Code == request.Code))
            throw new InvalidOperationException($"Test code \"{request.Code}\" already exists in the Test Master.");

        var userId = currentUserId;
        var sectionId = await _scope.ResolveSectionForCreateAsync(userId, request.SectionId);

        string? methodAbbr = string.IsNullOrWhiteSpace(request.MethodAbbreviation)
            ? null
            : request.MethodAbbreviation.Trim().ToUpperInvariant();

        if ((request.WorkflowType == WorkflowType.Disintegration || request.EquationType == EquationType.Disintegration) && request.RequiresSystemSuitability)
            throw new InvalidOperationException("Disintegration tests must not require system suitability.");

        if ((request.WorkflowType == WorkflowType.WeightVariation || request.EquationType == EquationType.WeightVariation) && request.RequiresSystemSuitability)
            throw new InvalidOperationException("Weight variation tests must not require system suitability.");

        if (request.RequiresSystemSuitability)
        {
            if (string.IsNullOrEmpty(methodAbbr))
                throw new InvalidOperationException("Method abbreviation is required when system suitability is enabled.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(methodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        }
        else if (request.EquationType == EquationType.CalibrationCurve)
        {
            if (request.WorkflowType != WorkflowType.ElementalAssay)
                throw new InvalidOperationException("Workflow type must be ElementalAssay when equation type is CalibrationCurve.");

            if (string.IsNullOrEmpty(methodAbbr))
                throw new InvalidOperationException("Method abbreviation is required when equation type is CalibrationCurve.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(methodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");

            if (!request.CalMinCorrelation.HasValue || request.CalMinCorrelation.Value <= 0m || request.CalMinCorrelation.Value > 1m)
                throw new InvalidOperationException("Minimum correlation must be in (0, 1] when equation type is CalibrationCurve.");

            if (!request.CalCorrelationType.HasValue)
                throw new InvalidOperationException("Correlation type is required when equation type is CalibrationCurve.");

            if (!request.CalMinStandards.HasValue || request.CalMinStandards.Value < 1)
                throw new InvalidOperationException("Minimum standards must be at least 1 when equation type is CalibrationCurve.");

            if (!request.CalCheckRecoveryLowPercent.HasValue || !request.CalCheckRecoveryHighPercent.HasValue)
                throw new InvalidOperationException("Both check recovery window bounds (low and high) are required when equation type is CalibrationCurve.");

            if (request.CalCheckRecoveryLowPercent.Value > request.CalCheckRecoveryHighPercent.Value)
                throw new InvalidOperationException("Check recovery low percent must be less than or equal to high percent.");

            if (request.CalRequireInternalStandard == true)
            {
                if (!request.CalIsRecoveryLowPercent.HasValue || !request.CalIsRecoveryHighPercent.HasValue)
                    throw new InvalidOperationException("Both internal standard recovery bounds (low and high) are required when internal standards are required.");

                if (request.CalIsRecoveryLowPercent.Value > request.CalIsRecoveryHighPercent.Value)
                    throw new InvalidOperationException("Internal standard recovery low percent must be less than or equal to high percent.");
            }
            else if (request.CalIsRecoveryLowPercent.HasValue && request.CalIsRecoveryHighPercent.HasValue &&
                request.CalIsRecoveryLowPercent.Value > request.CalIsRecoveryHighPercent.Value)
            {
                throw new InvalidOperationException("Internal standard recovery low percent must be less than or equal to high percent.");
            }

            if (!request.ReportedConcentrationBasis.HasValue)
                throw new InvalidOperationException("Reported concentration basis is required when equation type is CalibrationCurve.");
            if (request.ReportedConcentrationBasis != ReportedConcentrationBasis.SamplePpm)
                throw new InvalidOperationException("Only 'ppm in the sample' is supported: Syngistix applies weight, volume and dilution itself.");

            var maxAge = request.CalMaxRunAgeHours ?? 24;
            if (maxAge < 1)
                throw new InvalidOperationException("Maximum run age must be at least 1 hour when equation type is CalibrationCurve.");
        }
        else if (methodAbbr != null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(methodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        }

        // AAS reuses the CalibrationCurve path (D-A4): the instrument choice and standard
        // levels only make sense there, and only IcpOes/Aas are supported instrument families.
        if (request.CalInstrumentType.HasValue)
        {
            if (request.EquationType != EquationType.CalibrationCurve)
                throw new InvalidOperationException("Calibration instrument type only applies when equation type is CalibrationCurve.");

            if (request.CalInstrumentType.Value != EquipmentType.IcpOes && request.CalInstrumentType.Value != EquipmentType.Aas)
                throw new InvalidOperationException("Calibration instrument type must be IcpOes or Aas.");
        }

        string? normalizedCalLevels = null;
        if (!string.IsNullOrWhiteSpace(request.CalStandardLevelsMgPerL))
        {
            if (request.EquationType != EquationType.CalibrationCurve)
                throw new InvalidOperationException("Standard levels only apply when equation type is CalibrationCurve.");

            normalizedCalLevels = CalibrationStandardLevelsHelper.ParseAndValidate(request.CalStandardLevelsMgPerL).Normalized;
        }

        if (request.EquationType == EquationType.Measurement)
        {
            if (request.WorkflowType != WorkflowType.Measurement)
                throw new InvalidOperationException("Workflow type must be Measurement when equation type is Measurement.");

            if (!request.ReplicateCount.HasValue || request.ReplicateCount.Value < 1 || request.ReplicateCount.Value > 30)
                throw new InvalidOperationException("Replicate count must be between 1 and 30 when equation type is Measurement.");

            if (!request.EvaluationBasis.HasValue)
                throw new InvalidOperationException("Evaluation basis is required when equation type is Measurement.");
        }
        else if (request.WorkflowType == WorkflowType.Measurement)
        {
            if (request.EquationType != EquationType.Measurement)
                throw new InvalidOperationException("Equation type must be Measurement when workflow type is Measurement.");
        }

        if (request.ConditionFields != null && request.ConditionFields.Length > 500)
            throw new InvalidOperationException("Condition fields cannot exceed 500 characters.");

        if (request.EquationType is EquationType.GravimetricLoss or EquationType.GravimetricResidue)
        {
            if (request.WorkflowType != WorkflowType.Gravimetric)
                throw new InvalidOperationException($"Workflow type must be Gravimetric when equation type is {request.EquationType}.");

            if (!request.ReplicateCount.HasValue || request.ReplicateCount.Value < 1 || request.ReplicateCount.Value > 30)
                throw new InvalidOperationException($"Replicate count must be between 1 and 30 when equation type is {request.EquationType}.");
        }
        else if (request.WorkflowType == WorkflowType.Gravimetric)
        {
            if (request.EquationType is not (EquationType.GravimetricLoss or EquationType.GravimetricResidue))
                throw new InvalidOperationException("Equation type must be GravimetricLoss or GravimetricResidue when workflow type is Gravimetric.");

            if (!request.ReplicateCount.HasValue || request.ReplicateCount.Value < 1 || request.ReplicateCount.Value > 30)
                throw new InvalidOperationException($"Replicate count must be between 1 and 30 when equation type is {request.EquationType}.");
        }

        if (request.EquationType == EquationType.Qualitative)
        {
            if (request.WorkflowType != WorkflowType.Qualitative)
                throw new InvalidOperationException("Workflow type must be Qualitative when equation type is Qualitative.");
        }
        else if (request.WorkflowType == WorkflowType.Qualitative)
        {
            if (request.EquationType != EquationType.Qualitative)
                throw new InvalidOperationException("Equation type must be Qualitative when workflow type is Qualitative.");
        }

        if (request.EquationType == EquationType.Dissolution)
        {
            if (request.WorkflowType != WorkflowType.Dissolution)
                throw new InvalidOperationException("Workflow type must be Dissolution when equation type is Dissolution.");
        }
        else if (request.WorkflowType == WorkflowType.Dissolution)
        {
            if (request.EquationType != EquationType.Dissolution)
                throw new InvalidOperationException("Equation type must be Dissolution when workflow type is Dissolution.");
        }

        if (request.WorkflowType == WorkflowType.Dissolution)
        {
            if (!request.RequiresSystemSuitability)
                throw new InvalidOperationException("Dissolution tests must require system suitability: the standard comes from the HPLC run the sample is assigned to.");

            decimal s1 = request.DissolutionS1Offset ?? 5m;
            decimal s2 = request.DissolutionS2MinOffset ?? 15m;
            decimal s3 = request.DissolutionS3MinOffset ?? 25m;
            decimal maxBelow = request.DissolutionS3MaxBelowS2Min ?? 2m;

            if (s1 < 0 || s2 < 0 || s3 < 0 || maxBelow < 0)
                throw new InvalidOperationException("Dissolution stage offsets must be greater than or equal to zero.");
        }

        if (request.EquationType == EquationType.Disintegration)
        {
            if (request.WorkflowType != WorkflowType.Disintegration)
                throw new InvalidOperationException("Workflow type must be Disintegration when equation type is Disintegration.");
        }
        else if (request.WorkflowType == WorkflowType.Disintegration)
        {
            if (request.EquationType != EquationType.Disintegration)
                throw new InvalidOperationException("Equation type must be Disintegration when workflow type is Disintegration.");
        }

        if (request.WorkflowType == WorkflowType.Disintegration)
        {
            if (request.RequiresSystemSuitability)
                throw new InvalidOperationException("Disintegration tests must not require system suitability.");

            int s1 = request.DisintegrationStage1Units ?? 6;
            int s2 = request.DisintegrationStage2Units ?? 12;
            int maxFail = request.DisintegrationMaxStage1Failures ?? 2;
            int minPass = request.DisintegrationMinPassTotal ?? 16;

            if (s1 < 1 || s2 < 1)
                throw new InvalidOperationException("Disintegration stage units must be greater than or equal to 1.");
            if (maxFail < 0 || maxFail >= s1)
                throw new InvalidOperationException($"Disintegration maximum Stage 1 failures must be between 0 and {s1 - 1}.");
            if (minPass < 1 || minPass > (s1 + s2))
                throw new InvalidOperationException($"Disintegration minimum pass total must be between 1 and {s1 + s2}.");
        }

        if (request.EquationType == EquationType.WeightVariation)
        {
            if (request.WorkflowType != WorkflowType.WeightVariation)
                throw new InvalidOperationException("Workflow type must be WeightVariation when equation type is WeightVariation.");
        }
        else if (request.WorkflowType == WorkflowType.WeightVariation)
        {
            if (request.EquationType != EquationType.WeightVariation)
                throw new InvalidOperationException("Equation type must be WeightVariation when workflow type is WeightVariation.");
        }

        if (request.WorkflowType == WorkflowType.WeightVariation)
        {
            if (request.RequiresSystemSuitability)
                throw new InvalidOperationException("Weight variation tests must not require system suitability.");

            int unitCount = request.WvUnitCount ?? 20;
            decimal band1Mg = request.WvTabletBand1MaxMg ?? 130m;
            decimal band1Pct = request.WvTabletBand1Percent ?? 10m;
            decimal band2Mg = request.WvTabletBand2MaxMg ?? 324m;
            decimal band2Pct = request.WvTabletBand2Percent ?? 7.5m;
            decimal band3Pct = request.WvTabletBand3Percent ?? 5m;
            int tabMaxOutside = request.WvTabletMaxOutside ?? 2;

            decimal capInnerPct = request.WvCapsuleInnerPercent ?? 10m;
            decimal capOuterPct = request.WvCapsuleOuterPercent ?? 25m;
            int capS1MaxOutside = request.WvCapsuleS1MaxOutside ?? 2;
            int capS1MaxRetest = request.WvCapsuleS1MaxForRetest ?? 6;
            int capS2Extra = request.WvCapsuleS2ExtraUnits ?? 40;
            int capS2MaxOutside = request.WvCapsuleS2MaxOutside ?? 6;

            if (unitCount < 1 || capS2Extra < 1)
                throw new InvalidOperationException("Weight variation unit count and extra units must be greater than or equal to 1.");
            if (tabMaxOutside < 0 || capS1MaxOutside < 0 || capS2MaxOutside < 0)
                throw new InvalidOperationException("Weight variation maximum outside counts must be greater than or equal to 0.");
            if (band1Mg <= 0m || band2Mg <= 0m)
                throw new InvalidOperationException("Weight variation tablet band weight limits must be greater than zero.");
            if (band1Pct <= 0m || band2Pct <= 0m || band3Pct <= 0m || capInnerPct <= 0m || capOuterPct <= 0m)
                throw new InvalidOperationException("Weight variation percentages must be greater than zero.");
            if (band1Mg >= band2Mg)
                throw new InvalidOperationException("Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.");
            if (capInnerPct >= capOuterPct)
                throw new InvalidOperationException("Weight variation capsule inner percentage must be less than outer percentage.");
            if (capS1MaxOutside >= capS1MaxRetest || capS1MaxRetest > unitCount)
                throw new InvalidOperationException("Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.");
            if (capS2MaxOutside >= unitCount + capS2Extra)
                throw new InvalidOperationException($"Weight variation capsule Stage 2 max outside must be less than total units ({unitCount + capS2Extra}).");
        }

        // Retired: the old HPLC types and Standard-Comparison are replaced by HPLC method assay.
        if (request.WorkflowType is WorkflowType.HplcAssay or WorkflowType.HplcMultiAnalyte or WorkflowType.StandardComparison
            || request.EquationType is EquationType.HplcAssay or EquationType.HplcMultiAnalyte or EquationType.StandardComparison)
            throw new InvalidOperationException("HPLC Assay, HPLC Multi-Analyte and Standard-Comparison are retired; use HPLC method assay.");

        if (request.RequiresSystemSuitability && request.WorkflowType is not (WorkflowType.HplcMethodAssay or WorkflowType.Dissolution))
            throw new InvalidOperationException("Only HPLC method assay and dissolution tests can require system suitability.");

        // HPLC chain S3: HplcMethodAssay tests carry no analytes/SST criteria of
        // their own - everything comes from the linked HplcMethod (spec 3.4).
        if (request.EquationType == EquationType.HplcMethodAssay)
        {
            if (request.WorkflowType != WorkflowType.HplcMethodAssay)
                throw new InvalidOperationException("Workflow type must be HplcMethodAssay when equation type is HplcMethodAssay.");
        }
        else if (request.WorkflowType == WorkflowType.HplcMethodAssay)
        {
            if (request.EquationType != EquationType.HplcMethodAssay)
                throw new InvalidOperationException("Equation type must be HplcMethodAssay when workflow type is HplcMethodAssay.");
        }

        if (request.WorkflowType == WorkflowType.HplcMethodAssay)
        {
            if (!request.RequiresSystemSuitability)
                throw new InvalidOperationException("HPLC method assay tests must require system suitability.");

            if (!request.HplcMethodId.HasValue)
                throw new InvalidOperationException("HPLC method is required for HPLC method assay tests.");

            var hplcMethod = await _db.HplcMethods.FirstOrDefaultAsync(m => m.Id == request.HplcMethodId.Value)
                ?? throw new InvalidOperationException("HPLC method not found.");
            if (hplcMethod.SectionId != sectionId)
                throw new InvalidOperationException("The HPLC method belongs to another laboratory.");
            if (!hplcMethod.IsActive)
                throw new InvalidOperationException("The HPLC method is inactive.");
        }
        else if (request.WorkflowType == WorkflowType.Dissolution && request.HplcMethodId.HasValue)
        {
            // Optional for dissolution: the workspace run of this method supplies the standard.
            var hplcMethod = await _db.HplcMethods.FirstOrDefaultAsync(m => m.Id == request.HplcMethodId.Value)
                ?? throw new InvalidOperationException("HPLC method not found.");
            if (hplcMethod.SectionId != sectionId)
                throw new InvalidOperationException("The HPLC method belongs to another laboratory.");
            if (!hplcMethod.IsActive)
                throw new InvalidOperationException("The HPLC method is inactive.");
        }
        else if (request.HplcMethodId.HasValue)
        {
            throw new InvalidOperationException("HPLC method is only allowed for HPLC method assay and dissolution tests.");
        }

        var entity = new TestDefinition
        {
            Code = request.Code,
            DisplayName = request.DisplayName,
            SectionId = sectionId,
            WorkflowType = request.WorkflowType,
            EquationType = request.EquationType,
            RequiresSystemSuitability = request.RequiresSystemSuitability,
            MethodAbbreviation = methodAbbr,
            SstMaxRsdPercent = request.SstMaxRsdPercent,
            SstMinResolution = request.SstMinResolution,
            SstMaxTailingFactor = request.SstMaxTailingFactor,
            SstMinTheoreticalPlates = request.SstMinTheoreticalPlates,
            CalibrationEntryMode = request.CalibrationEntryMode,
            CalMinCorrelation = request.CalMinCorrelation,
            CalCorrelationType = request.CalCorrelationType,
            CalMinStandards = request.CalMinStandards,
            CalCheckRecoveryLowPercent = request.CalCheckRecoveryLowPercent,
            CalCheckRecoveryHighPercent = request.CalCheckRecoveryHighPercent,
            CalBlankMax = request.CalBlankMax,
            CalIsRecoveryLowPercent = request.CalIsRecoveryLowPercent,
            CalIsRecoveryHighPercent = request.CalIsRecoveryHighPercent,
            CalRequireBlank = request.CalRequireBlank,
            CalRequireIcv = request.CalRequireIcv,
            CalRequireCcv = request.CalRequireCcv,
            CalRequireInternalStandard = request.CalRequireInternalStandard,
            ReportedConcentrationBasis = request.ReportedConcentrationBasis,
            CalMaxRunAgeHours = request.CalMaxRunAgeHours ?? 24,
            CalInstrumentType = request.CalInstrumentType,
            CalStandardLevelsMgPerL = normalizedCalLevels,
            ReplicateCount = request.ReplicateCount,
            EvaluationBasis = request.EvaluationBasis,
            ConditionFields = request.ConditionFields,
            UsesTare = request.WorkflowType == WorkflowType.Gravimetric ? (request.UsesTare ?? false) : request.UsesTare,
            DissolutionS1Offset = request.WorkflowType == WorkflowType.Dissolution ? (request.DissolutionS1Offset ?? 5m) : request.DissolutionS1Offset,
            DissolutionS2MinOffset = request.WorkflowType == WorkflowType.Dissolution ? (request.DissolutionS2MinOffset ?? 15m) : request.DissolutionS2MinOffset,
            DissolutionS3MinOffset = request.WorkflowType == WorkflowType.Dissolution ? (request.DissolutionS3MinOffset ?? 25m) : request.DissolutionS3MinOffset,
            DissolutionS3MaxBelowS2Min = request.WorkflowType == WorkflowType.Dissolution ? (request.DissolutionS3MaxBelowS2Min ?? 2m) : request.DissolutionS3MaxBelowS2Min,
            DisintegrationStage1Units = request.WorkflowType == WorkflowType.Disintegration ? (request.DisintegrationStage1Units ?? 6) : request.DisintegrationStage1Units,
            DisintegrationStage2Units = request.WorkflowType == WorkflowType.Disintegration ? (request.DisintegrationStage2Units ?? 12) : request.DisintegrationStage2Units,
            DisintegrationMaxStage1Failures = request.WorkflowType == WorkflowType.Disintegration ? (request.DisintegrationMaxStage1Failures ?? 2) : request.DisintegrationMaxStage1Failures,
            DisintegrationMinPassTotal = request.WorkflowType == WorkflowType.Disintegration ? (request.DisintegrationMinPassTotal ?? 16) : request.DisintegrationMinPassTotal,
            WvUnitCount = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvUnitCount ?? 20) : request.WvUnitCount,
            WvTabletBand1MaxMg = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand1MaxMg ?? 130m) : request.WvTabletBand1MaxMg,
            WvTabletBand1Percent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand1Percent ?? 10m) : request.WvTabletBand1Percent,
            WvTabletBand2MaxMg = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand2MaxMg ?? 324m) : request.WvTabletBand2MaxMg,
            WvTabletBand2Percent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand2Percent ?? 7.5m) : request.WvTabletBand2Percent,
            WvTabletBand3Percent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletBand3Percent ?? 5m) : request.WvTabletBand3Percent,
            WvTabletMaxOutside = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvTabletMaxOutside ?? 2) : request.WvTabletMaxOutside,
            WvCapsuleInnerPercent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleInnerPercent ?? 10m) : request.WvCapsuleInnerPercent,
            WvCapsuleOuterPercent = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleOuterPercent ?? 25m) : request.WvCapsuleOuterPercent,
            WvCapsuleS1MaxOutside = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleS1MaxOutside ?? 2) : request.WvCapsuleS1MaxOutside,
            WvCapsuleS1MaxForRetest = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleS1MaxForRetest ?? 6) : request.WvCapsuleS1MaxForRetest,
            WvCapsuleS2ExtraUnits = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleS2ExtraUnits ?? 40) : request.WvCapsuleS2ExtraUnits,
            WvCapsuleS2MaxOutside = request.WorkflowType == WorkflowType.WeightVariation ? (request.WvCapsuleS2MaxOutside ?? 6) : request.WvCapsuleS2MaxOutside,
            HplcMethodId = request.WorkflowType is WorkflowType.HplcMethodAssay or WorkflowType.Dissolution ? request.HplcMethodId : null
        };
        _db.TestDefinitions.Add(entity);
        await _db.SaveChangesAsync();
        return TestDefinitionResponse.From(entity);
    }

    public async Task<TestDefinitionResponse> UpdateTestDefinitionAsync(int currentUserId, int id, UpdateTestDefinitionRequest request)
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Test {id} not found.");
        RecordVersion.EnsureCurrent(_db, entity);

        if (await _db.TestDefinitions.AnyAsync(t => t.Code == request.Code && t.Id != id))
            throw new InvalidOperationException($"Test code \"{request.Code}\" already exists in the Test Master.");

        // Only a member of the test's section may change it, and only into a
        // section they belong to.
        var scope = await _scope.GetAccessibleSectionIdsAsync(currentUserId);
        if (scope is not null && !scope.Contains(entity.SectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        if (request.SectionId.HasValue && request.SectionId.Value != entity.SectionId)
            entity.SectionId = await _scope.ResolveSectionForCreateAsync(currentUserId, request.SectionId);

        var effectiveRequiresSst = request.RequiresSystemSuitability ?? entity.RequiresSystemSuitability;
        var effectiveEquationType = request.EquationType ?? entity.EquationType;
        var effectiveWorkflowType = request.WorkflowType ?? entity.WorkflowType;
        var effectiveMethodAbbr = request.MethodAbbreviation != null
            ? (string.IsNullOrWhiteSpace(request.MethodAbbreviation) ? null : request.MethodAbbreviation.Trim().ToUpperInvariant())
            : entity.MethodAbbreviation;

        var effectiveCalMinCorr = request.CalMinCorrelation ?? entity.CalMinCorrelation;
        var effectiveCalCorrType = request.CalCorrelationType ?? entity.CalCorrelationType;
        var effectiveCalMinStds = request.CalMinStandards ?? entity.CalMinStandards;
        var effectiveCalRecLow = request.CalCheckRecoveryLowPercent ?? entity.CalCheckRecoveryLowPercent;
        var effectiveCalRecHigh = request.CalCheckRecoveryHighPercent ?? entity.CalCheckRecoveryHighPercent;
        var effectiveCalIsLow = request.CalIsRecoveryLowPercent ?? entity.CalIsRecoveryLowPercent;
        var effectiveCalIsHigh = request.CalIsRecoveryHighPercent ?? entity.CalIsRecoveryHighPercent;
        var effectiveCalRequireIs = request.CalRequireInternalStandard ?? entity.CalRequireInternalStandard;
        var effectiveBasis = request.ReportedConcentrationBasis ?? entity.ReportedConcentrationBasis;
        var effectiveMaxAge = request.CalMaxRunAgeHours ?? entity.CalMaxRunAgeHours ?? 24;
        var effectiveReplicateCount = request.ReplicateCount ?? entity.ReplicateCount;
        var effectiveEvaluationBasis = request.EvaluationBasis ?? entity.EvaluationBasis;
        var effectiveConditionFields = request.ConditionFields ?? entity.ConditionFields;
        var effectiveUsesTare = request.UsesTare ?? entity.UsesTare;

        if (effectiveConditionFields != null && effectiveConditionFields.Length > 500)
            throw new InvalidOperationException("Condition fields cannot exceed 500 characters.");

        if ((effectiveWorkflowType == WorkflowType.Disintegration || effectiveEquationType == EquationType.Disintegration) && effectiveRequiresSst)
            throw new InvalidOperationException("Disintegration tests must not require system suitability.");

        if ((effectiveWorkflowType == WorkflowType.WeightVariation || effectiveEquationType == EquationType.WeightVariation) && effectiveRequiresSst)
            throw new InvalidOperationException("Weight variation tests must not require system suitability.");

        if (effectiveRequiresSst)
        {
            if (string.IsNullOrEmpty(effectiveMethodAbbr))
                throw new InvalidOperationException("Method abbreviation is required when system suitability is enabled.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(effectiveMethodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        }
        else if (effectiveEquationType == EquationType.CalibrationCurve)
        {
            if (effectiveWorkflowType != WorkflowType.ElementalAssay)
                throw new InvalidOperationException("Workflow type must be ElementalAssay when equation type is CalibrationCurve.");

            if (string.IsNullOrEmpty(effectiveMethodAbbr))
                throw new InvalidOperationException("Method abbreviation is required when equation type is CalibrationCurve.");

            if (!System.Text.RegularExpressions.Regex.IsMatch(effectiveMethodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");

            if (!effectiveCalMinCorr.HasValue || effectiveCalMinCorr.Value <= 0m || effectiveCalMinCorr.Value > 1m)
                throw new InvalidOperationException("Minimum correlation must be in (0, 1] when equation type is CalibrationCurve.");

            if (!effectiveCalCorrType.HasValue)
                throw new InvalidOperationException("Correlation type is required when equation type is CalibrationCurve.");

            if (!effectiveCalMinStds.HasValue || effectiveCalMinStds.Value < 1)
                throw new InvalidOperationException("Minimum standards must be at least 1 when equation type is CalibrationCurve.");

            if (!effectiveCalRecLow.HasValue || !effectiveCalRecHigh.HasValue)
                throw new InvalidOperationException("Both check recovery window bounds (low and high) are required when equation type is CalibrationCurve.");

            if (effectiveCalRecLow.Value > effectiveCalRecHigh.Value)
                throw new InvalidOperationException("Check recovery low percent must be less than or equal to high percent.");

            if (effectiveCalRequireIs == true)
            {
                if (!effectiveCalIsLow.HasValue || !effectiveCalIsHigh.HasValue)
                    throw new InvalidOperationException("Both internal standard recovery bounds (low and high) are required when internal standards are required.");

                if (effectiveCalIsLow.Value > effectiveCalIsHigh.Value)
                    throw new InvalidOperationException("Internal standard recovery low percent must be less than or equal to high percent.");
            }
            else if (effectiveCalIsLow.HasValue && effectiveCalIsHigh.HasValue && effectiveCalIsLow.Value > effectiveCalIsHigh.Value)
            {
                throw new InvalidOperationException("Internal standard recovery low percent must be less than or equal to high percent.");
            }

            if (!effectiveBasis.HasValue)
                throw new InvalidOperationException("Reported concentration basis is required when equation type is CalibrationCurve.");
            if (effectiveBasis != ReportedConcentrationBasis.SamplePpm)
                throw new InvalidOperationException("Only 'ppm in the sample' is supported: Syngistix applies weight, volume and dilution itself.");

            if (effectiveMaxAge < 1)
                throw new InvalidOperationException("Maximum run age must be at least 1 hour when equation type is CalibrationCurve.");
        }
        else if (effectiveMethodAbbr != null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(effectiveMethodAbbr, "^[A-Z0-9-]{1,20}$"))
                throw new InvalidOperationException("Method abbreviation must be 1-20 uppercase alphanumeric characters or hyphens.");
        }

        // AAS reuses the CalibrationCurve path (D-A4): the instrument choice and standard
        // levels only make sense there, and only IcpOes/Aas are supported instrument families.
        var effectiveCalInstrumentType = request.CalInstrumentType ?? entity.CalInstrumentType;
        if (effectiveCalInstrumentType.HasValue)
        {
            if (effectiveEquationType != EquationType.CalibrationCurve)
                throw new InvalidOperationException("Calibration instrument type only applies when equation type is CalibrationCurve.");

            if (effectiveCalInstrumentType.Value != EquipmentType.IcpOes && effectiveCalInstrumentType.Value != EquipmentType.Aas)
                throw new InvalidOperationException("Calibration instrument type must be IcpOes or Aas.");
        }

        // Empty string clears the levels (explicit "no levels configured"); null (the
        // default) means keep the existing value - the same convention as every other
        // Cal* field, but strings need an explicit marker to distinguish "clear" from "keep".
        string? normalizedCalLevels = entity.CalStandardLevelsMgPerL;
        if (request.CalStandardLevelsMgPerL != null)
        {
            normalizedCalLevels = string.IsNullOrWhiteSpace(request.CalStandardLevelsMgPerL)
                ? null
                : CalibrationStandardLevelsHelper.ParseAndValidate(request.CalStandardLevelsMgPerL).Normalized;
        }

        if (normalizedCalLevels != null && effectiveEquationType != EquationType.CalibrationCurve)
            throw new InvalidOperationException("Standard levels only apply when equation type is CalibrationCurve.");

        if (effectiveEquationType == EquationType.Measurement)
        {
            if (effectiveWorkflowType != WorkflowType.Measurement)
                throw new InvalidOperationException("Workflow type must be Measurement when equation type is Measurement.");

            if (!effectiveReplicateCount.HasValue || effectiveReplicateCount.Value < 1 || effectiveReplicateCount.Value > 30)
                throw new InvalidOperationException("Replicate count must be between 1 and 30 when equation type is Measurement.");

            if (!effectiveEvaluationBasis.HasValue)
                throw new InvalidOperationException("Evaluation basis is required when equation type is Measurement.");
        }
        else if (effectiveWorkflowType == WorkflowType.Measurement)
        {
            if (effectiveEquationType != EquationType.Measurement)
                throw new InvalidOperationException("Equation type must be Measurement when workflow type is Measurement.");
        }

        if (effectiveEquationType is EquationType.GravimetricLoss or EquationType.GravimetricResidue)
        {
            if (effectiveWorkflowType != WorkflowType.Gravimetric)
                throw new InvalidOperationException($"Workflow type must be Gravimetric when equation type is {effectiveEquationType}.");

            if (!effectiveReplicateCount.HasValue || effectiveReplicateCount.Value < 1 || effectiveReplicateCount.Value > 30)
                throw new InvalidOperationException($"Replicate count must be between 1 and 30 when equation type is {effectiveEquationType}.");
        }
        else if (effectiveWorkflowType == WorkflowType.Gravimetric)
        {
            if (effectiveEquationType is not (EquationType.GravimetricLoss or EquationType.GravimetricResidue))
                throw new InvalidOperationException("Equation type must be GravimetricLoss or GravimetricResidue when workflow type is Gravimetric.");

            if (!effectiveReplicateCount.HasValue || effectiveReplicateCount.Value < 1 || effectiveReplicateCount.Value > 30)
                throw new InvalidOperationException($"Replicate count must be between 1 and 30 when equation type is {effectiveEquationType}.");
        }

        if (effectiveEquationType == EquationType.Qualitative)
        {
            if (effectiveWorkflowType != WorkflowType.Qualitative)
                throw new InvalidOperationException("Workflow type must be Qualitative when equation type is Qualitative.");
        }
        else if (effectiveWorkflowType == WorkflowType.Qualitative)
        {
            if (effectiveEquationType != EquationType.Qualitative)
                throw new InvalidOperationException("Equation type must be Qualitative when workflow type is Qualitative.");
        }

        if (effectiveEquationType == EquationType.Dissolution)
        {
            if (effectiveWorkflowType != WorkflowType.Dissolution)
                throw new InvalidOperationException("Workflow type must be Dissolution when equation type is Dissolution.");
        }
        else if (effectiveWorkflowType == WorkflowType.Dissolution)
        {
            if (effectiveEquationType != EquationType.Dissolution)
                throw new InvalidOperationException("Equation type must be Dissolution when workflow type is Dissolution.");
        }

        if (effectiveWorkflowType == WorkflowType.Dissolution)
        {
            if (!effectiveRequiresSst)
                throw new InvalidOperationException("Dissolution tests must require system suitability: the standard comes from the HPLC run the sample is assigned to.");

            var effectiveS1 = request.DissolutionS1Offset ?? entity.DissolutionS1Offset;
            var effectiveS2 = request.DissolutionS2MinOffset ?? entity.DissolutionS2MinOffset;
            var effectiveS3 = request.DissolutionS3MinOffset ?? entity.DissolutionS3MinOffset;
            var effectiveMaxBelow = request.DissolutionS3MaxBelowS2Min ?? entity.DissolutionS3MaxBelowS2Min;

            if (!effectiveS1.HasValue || !effectiveS2.HasValue || !effectiveS3.HasValue || !effectiveMaxBelow.HasValue)
                throw new InvalidOperationException("Dissolution stage offsets are required for Dissolution tests.");

            if (effectiveS1.Value < 0 || effectiveS2.Value < 0 || effectiveS3.Value < 0 || effectiveMaxBelow.Value < 0)
                throw new InvalidOperationException("Dissolution stage offsets must be greater than or equal to zero.");
        }

        if (effectiveEquationType == EquationType.Disintegration)
        {
            if (effectiveWorkflowType != WorkflowType.Disintegration)
                throw new InvalidOperationException("Workflow type must be Disintegration when equation type is Disintegration.");
        }
        else if (effectiveWorkflowType == WorkflowType.Disintegration)
        {
            if (effectiveEquationType != EquationType.Disintegration)
                throw new InvalidOperationException("Equation type must be Disintegration when workflow type is Disintegration.");
        }

        if (effectiveWorkflowType == WorkflowType.Disintegration)
        {
            if (effectiveRequiresSst)
                throw new InvalidOperationException("Disintegration tests must not require system suitability.");

            var effectiveS1 = request.DisintegrationStage1Units ?? entity.DisintegrationStage1Units ?? 6;
            var effectiveS2 = request.DisintegrationStage2Units ?? entity.DisintegrationStage2Units ?? 12;
            var effectiveMaxF1 = request.DisintegrationMaxStage1Failures ?? entity.DisintegrationMaxStage1Failures ?? 2;
            var effectiveMinPass = request.DisintegrationMinPassTotal ?? entity.DisintegrationMinPassTotal ?? 16;

            if (effectiveS1 < 1 || effectiveS2 < 1)
                throw new InvalidOperationException("Disintegration stage units must be greater than or equal to 1.");
            if (effectiveMaxF1 < 0 || effectiveMaxF1 >= effectiveS1)
                throw new InvalidOperationException($"Disintegration maximum Stage 1 failures must be between 0 and {effectiveS1 - 1}.");
            if (effectiveMinPass < 1 || effectiveMinPass > effectiveS1 + effectiveS2)
                throw new InvalidOperationException($"Disintegration minimum pass total must be between 1 and {effectiveS1 + effectiveS2}.");
        }

        if (effectiveEquationType == EquationType.WeightVariation)
        {
            if (effectiveWorkflowType != WorkflowType.WeightVariation)
                throw new InvalidOperationException("Workflow type must be WeightVariation when equation type is WeightVariation.");
        }
        else if (effectiveWorkflowType == WorkflowType.WeightVariation)
        {
            if (effectiveEquationType != EquationType.WeightVariation)
                throw new InvalidOperationException("Equation type must be WeightVariation when workflow type is WeightVariation.");
        }

        if (effectiveWorkflowType == WorkflowType.WeightVariation)
        {
            if (effectiveRequiresSst)
                throw new InvalidOperationException("Weight variation tests must not require system suitability.");

            var effectiveUnitCount = request.WvUnitCount ?? entity.WvUnitCount ?? 20;
            var effectiveBand1Mg = request.WvTabletBand1MaxMg ?? entity.WvTabletBand1MaxMg ?? 130m;
            var effectiveBand1Pct = request.WvTabletBand1Percent ?? entity.WvTabletBand1Percent ?? 10m;
            var effectiveBand2Mg = request.WvTabletBand2MaxMg ?? entity.WvTabletBand2MaxMg ?? 324m;
            var effectiveBand2Pct = request.WvTabletBand2Percent ?? entity.WvTabletBand2Percent ?? 7.5m;
            var effectiveBand3Pct = request.WvTabletBand3Percent ?? entity.WvTabletBand3Percent ?? 5m;
            var effectiveTabMaxOutside = request.WvTabletMaxOutside ?? entity.WvTabletMaxOutside ?? 2;

            var effectiveCapInnerPct = request.WvCapsuleInnerPercent ?? entity.WvCapsuleInnerPercent ?? 10m;
            var effectiveCapOuterPct = request.WvCapsuleOuterPercent ?? entity.WvCapsuleOuterPercent ?? 25m;
            var effectiveCapS1MaxOutside = request.WvCapsuleS1MaxOutside ?? entity.WvCapsuleS1MaxOutside ?? 2;
            var effectiveCapS1MaxRetest = request.WvCapsuleS1MaxForRetest ?? entity.WvCapsuleS1MaxForRetest ?? 6;
            var effectiveCapS2Extra = request.WvCapsuleS2ExtraUnits ?? entity.WvCapsuleS2ExtraUnits ?? 40;
            var effectiveCapS2MaxOutside = request.WvCapsuleS2MaxOutside ?? entity.WvCapsuleS2MaxOutside ?? 6;

            if (effectiveUnitCount < 1 || effectiveCapS2Extra < 1)
                throw new InvalidOperationException("Weight variation unit count and extra units must be greater than or equal to 1.");
            if (effectiveTabMaxOutside < 0 || effectiveCapS1MaxOutside < 0 || effectiveCapS2MaxOutside < 0)
                throw new InvalidOperationException("Weight variation maximum outside counts must be greater than or equal to 0.");
            if (effectiveBand1Mg <= 0m || effectiveBand2Mg <= 0m)
                throw new InvalidOperationException("Weight variation tablet band weight limits must be greater than zero.");
            if (effectiveBand1Pct <= 0m || effectiveBand2Pct <= 0m || effectiveBand3Pct <= 0m || effectiveCapInnerPct <= 0m || effectiveCapOuterPct <= 0m)
                throw new InvalidOperationException("Weight variation percentages must be greater than zero.");
            if (effectiveBand1Mg >= effectiveBand2Mg)
                throw new InvalidOperationException("Weight variation Tablet Band 1 Max Mg must be less than Band 2 Max Mg.");
            if (effectiveCapInnerPct >= effectiveCapOuterPct)
                throw new InvalidOperationException("Weight variation capsule inner percentage must be less than outer percentage.");
            if (effectiveCapS1MaxOutside >= effectiveCapS1MaxRetest || effectiveCapS1MaxRetest > effectiveUnitCount)
                throw new InvalidOperationException("Weight variation capsule Stage 1 max outside must be less than Stage 1 max for retest, which must be less than or equal to unit count.");
            if (effectiveCapS2MaxOutside >= effectiveUnitCount + effectiveCapS2Extra)
                throw new InvalidOperationException($"Weight variation capsule Stage 2 max outside must be less than total units ({effectiveUnitCount + effectiveCapS2Extra}).");
        }

        // Retired: the old HPLC types and Standard-Comparison are replaced by HPLC method assay.
        if ((request.WorkflowType is WorkflowType.HplcAssay or WorkflowType.HplcMultiAnalyte or WorkflowType.StandardComparison)
            || (request.EquationType is EquationType.HplcAssay or EquationType.HplcMultiAnalyte or EquationType.StandardComparison))
            throw new InvalidOperationException("HPLC Assay, HPLC Multi-Analyte and Standard-Comparison are retired; use HPLC method assay.");

        if (effectiveRequiresSst && effectiveWorkflowType is not (WorkflowType.HplcMethodAssay or WorkflowType.Dissolution))
            throw new InvalidOperationException("Only HPLC method assay and dissolution tests can require system suitability.");

        // HPLC chain S3: HplcMethodAssay tests carry no analytes/SST criteria of
        // their own - everything comes from the linked HplcMethod (spec 3.4).
        if (effectiveEquationType == EquationType.HplcMethodAssay)
        {
            if (effectiveWorkflowType != WorkflowType.HplcMethodAssay)
                throw new InvalidOperationException("Workflow type must be HplcMethodAssay when equation type is HplcMethodAssay.");
        }
        else if (effectiveWorkflowType == WorkflowType.HplcMethodAssay)
        {
            if (effectiveEquationType != EquationType.HplcMethodAssay)
                throw new InvalidOperationException("Equation type must be HplcMethodAssay when workflow type is HplcMethodAssay.");
        }

        var effectiveHplcMethodId = request.HplcMethodId ?? entity.HplcMethodId;
        if (effectiveWorkflowType == WorkflowType.HplcMethodAssay)
        {
            if (!effectiveRequiresSst)
                throw new InvalidOperationException("HPLC method assay tests must require system suitability.");

            if (!effectiveHplcMethodId.HasValue)
                throw new InvalidOperationException("HPLC method is required for HPLC method assay tests.");

            var hplcMethod = await _db.HplcMethods.FirstOrDefaultAsync(m => m.Id == effectiveHplcMethodId.Value)
                ?? throw new InvalidOperationException("HPLC method not found.");
            if (hplcMethod.SectionId != entity.SectionId)
                throw new InvalidOperationException("The HPLC method belongs to another laboratory.");

            // Active is only enforced when the method is actually changing -
            // an existing test keeps working if its method is later deactivated.
            var methodChanged = effectiveHplcMethodId != entity.HplcMethodId;
            if (!hplcMethod.IsActive && methodChanged)
                throw new InvalidOperationException("The HPLC method is inactive.");
        }
        else if (effectiveWorkflowType == WorkflowType.Dissolution && effectiveHplcMethodId.HasValue)
        {
            var hplcMethod = await _db.HplcMethods.FirstOrDefaultAsync(m => m.Id == effectiveHplcMethodId.Value)
                ?? throw new InvalidOperationException("HPLC method not found.");
            if (hplcMethod.SectionId != entity.SectionId)
                throw new InvalidOperationException("The HPLC method belongs to another laboratory.");
            if (!hplcMethod.IsActive && effectiveHplcMethodId != entity.HplcMethodId)
                throw new InvalidOperationException("The HPLC method is inactive.");
        }
        else if (effectiveHplcMethodId.HasValue)
        {
            throw new InvalidOperationException("HPLC method is only allowed for HPLC method assay and dissolution tests.");
        }

        entity.Code = request.Code;
        entity.DisplayName = request.DisplayName;
        if (request.WorkflowType.HasValue) entity.WorkflowType = request.WorkflowType.Value;
        if (request.EquationType.HasValue) entity.EquationType = request.EquationType.Value;
        if (request.RequiresSystemSuitability.HasValue) entity.RequiresSystemSuitability = request.RequiresSystemSuitability.Value;
        if (request.MethodAbbreviation != null) entity.MethodAbbreviation = effectiveMethodAbbr;
        if (request.SstMaxRsdPercent.HasValue) entity.SstMaxRsdPercent = request.SstMaxRsdPercent;
        if (request.SstMinResolution.HasValue) entity.SstMinResolution = request.SstMinResolution;
        if (request.SstMaxTailingFactor.HasValue) entity.SstMaxTailingFactor = request.SstMaxTailingFactor;
        if (request.SstMinTheoreticalPlates.HasValue) entity.SstMinTheoreticalPlates = request.SstMinTheoreticalPlates;
        if (request.CalibrationEntryMode.HasValue) entity.CalibrationEntryMode = request.CalibrationEntryMode.Value;
        if (request.CalMinCorrelation.HasValue) entity.CalMinCorrelation = request.CalMinCorrelation;
        if (request.CalCorrelationType.HasValue) entity.CalCorrelationType = request.CalCorrelationType;
        if (request.CalMinStandards.HasValue) entity.CalMinStandards = request.CalMinStandards;
        if (request.CalCheckRecoveryLowPercent.HasValue) entity.CalCheckRecoveryLowPercent = request.CalCheckRecoveryLowPercent;
        if (request.CalCheckRecoveryHighPercent.HasValue) entity.CalCheckRecoveryHighPercent = request.CalCheckRecoveryHighPercent;
        if (request.CalBlankMax.HasValue) entity.CalBlankMax = request.CalBlankMax;
        if (request.CalIsRecoveryLowPercent.HasValue) entity.CalIsRecoveryLowPercent = request.CalIsRecoveryLowPercent;
        if (request.CalIsRecoveryHighPercent.HasValue) entity.CalIsRecoveryHighPercent = request.CalIsRecoveryHighPercent;
        if (request.CalRequireBlank.HasValue) entity.CalRequireBlank = request.CalRequireBlank;
        if (request.CalRequireIcv.HasValue) entity.CalRequireIcv = request.CalRequireIcv;
        if (request.CalRequireCcv.HasValue) entity.CalRequireCcv = request.CalRequireCcv;
        if (request.CalRequireInternalStandard.HasValue) entity.CalRequireInternalStandard = request.CalRequireInternalStandard;
        if (request.ReportedConcentrationBasis.HasValue) entity.ReportedConcentrationBasis = request.ReportedConcentrationBasis;
        if (request.CalMaxRunAgeHours.HasValue) entity.CalMaxRunAgeHours = request.CalMaxRunAgeHours;
        if (request.CalInstrumentType.HasValue) entity.CalInstrumentType = request.CalInstrumentType;
        // normalizedCalLevels already folds in the "empty string clears" convention above.
        if (request.CalStandardLevelsMgPerL != null) entity.CalStandardLevelsMgPerL = normalizedCalLevels;
        if (request.ReplicateCount.HasValue) entity.ReplicateCount = request.ReplicateCount.Value;
        if (request.EvaluationBasis.HasValue) entity.EvaluationBasis = request.EvaluationBasis.Value;
        if (request.ConditionFields != null) entity.ConditionFields = request.ConditionFields;
        if (request.UsesTare.HasValue) entity.UsesTare = request.UsesTare.Value;
        else if (effectiveWorkflowType == WorkflowType.Gravimetric && !entity.UsesTare.HasValue) entity.UsesTare = false;
        if (request.DissolutionS1Offset.HasValue) entity.DissolutionS1Offset = request.DissolutionS1Offset.Value;
        else if (effectiveWorkflowType == WorkflowType.Dissolution && !entity.DissolutionS1Offset.HasValue) entity.DissolutionS1Offset = 5m;
        if (request.DissolutionS2MinOffset.HasValue) entity.DissolutionS2MinOffset = request.DissolutionS2MinOffset.Value;
        else if (effectiveWorkflowType == WorkflowType.Dissolution && !entity.DissolutionS2MinOffset.HasValue) entity.DissolutionS2MinOffset = 15m;
        if (request.DissolutionS3MinOffset.HasValue) entity.DissolutionS3MinOffset = request.DissolutionS3MinOffset.Value;
        else if (effectiveWorkflowType == WorkflowType.Dissolution && !entity.DissolutionS3MinOffset.HasValue) entity.DissolutionS3MinOffset = 25m;
        if (request.DissolutionS3MaxBelowS2Min.HasValue) entity.DissolutionS3MaxBelowS2Min = request.DissolutionS3MaxBelowS2Min.Value;
        else if (effectiveWorkflowType == WorkflowType.Dissolution && !entity.DissolutionS3MaxBelowS2Min.HasValue) entity.DissolutionS3MaxBelowS2Min = 2m;
        if (request.DisintegrationStage1Units.HasValue) entity.DisintegrationStage1Units = request.DisintegrationStage1Units.Value;
        else if (effectiveWorkflowType == WorkflowType.Disintegration && !entity.DisintegrationStage1Units.HasValue) entity.DisintegrationStage1Units = 6;
        if (request.DisintegrationStage2Units.HasValue) entity.DisintegrationStage2Units = request.DisintegrationStage2Units.Value;
        else if (effectiveWorkflowType == WorkflowType.Disintegration && !entity.DisintegrationStage2Units.HasValue) entity.DisintegrationStage2Units = 12;
        if (request.DisintegrationMaxStage1Failures.HasValue) entity.DisintegrationMaxStage1Failures = request.DisintegrationMaxStage1Failures.Value;
        else if (effectiveWorkflowType == WorkflowType.Disintegration && !entity.DisintegrationMaxStage1Failures.HasValue) entity.DisintegrationMaxStage1Failures = 2;
        if (request.DisintegrationMinPassTotal.HasValue) entity.DisintegrationMinPassTotal = request.DisintegrationMinPassTotal.Value;
        else if (effectiveWorkflowType == WorkflowType.Disintegration && !entity.DisintegrationMinPassTotal.HasValue) entity.DisintegrationMinPassTotal = 16;
        if (request.WvUnitCount.HasValue) entity.WvUnitCount = request.WvUnitCount.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvUnitCount.HasValue) entity.WvUnitCount = 20;
        if (request.WvTabletBand1MaxMg.HasValue) entity.WvTabletBand1MaxMg = request.WvTabletBand1MaxMg.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand1MaxMg.HasValue) entity.WvTabletBand1MaxMg = 130m;
        if (request.WvTabletBand1Percent.HasValue) entity.WvTabletBand1Percent = request.WvTabletBand1Percent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand1Percent.HasValue) entity.WvTabletBand1Percent = 10m;
        if (request.WvTabletBand2MaxMg.HasValue) entity.WvTabletBand2MaxMg = request.WvTabletBand2MaxMg.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand2MaxMg.HasValue) entity.WvTabletBand2MaxMg = 324m;
        if (request.WvTabletBand2Percent.HasValue) entity.WvTabletBand2Percent = request.WvTabletBand2Percent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand2Percent.HasValue) entity.WvTabletBand2Percent = 7.5m;
        if (request.WvTabletBand3Percent.HasValue) entity.WvTabletBand3Percent = request.WvTabletBand3Percent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletBand3Percent.HasValue) entity.WvTabletBand3Percent = 5m;
        if (request.WvTabletMaxOutside.HasValue) entity.WvTabletMaxOutside = request.WvTabletMaxOutside.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvTabletMaxOutside.HasValue) entity.WvTabletMaxOutside = 2;
        if (request.WvCapsuleInnerPercent.HasValue) entity.WvCapsuleInnerPercent = request.WvCapsuleInnerPercent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleInnerPercent.HasValue) entity.WvCapsuleInnerPercent = 10m;
        if (request.WvCapsuleOuterPercent.HasValue) entity.WvCapsuleOuterPercent = request.WvCapsuleOuterPercent.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleOuterPercent.HasValue) entity.WvCapsuleOuterPercent = 25m;
        if (request.WvCapsuleS1MaxOutside.HasValue) entity.WvCapsuleS1MaxOutside = request.WvCapsuleS1MaxOutside.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleS1MaxOutside.HasValue) entity.WvCapsuleS1MaxOutside = 2;
        if (request.WvCapsuleS1MaxForRetest.HasValue) entity.WvCapsuleS1MaxForRetest = request.WvCapsuleS1MaxForRetest.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleS1MaxForRetest.HasValue) entity.WvCapsuleS1MaxForRetest = 6;
        if (request.WvCapsuleS2ExtraUnits.HasValue) entity.WvCapsuleS2ExtraUnits = request.WvCapsuleS2ExtraUnits.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleS2ExtraUnits.HasValue) entity.WvCapsuleS2ExtraUnits = 40;
        if (request.WvCapsuleS2MaxOutside.HasValue) entity.WvCapsuleS2MaxOutside = request.WvCapsuleS2MaxOutside.Value;
        else if (effectiveWorkflowType == WorkflowType.WeightVariation && !entity.WvCapsuleS2MaxOutside.HasValue) entity.WvCapsuleS2MaxOutside = 6;
        if (request.HplcMethodId.HasValue) entity.HplcMethodId = request.HplcMethodId;

        await _db.SaveChangesAsync();

        return TestDefinitionResponse.From(entity);
    }

    public async Task<TestDefinitionResponse> FreezeTestDefinitionAsync(int id)
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Test {id} not found.");
        entity.IsActive = false;
        await _db.SaveChangesAsync();
        return TestDefinitionResponse.From(entity);
    }

    public async Task<TestDefinitionResponse> UnfreezeTestDefinitionAsync(int id)
    {
        var entity = await _db.TestDefinitions.FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new NotFoundException($"Test {id} not found.");
        entity.IsActive = true;
        await _db.SaveChangesAsync();
        return TestDefinitionResponse.From(entity);
    }

    // Equation Types (REQ-FP-030/042)
    public IReadOnlyList<EquationTypeDto> GetEquationTypes()
    {
        var types = new[]
        {
            new EquationTypeDto(
                Code: nameof(EquationType.None),
                Name: "None",
                FormulaText: string.Empty,
                RequiredInputs: Array.Empty<string>()),
            new EquationTypeDto(
                Code: nameof(EquationType.SystemSuitability),
                Name: "System Suitability",
                FormulaText: "RSD <= MaxRSD, Resolution >= MinResolution, Tailing <= MaxTailing, Plates >= MinPlates",
                RequiredInputs: new[]
                {
                    "RsdPercent",
                    "Resolution",
                    "TailingFactor",
                    "TheoreticalPlates"
                }),
            new EquationTypeDto(
                Code: nameof(EquationType.CalibrationCurve),
                Name: "Calibration Curve",
                FormulaText: "Linear regression y = mx + b, correlation, blank and check recovery criteria",
                RequiredInputs: new[] { "ReportedPpm" }),
            new EquationTypeDto(
                Code: nameof(EquationType.Measurement),
                Name: "Numeric Measurement",
                FormulaText: "Mean, Min, Max, SD, RSD over replicate readings; evaluated against specification",
                RequiredInputs: new[] { "Readings" }),
            new EquationTypeDto(
                Code: nameof(EquationType.GravimetricLoss),
                Name: "Gravimetric % Loss",
                FormulaText: "% Loss = (W1 - W2) / W1 * 100",
                RequiredInputs: new[] { "W1", "W2" }),
            new EquationTypeDto(
                Code: nameof(EquationType.GravimetricResidue),
                Name: "Gravimetric % Residue",
                FormulaText: "% Residue = W2 / W1 * 100",
                RequiredInputs: new[] { "W1", "W2" }),
            new EquationTypeDto(
                Code: nameof(EquationType.Qualitative),
                Name: "Qualitative / Identification",
                FormulaText: "Conforms / Does Not Conform evaluated against expected text",
                RequiredInputs: new[] { "Conforms" })
        };
        return types;
    }
}
