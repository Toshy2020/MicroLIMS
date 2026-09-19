namespace MicroLIMS.Domain.Entities;

public record GravimetricReplicateCalculationData(
    decimal? Container,
    decimal Initial,
    decimal Final,
    decimal W1,
    decimal W2,
    decimal Percent
);

public record GravimetricCalculationData(
    string Mode,
    IReadOnlyList<GravimetricReplicateCalculationData> Replicates,
    decimal Mean,
    int N
);
