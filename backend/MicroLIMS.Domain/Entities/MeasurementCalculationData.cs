namespace MicroLIMS.Domain.Entities;

public record MeasurementCalculationData(
    decimal Mean,
    decimal Min,
    decimal Max,
    decimal? Sd,
    decimal? Rsd,
    string Basis,
    int N
);
