namespace MicroLIMS.Domain.Entities;

// Plain name-only Section Head master list (Tween, Lecithin, ...) - no
// lot/expiry tracking.
public class Neutralizer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
