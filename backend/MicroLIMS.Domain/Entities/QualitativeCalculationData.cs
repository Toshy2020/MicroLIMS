namespace MicroLIMS.Domain.Entities;

public record QualitativeCalculationData(
    string Expected,
    bool Conforms,
    int? ContainerCount = null,
    List<int>? FailedContainers = null
);
