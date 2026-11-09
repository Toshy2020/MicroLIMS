using MicroLIMS.Shared.Exceptions;
namespace MicroLIMS.Application.Services;

// The preparation parameters an analyst confirms and a Section Head
// configures - identical field set on both sides, so the rules live here
// once rather than being repeated in each service.
// DiluentChoice / NeutralizerChoice carry the structured master (source + id)
// where the caller is saving one; Diluent / Neutralizer are then the display
// names the server resolved from it.
public record PreparationParameters(
    decimal Amount,
    string Technique,
    decimal? FiltrationVolume,
    decimal? WashingVolume,
    string Diluent,
    string Neutralizer,
    PreparationMaterialChoice? DiluentChoice = null,
    PreparationMaterialChoice? NeutralizerChoice = null);

public class PreparationParameterValidator
{
    // Diluent/Neutralizer are display names: resolved from the referenced
    // master when saved with a source, legacy free text on configurations that
    // predate diluent/neutralizer traceability (see ItemPreparationConfiguration).
    // Either way they must not be blank.
    public Task ValidateAsync(PreparationParameters p)
    {
        if (string.IsNullOrWhiteSpace(p.Diluent))
            throw new BusinessRuleException("Diluent is required.");

        if (string.IsNullOrWhiteSpace(p.Neutralizer))
            throw new BusinessRuleException("Neutralizer is required.");

        if (p.Technique.Equals("Filtration", StringComparison.OrdinalIgnoreCase))
        {
            if (p.FiltrationVolume is null || p.WashingVolume is null)
                throw new BusinessRuleException("Filtration technique requires both Filtration Volume and Washing Volume.");
        }

        return Task.CompletedTask;
    }
}
