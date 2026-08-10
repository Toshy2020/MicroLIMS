namespace MicroLIMS.Domain.Entities;

// A medium permitted on a workflow step, configured in Test Master.
// MaterialId points at the medium itself (a Material with MaterialType.
// DehydratedMedia); the physical lot chosen at run time is a Media row.
public class TestWorkflowStepMedia
{
    public int Id { get; set; }

    public int TestWorkflowStepId { get; set; }
    public TestWorkflowStep? TestWorkflowStep { get; set; }

    public int MaterialId { get; set; }
    public Material? Material { get; set; }

    // Bounds the incubators offered for this medium at run time.
    public decimal TempMin { get; set; }
    public decimal TempMax { get; set; }

    // True = mandatory single medium (broth and selective plating steps).
    // False = analyst-selectable from the permitted list (confirmatory).
    public bool IsRequired { get; set; }

    public int DisplayOrder { get; set; }
}
