namespace MicroLIMS.Application.Validators;

public class PathogenValidator
{
    // Salmonella cannot be finalized from a single growth/no-growth flag -
    // it must complete the TSB -> RVS -> XLD+TSI chain first.
    public List<string> ValidateSalmonellaChain(bool tsbComplete, bool rvsComplete, bool xldTsiComplete)
    {
        var errors = new List<string>();
        if (!tsbComplete) errors.Add("TSB enrichment step is not complete.");
        else if (!rvsComplete) errors.Add("RVS enrichment step is not complete.");
        else if (!xldTsiComplete) errors.Add("XLD + TSI confirmation step is not complete.");
        return errors;
    }
}
