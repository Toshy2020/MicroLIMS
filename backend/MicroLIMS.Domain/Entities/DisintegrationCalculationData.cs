namespace MicroLIMS.Domain.Entities;

public record DisintegrationConfigData(
    int Stage1Units,
    int Stage2Units,
    int MaxStage1Failures,
    int MinPassTotal);

public record DisintegrationUnitData(
    int Stage,
    int UnitIndex,
    decimal? Minutes,
    bool Passed);

public record DisintegrationCalculationData(
    decimal LimitMinutes,
    DisintegrationConfigData Config,
    IReadOnlyList<DisintegrationUnitData> Units,
    int PassedCount,
    decimal? LongestMinutes,
    string Outcome,
    IReadOnlyList<string> Reasons);
