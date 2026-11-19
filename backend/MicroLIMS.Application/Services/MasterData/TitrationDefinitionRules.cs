using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.MasterData;

// Titration columns on TestDefinition (spec 3.2). Runs on the entity after the
// request values were applied: validates, defaults, and clears every titration
// column that does not apply (all of them when the workflow is not Titration).
public static class TitrationDefinitionRules
{
    public const decimal DefaultExpansionCoefficient = 0.0011m;

    public static async Task NormalizeAndValidateAsync(IMicroLimsDbContext db, TestDefinition t)
    {
        if (t.WorkflowType != WorkflowType.Titration)
        {
            if (t.EquationType == EquationType.Titration)
                throw new BusinessRuleException(
                    MessageCodes.TitrationEquationOnlyForTitrationWorkflow,
                    "Equation type Titration is only allowed for the Titration workflow.");
            Clear(t);
            return;
        }

        if (t.EquationType != EquationType.Titration)
            throw new BusinessRuleException(
                MessageCodes.TitrationEquationMustBeTitration,
                "Equation type must be Titration when workflow type is Titration.");
        if (!t.ReplicateCount.HasValue || t.ReplicateCount.Value < 1 || t.ReplicateCount.Value > 10)
            throw new BusinessRuleException(
                MessageCodes.TitrationReplicateCountRange,
                "Replicate count must be between 1 and 10 for titration tests.");
        if (t.RequiresSystemSuitability)
            throw new BusinessRuleException(
                MessageCodes.TitrationSystemSuitabilityNotAllowed,
                "Titration tests must not require system suitability.");

        if (!t.TitrationType.HasValue)
            throw new BusinessRuleException(MessageCodes.TitrationTypeRequired, "Titration type is required.");
        if (!t.TitrationMode.HasValue)
            throw new BusinessRuleException(MessageCodes.TitrationModeRequired, "Titration mode (direct or residual) is required.");
        if (!t.TitrationCalculation.HasValue)
            throw new BusinessRuleException(MessageCodes.TitrationCalculationRequired, "Titration calculation method is required.");
        if (!t.TitrationEndpoint.HasValue)
            throw new BusinessRuleException(MessageCodes.TitrationEndpointRequired, "Titration endpoint is required.");
        if (!t.TitrantSolutionMasterId.HasValue)
            throw new BusinessRuleException(MessageCodes.TitrationTitrantRequired, "Titrant is required.");

        var type = t.TitrationType.Value;
        var mode = t.TitrationMode.Value;
        var calc = t.TitrationCalculation.Value;
        bool kf = type == TitrationType.KarlFischer;

        var titrant = await db.SolutionMasters.AsNoTracking().FirstOrDefaultAsync(m => m.Id == t.TitrantSolutionMasterId.Value)
            ?? throw new BusinessRuleException(MessageCodes.TitrationTitrantMasterNotFound, "Titrant solution master not found.");
        RequireTitrant(titrant, t.SectionId, "Titrant");
        if (!titrant.NominalStrength.HasValue || titrant.NominalStrength <= 0 || !titrant.StrengthUnit.HasValue)
            throw new BusinessRuleException(MessageCodes.TitrationTitrantMasterNoNominalStrength, "Titrant has no nominal strength configured.");
        if (kf != (titrant.StrengthUnit == TitrantStrengthUnit.MgWaterPerMl))
        {
            if (kf)
                throw new BusinessRuleException(
                    MessageCodes.TitrationKarlFischerTitrantUnitRequired,
                    "Karl Fischer tests need a titrant whose strength unit is mg water per mL.");
            throw new BusinessRuleException(
                MessageCodes.TitrationMgWaterUnitOnlyForKarlFischer,
                "A titrant with strength unit mg water per mL can only be used for Karl Fischer tests.");
        }

        if (kf && (calc != TitrationCalculation.UspFactor || mode != TitrationMode.Direct))
            throw new BusinessRuleException(
                MessageCodes.TitrationKarlFischerModeAndCalculation,
                "Karl Fischer tests support only the USP factor calculation in direct mode.");

        if (t.TitrationNonAqueous == true && type != TitrationType.AcidBase)
            throw new BusinessRuleException(
                MessageCodes.TitrationNonAqueousOnlyForAcidBase,
                "Non-aqueous titration only applies to acid-base titrations.");
        t.TitrationNonAqueous = type == TitrationType.AcidBase ? (t.TitrationNonAqueous ?? false) : null;

        // F: only the USP factor form of a non-KF test uses it.
        if (calc == TitrationCalculation.UspFactor && !kf)
        {
            if (!t.TitrationEquivalencyFactor.HasValue || t.TitrationEquivalencyFactor <= 0)
                throw new BusinessRuleException(
                    MessageCodes.TitrationEquivalencyFactorWithUnitPositive,
                    "Equivalency factor (mg per mEq/mmol) must be greater than zero.");
        }
        else t.TitrationEquivalencyFactor = null;

        t.TitrationBlankRequired ??= false;

        if (mode == TitrationMode.Residual)
        {
            if (kf)
                throw new BusinessRuleException(
                    MessageCodes.TitrationKarlFischerNoResidualMode,
                    "Karl Fischer tests cannot use residual mode.");
            if (!t.TitrationExcessSolutionMasterId.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.TitrationResidualNeedsExcessTitrant,
                    "Residual titration needs the excess titrant.");
            var excess = await db.SolutionMasters.AsNoTracking().FirstOrDefaultAsync(m => m.Id == t.TitrationExcessSolutionMasterId.Value)
                ?? throw new BusinessRuleException(MessageCodes.TitrationExcessMasterNotFound, "Excess titrant solution master not found.");
            RequireTitrant(excess, t.SectionId, "Excess titrant");
            if (!excess.NominalStrength.HasValue || excess.NominalStrength <= 0 || excess.StrengthUnit != titrant.StrengthUnit)
                throw new BusinessRuleException(
                    MessageCodes.TitrationExcessMasterStrengthUnitMismatch,
                    "The excess titrant must have a nominal strength and the same strength unit as the titrant.");
            if (!t.TitrationExcessVolumeMl.HasValue || t.TitrationExcessVolumeMl <= 0)
                throw new BusinessRuleException(
                    MessageCodes.TitrationExcessTitrantVolumePositive,
                    "Excess titrant volume must be greater than zero.");
        }
        else
        {
            t.TitrationExcessSolutionMasterId = null;
            t.TitrationExcessVolumeMl = null;
        }

        if (calc == TitrationCalculation.Relative)
        {
            if (!t.TitrationStandardEntryId.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.TitrationReferenceStandardRequired,
                    "The reference standard is required for the relative calculation.");
            var entry = await db.MaterialMasterEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == t.TitrationStandardEntryId.Value)
                ?? throw new BusinessRuleException(
                    MessageCodes.TitrationReferenceStandardMasterNotFound,
                    "Reference standard master entry not found.");
            if (mode != TitrationMode.Direct)
                throw new BusinessRuleException(
                    MessageCodes.TitrationRelativeOnlyDirectMode,
                    "The relative calculation supports direct mode only.");
        }
        else
        {
            t.TitrationStandardEntryId = null;
        }

        if (t.TitrationMaxRsdPercent.HasValue && t.TitrationMaxRsdPercent <= 0)
            throw new BusinessRuleException(
                MessageCodes.TitrationMaxRsdPositive,
                "Maximum RSD must be greater than zero when set.");

        // Visual endpoint: the indicator is an Indicator entry of Reagents &
        // Reference Standards; its name is copied for display and the record.
        if (t.TitrationEndpoint == TitrationEndpoint.Visual)
        {
            if (!t.TitrationIndicatorEntryId.HasValue)
                throw new BusinessRuleException(
                    MessageCodes.TitrationIndicatorRequired,
                    "Indicator is required for a visual endpoint.");
            var indicator = await db.MaterialMasterEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == t.TitrationIndicatorEntryId.Value)
                ?? throw new BusinessRuleException(
                    MessageCodes.TitrationIndicatorMasterNotFound,
                    "Indicator master entry not found.");
            if (indicator.Category != MaterialMasterCategory.Indicator)
                throw new BusinessRuleException(
                    MessageCodes.TitrationIndicatorInvalidCategory,
                    "The indicator must be an Indicator entry in Reagents & Reference Standards.");
            t.TitrationIndicator = indicator.Name;
        }
        else
        {
            t.TitrationIndicatorEntryId = null;
            t.TitrationIndicator = null;
        }

        if (t.TitrationTempCorrection == true)
        {
            if (type != TitrationType.AcidBase || t.TitrationNonAqueous != true)
                throw new BusinessRuleException(
                    MessageCodes.TitrationTemperatureCorrectionOnlyNonAqueousAcidBase,
                    "Temperature correction only applies to non-aqueous acid-base titrations.");
            t.TitrationExpansionCoefficient ??= DefaultExpansionCoefficient;
            if (t.TitrationExpansionCoefficient <= 0)
                throw new BusinessRuleException(
                    MessageCodes.TitrationExpansionCoefficientPositive,
                    "Expansion coefficient must be greater than zero.");
        }
        else
        {
            t.TitrationTempCorrection = false;
            t.TitrationExpansionCoefficient = null;
        }
    }

    private static void RequireTitrant(SolutionMaster m, int sectionId, string what)
    {
        bool isExcess = what == "Excess titrant";
        if (m.Type != SolutionType.Titrant)
        {
            var code = isExcess ? MessageCodes.TitrationExcessMasterNotTitrant : MessageCodes.TitrationTitrantMasterNotTitrant;
            throw new BusinessRuleException(code, $"{what} must be a titrant solution master.");
        }
        if (m.SectionId != sectionId)
        {
            var code = isExcess ? MessageCodes.TitrationExcessMasterWrongLab : MessageCodes.TitrationTitrantMasterWrongLab;
            throw new BusinessRuleException(code, $"{what} belongs to another laboratory.");
        }
    }

    // Every titration setting, for the before/after comparison and audit of a reason-for-change edit.
    // Decimals are normalised so numeric(18,6) scale does not read as a change.
    public static object Snapshot(TestDefinition t)
    {
        static decimal? N(decimal? d) => d / 1.000000000000000000000000000m;
        return new
        {
            t.ReplicateCount, t.TitrantSolutionMasterId, t.TitrationType, t.TitrationNonAqueous, t.TitrationMode, t.TitrationCalculation,
            TitrationEquivalencyFactor = N(t.TitrationEquivalencyFactor), t.TitrationBlankRequired, t.TitrationExcessSolutionMasterId,
            TitrationExcessVolumeMl = N(t.TitrationExcessVolumeMl), TitrationMaxRsdPercent = N(t.TitrationMaxRsdPercent),
            t.TitrationEndpoint, t.TitrationIndicatorEntryId, t.TitrationIndicator, t.TitrationTempCorrection,
            TitrationExpansionCoefficient = N(t.TitrationExpansionCoefficient), t.TitrationStandardEntryId, t.PhyschemArea, t.MoistureRole,
        };
    }

    private static void Clear(TestDefinition t)
    {
        t.TitrationType = null; t.TitrationNonAqueous = null; t.TitrationMode = null; t.TitrationCalculation = null;
        t.TitrantSolutionMasterId = null; t.TitrationEquivalencyFactor = null; t.TitrationBlankRequired = null;
        t.TitrationExcessSolutionMasterId = null; t.TitrationExcessVolumeMl = null; t.TitrationMaxRsdPercent = null;
        t.TitrationEndpoint = null; t.TitrationIndicatorEntryId = null; t.TitrationIndicator = null; t.TitrationTempCorrection = null;
        t.TitrationExpansionCoefficient = null; t.TitrationStandardEntryId = null;
    }
}
