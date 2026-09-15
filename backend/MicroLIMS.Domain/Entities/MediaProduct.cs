namespace MicroLIMS.Domain.Entities;

// One row per dehydrated medium; Name is free text entered on the Media
// Configuration page; Code is the prefix of every prepared lot number
// ({Code}/{seq:D2}/{yy}) and is copied onto Material.Code when a batch is
// received; changing Code later requires a Section Head electronic signature
// (enforced in the Application layer).
public class MediaProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<MediaConfiguration> Configurations { get; set; } = new();
    public List<MediaIncubationCondition> IncubationConditions { get; set; } = new();
}
