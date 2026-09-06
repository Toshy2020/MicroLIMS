namespace MicroLIMS.Domain.Entities;

// Section Head managed list of Production Stage values (e.g. B, IP,
// F.P) offered on the Finished Product receiving form. Sample.
// ProductionStage stores the chosen value as plain text, not an FK -
// removing a stage here never affects historical records.
public class ProductionStage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
