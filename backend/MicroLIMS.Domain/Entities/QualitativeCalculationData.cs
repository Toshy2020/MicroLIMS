using System.Text.Json.Serialization;

namespace MicroLIMS.Domain.Entities;

public record QualitativeCalculationData(
    string Expected,
    bool Conforms,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ContainerCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<int>? FailedContainers = null
);
