namespace MicroLIMS.Domain.Entities;

public class Machine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<MachinePart> Parts { get; set; } = new();
}
