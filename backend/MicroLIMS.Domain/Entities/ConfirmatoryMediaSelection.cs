namespace MicroLIMS.Domain.Entities;

// What the analyst actually chose for one confirmatory plating run:
// which permitted medium, which released lot of it, which incubator.
public class ConfirmatoryMediaSelection
{
    public int Id { get; set; }

    public int WorkflowStepResultId { get; set; }
    public WorkflowStepResult? WorkflowStepResult { get; set; }

    public int MaterialId { get; set; }
    public Material? Material { get; set; }

    // The released lot (Media row) of that material.
    public int MediaId { get; set; }
    public Media? Media { get; set; }

    public int EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    // Always false: a medium outside the permitted list is rejected
    // before this row is created. Persisted so the record states the
    // fact rather than leaving it implied.
    public bool WasAnalystAdded { get; set; }
}
