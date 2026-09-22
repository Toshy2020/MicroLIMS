using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.DTOs;

public record CreateTestDefinitionStageReplicateRequest(
    ProductionStageRole Role,
    int StandardReplicates,
    int SampleReplicates);

public record UpdateTestDefinitionStageReplicateRequest(
    int? StandardReplicates = null,
    int? SampleReplicates = null);

public record TestDefinitionStageReplicateDto(
    int Id,
    int TestDefinitionId,
    ProductionStageRole Role,
    int StandardReplicates,
    int SampleReplicates)
{
    public static TestDefinitionStageReplicateDto From(TestDefinitionStageReplicate r) => new(
        r.Id,
        r.TestDefinitionId,
        r.Role,
        r.StandardReplicates,
        r.SampleReplicates);
}
