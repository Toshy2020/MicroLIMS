namespace MicroLIMS.Domain.Entities;

// Fine-grained permission a Role can be granted, e.g. "samples.receive",
// "results.approve". Kept separate from RoleType so System Administration
// and Laboratory Administration can be composed independently (Frozen
// Principle #4 - Role separation).
public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
