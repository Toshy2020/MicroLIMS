namespace MicroLIMS.Domain.Entities;

public record DissolutionStandardData(
    int SystemSuitabilityRunId,
    string? RunCode,
    decimal StandardWeightMg,
    decimal StandardDilution,
    decimal StandardPurityPercent,
    decimal StandardMeanArea,
    decimal Cs);

public record DissolutionVesselData(
    int Stage,
    int VesselIndex,
    decimal Area,
    decimal Percent,
    bool Passed);

public record DissolutionOffsetsData(
    decimal S1Offset,
    decimal S2MinOffset,
    decimal S3MinOffset,
    decimal S3MaxBelowS2Min);

public record DissolutionCalculationData(
    decimal Cs,
    DissolutionStandardData Standard,
    decimal V,
    decimal Df,
    decimal Lc,
    decimal Q,
    DissolutionOffsetsData Offsets,
    IReadOnlyList<DissolutionVesselData> Vessels,
    decimal Mean,
    string Outcome,
    IReadOnlyList<string> Reasons);
