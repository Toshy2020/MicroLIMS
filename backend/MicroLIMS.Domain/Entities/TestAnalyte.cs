using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

public class TestAnalyte
{
    public int Id { get; set; }

    public int TestDefinitionId { get; set; }
    public TestDefinition? TestDefinition { get; set; }

    public string Element { get; set; } = string.Empty; // e.g. "Zn", "Ca", or vitamin name
    public decimal WavelengthNm { get; set; }
    public AnalyteView? View { get; set; }
    public decimal? LoqMgPerL { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Finished Product / Multi-Vitamin HPLC per-analyte SST criteria (null = not checked)
    public decimal? SstMaxRsdPercent { get; set; }
    public decimal? SstMinResolution { get; set; }
    public decimal? SstMaxTailingFactor { get; set; }
    public decimal? SstMinTheoreticalPlates { get; set; }
}
