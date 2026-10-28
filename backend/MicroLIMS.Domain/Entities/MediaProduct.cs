namespace MicroLIMS.Domain.Entities;

// One row per dehydrated medium; Name is free text entered on the Media
// Configuration page; Code is the prefix of every prepared lot number
// ({Code}/{seq:D2}/{yy}) and is copied onto Material.Code when a batch is
// received; changing Code later requires a Section Head electronic signature
// (enforced in the Application layer).
public class MediaProduct : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    // A deactivated product can't be chosen for new dehydrated-media lots or new
    // configurations; existing lots and configurations keep working.
    public bool IsActive { get; set; } = true;
    public List<MediaConfiguration> Configurations { get; set; } = new();
    public List<MediaIncubationCondition> IncubationConditions { get; set; } = new();
}
