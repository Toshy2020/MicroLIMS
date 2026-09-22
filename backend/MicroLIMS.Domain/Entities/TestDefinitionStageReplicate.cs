using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Per-(TestDefinition, ProductionStageRole) standard/sample replicate
// counts for FP Standard-Comparison Assay, e.g. the SOP's own "standard
// 6 release / 3 stability, sample 1 bulk / 2 finished / 3 stability"
// figures - entered here by a Section Head in Test Master, never
// hardcoded as a default anywhere in code. A missing row for a role
// means "not configured for that stage" - distinct from a configured
// zero, which is rejected outright (StandardReplicates/SampleReplicates
// must both be >= 1).
public class TestDefinitionStageReplicate
{
    public int Id { get; set; }

    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public ProductionStageRole Role { get; set; }

    public int StandardReplicates { get; set; }
    public int SampleReplicates { get; set; }
}
