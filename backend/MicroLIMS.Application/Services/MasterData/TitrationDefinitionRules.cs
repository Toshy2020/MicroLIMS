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
                throw new InvalidOperationException("Equation type Titration is only allowed for the Titration workflow.");
            Clear(t);
            return;
        }

        if (t.EquationType != EquationType.Titration)
            throw new InvalidOperationException("Equation type must be Titration when workflow type is Titration.");
        if (!t.ReplicateCount.HasValue || t.ReplicateCount.Value < 1 || t.ReplicateCount.Value > 10)
            throw new InvalidOperationException("Replicate count must be between 1 and 10 for titration tests.");
        if (t.RequiresSystemSuitability)
            throw new InvalidOperationException("Titration tests must not require system suitability.");

        if (!t.TitrationType.HasValue) throw new InvalidOperationException("Titration type is required.");
        if (!t.TitrationMode.HasValue) throw new InvalidOperationException("Titration mode (direct or residual) is required.");
        if (!t.TitrationCalculation.HasValue) throw new InvalidOperationException("Titration calculation method is required.");
        if (!t.TitrationEndpoint.HasValue) throw new InvalidOperationException("Titration endpoint is required.");
        if (!t.TitrantSolutionMasterId.HasValue) throw new InvalidOperationException("Titrant is required.");

        var type = t.TitrationType.Value;
        var mode = t.TitrationMode.Value;
        var calc = t.TitrationCalculation.Value;
        bool kf = type == TitrationType.KarlFischer;

        var titrant = await db.SolutionMasters.AsNoTracking().FirstOrDefaultAsync(m => m.Id == t.TitrantSolutionMasterId.Value)
            ?? throw new InvalidOperationException("Titrant solution master not found.");
        RequireTitrant(titrant, t.SectionId, "Titrant");
        if (!titrant.NominalStrength.HasValue || titrant.NominalStrength <= 0 || !titrant.StrengthUnit.HasValue)
            throw new InvalidOperationException("Titrant has no nominal strength configured.");
        if (kf != (titrant.StrengthUnit == TitrantStrengthUnit.MgWaterPerMl))
            throw new InvalidOperationException(kf
                ? "Karl Fischer tests need a titrant whose strength unit is mg water per mL."
                : "A titrant with strength unit mg water per mL can only be used for Karl Fischer tests.");

        if (kf && (calc != TitrationCalculation.UspFactor || mode != TitrationMode.Direct))
            throw new InvalidOperationException("Karl Fischer tests support only the USP factor calculation in direct mode.");

        if (t.TitrationNonAqueous == true && type != TitrationType.AcidBase)
            throw new InvalidOperationException("Non-aqueous titration only applies to acid-base titrations.");
        t.TitrationNonAqueous = type == TitrationType.AcidBase ? (t.TitrationNonAqueous ?? false) : null;

        // F: only the USP factor form of a non-KF test uses it.
        if (calc == TitrationCalculation.UspFactor && !kf)
        {
            if (!t.TitrationEquivalencyFactor.HasValue || t.TitrationEquivalencyFactor <= 0)
                throw new InvalidOperationException("Equivalency factor (mg per mEq/mmol) must be greater than zero.");
        }
        else t.TitrationEquivalencyFactor = null;

        t.TitrationBlankRequired ??= false;

        if (mode == TitrationMode.Residual)
        {
            if (kf) throw new InvalidOperationException("Karl Fischer tests cannot use residual mode.");
            if (!t.TitrationExcessSolutionMasterId.HasValue)
                throw new InvalidOperationException("Residual titration needs the excess titrant.");
            var excess = await db.SolutionMasters.AsNoTracking().FirstOrDefaultAsync(m => m.Id == t.TitrationExcessSolutionMasterId.Value)
                ?? throw new InvalidOperationException("Excess titrant solution master not found.");
            RequireTitrant(excess, t.SectionId, "Excess titrant");
            if (!excess.NominalStrength.HasValue || excess.NominalStrength <= 0 || excess.StrengthUnit != titrant.StrengthUnit)
                throw new InvalidOperationException("The excess titrant must have a nominal strength and the same strength unit as the titrant.");
            if (!t.TitrationExcessVolumeMl.HasValue || t.TitrationExcessVolumeMl <= 0)
                throw new InvalidOperationException("Excess titrant volume must be greater than zero.");
        }
        else
        {
            t.TitrationExcessSolutionMasterId = null;
            t.TitrationExcessVolumeMl = null;
        }

        if (calc == TitrationCalculation.Relative)
        {
            if (!t.TitrationStandardEntryId.HasValue)
                throw new InvalidOperationException("The reference standard is required for the relative calculation.");
            var entry = await db.MaterialMasterEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == t.TitrationStandardEntryId.Value)
                ?? throw new InvalidOperationException("Reference standard master entry not found.");
            if (entry.SectionId != t.SectionId)
                throw new InvalidOperationException("The reference standard belongs to another laboratory.");
            if (mode != TitrationMode.Direct)
                throw new InvalidOperationException("The relative calculation supports direct mode only.");
        }
        else
        {
            t.TitrationStandardEntryId = null;
        }

        if (t.TitrationMaxRsdPercent.HasValue && t.TitrationMaxRsdPercent <= 0)
            throw new InvalidOperationException("Maximum RSD must be greater than zero when set.");

        // Visual endpoint: the indicator is an Indicator entry of Reagents &
        // Reference Standards; its name is copied for display and the record.
        if (t.TitrationEndpoint == TitrationEndpoint.Visual)
        {
            if (!t.TitrationIndicatorEntryId.HasValue)
                throw new InvalidOperationException("Indicator is required for a visual endpoint.");
            var indicator = await db.MaterialMasterEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == t.TitrationIndicatorEntryId.Value)
                ?? throw new InvalidOperationException("Indicator master entry not found.");
            if (indicator.Category != MaterialMasterCategory.Indicator)
                throw new InvalidOperationException("The indicator must be an Indicator entry in Reagents & Reference Standards.");
            if (indicator.SectionId != t.SectionId)
                throw new InvalidOperationException("The indicator belongs to another laboratory.");
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
                throw new InvalidOperationException("Temperature correction only applies to non-aqueous acid-base titrations.");
            t.TitrationExpansionCoefficient ??= DefaultExpansionCoefficient;
            if (t.TitrationExpansionCoefficient <= 0)
                throw new InvalidOperationException("Expansion coefficient must be greater than zero.");
        }
        else
        {
            t.TitrationTempCorrection = false;
            t.TitrationExpansionCoefficient = null;
        }
    }

    private static void RequireTitrant(SolutionMaster m, int sectionId, string what)
    {
        if (m.Type != SolutionType.Titrant) throw new InvalidOperationException($"{what} must be a titrant solution master.");
        if (m.SectionId != sectionId) throw new InvalidOperationException($"{what} belongs to another laboratory.");
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
