namespace MicroLIMS.Application.Services;

// The preparation parameters an analyst confirms and a Section Head
// configures - identical field set on both sides, so the rules live here
// once rather than being repeated in each service.
public record PreparationParameters(
    decimal Amount,
    string Technique,
    decimal? FiltrationVolume,
    decimal? WashingVolume,
    string Diluent,
    string Neutralizer);

public class PreparationParameterValidator
{
    // Diluent/Neutralizer are free text as of the 2026-09 simplification
    // (previously FK-validated against DiluentType/Neutralizer master
    // lists, with DiluentType additionally gating a GPT-released Media lot
    // selection for batch-tracked diluents - dropped as unused). Still
    // required, matching the "always a selection" behavior the dropdowns
    // enforced before.
    public Task ValidateAsync(PreparationParameters p)
    {
        if (string.IsNullOrWhiteSpace(p.Diluent))
            throw new InvalidOperationException("Diluent is required.");

        if (string.IsNullOrWhiteSpace(p.Neutralizer))
            throw new InvalidOperationException("Neutralizer is required.");

        if (p.Technique.Equals("Filtration", StringComparison.OrdinalIgnoreCase))
        {
            if (p.FiltrationVolume is null || p.WashingVolume is null)
                throw new InvalidOperationException("Filtration technique requires both Filtration Volume and Washing Volume.");
        }

        return Task.CompletedTask;
    }
}
