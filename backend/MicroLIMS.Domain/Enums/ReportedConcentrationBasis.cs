namespace MicroLIMS.Domain.Enums;

// What the instrument software reports for each element (scoping Q2).
public enum ReportedConcentrationBasis
{
    // Concentration in the measured solution; the LIMS would apply dilution,
    // volume and weight. Reserved - not used by the lab's Syngistix template.
    SolutionMgPerL = 1,

    // Syngistix applies weight, volume and dilution in its method template
    // and reports ppm in the sample: mg/kg for solids, mg/L for liquids.
    // The LIMS never applies them again. (User confirmed 2026-09-19.)
    SamplePpm = 2
}
