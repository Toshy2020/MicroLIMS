using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class TestAnalyte
{
    public int Id { get; set; }

    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public string Element { get; set; } = string.Empty; // e.g. "Zn", "Ca"
    public decimal WavelengthNm { get; set; }
    public AnalyteView View { get; set; }
    public decimal LoqMgPerL { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
