namespace MicroLIMS.Domain.Entities;

public record ElementalCalculationData(
    string Element,
    string RunCode,
    bool RunAnalytePassed,
    decimal ReportedPpm,
    bool OverRange,
    bool BelowLoq,
    decimal? MgPerUnit,
    decimal? ResultClaim,
    decimal? PercentLabelClaim,
    decimal? ConversionFactor,
    decimal? LabelClaim,
    string? LabelClaimUnit
);
