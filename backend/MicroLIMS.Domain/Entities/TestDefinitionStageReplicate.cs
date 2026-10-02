using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Per-(TestDefinition, ProductionStageRole) sample replicate count, e.g.
// "sample 3 bulk / 2 finished" - entered by a Section Head in Test Master,
// never hardcoded as a default anywhere in code. Standard injections are
// set on the HPLC method analyte, not here. A missing row for a role
// means "not configured for that stage" - distinct from a configured
// zero, which is rejected outright (SampleReplicates must be >= 1).
public class TestDefinitionStageReplicate : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }

    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public ProductionStageRole Role { get; set; }

    public int SampleReplicates { get; set; }
}
