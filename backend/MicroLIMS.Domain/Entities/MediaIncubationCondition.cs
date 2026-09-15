namespace MicroLIMS.Domain.Entities;

// An incubation condition (time + temperature) available for a dehydrated media product.
// Conditions are locked once used by any MediaConfiguration or TestWorkflowStepMedia;
// they cannot be edited or deleted once in use, ensuring audit trail and historical integrity.
public class MediaIncubationCondition
{
    public int Id { get; set; }

    public int MediaProductId { get; set; }
    public MediaProduct? MediaProduct { get; set; }

    public int IncubationMinHours { get; set; }
    public int IncubationMaxHours { get; set; }
    public decimal TemperatureMin { get; set; }
    public decimal TemperatureMax { get; set; }
}
