namespace MicroLIMS.Application.DTOs;

// The Certificate of Analysis content, decided on the server (see
// CertificateOfAnalysisBuilder). The page renders it and decides nothing.
public class CertificateOfAnalysisDto
{
    // The whole sample: the single-laboratory certificate, or the combined
    // one across every laboratory.
    public CoaScopeDto Sample { get; set; } = new();

    // One per laboratory section with tests on this sample.
    public List<CoaSectionScopeDto> Sections { get; set; } = new();
}

public class CoaSectionScopeDto
{
    public int SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public CoaScopeDto Scope { get; set; } = new();
}

public class CoaScopeDto
{
    // Water/EM/After Cleaning: a location x test grid. Null when none of
    // the scope's tests has locations.
    public CoaMatrixDto? Matrix { get; set; }

    // Product/Raw Material/Packaging: one row per test. Set only when there
    // is no Matrix.
    public CoaSimpleResultDto? Simple { get; set; }

    public bool Complies { get; set; }
    public string ConclusionText { get; set; } = string.Empty;

    // Latest entered/observed/submitted timestamp across the scope's
    // non-superseded tests.
    public DateTime? ResultDate { get; set; }
}

public class CoaMatrixDto
{
    public List<CoaColumnDto> Columns { get; set; } = new();
    public List<CoaRowDto> Rows { get; set; } = new();
    public List<CoaTestConclusionDto> TestConclusions { get; set; } = new();
    public bool OverallComplies { get; set; }
    public int TotalTests { get; set; }
    public int TotalLocations { get; set; }

    // Distinct units among quantitative columns, for the footnote.
    public List<string> Units { get; set; } = new();
}

public class CoaColumnDto
{
    public int TestOrderId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestDisplayName { get; set; } = string.Empty;
    public bool IsQuantitative { get; set; }
    public string? Unit { get; set; }
}

public class CoaRowDto
{
    public string LocationKey { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;

    // Aligned 1:1 with CoaMatrixDto.Columns. Null = that test has no
    // result row at this location (rendered as a dash).
    public List<CoaCellDto?> Cells { get; set; } = new();
}

public class CoaCellDto
{
    // "quantitative" or "qualitative".
    public string Kind { get; set; } = string.Empty;
    public string? Alert { get; set; }
    public string? Action { get; set; }
    public string? Spec { get; set; }
    public string Result { get; set; } = string.Empty;
    public bool Conform { get; set; }
}

public class CoaTestConclusionDto
{
    public int TestOrderId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestDisplayName { get; set; } = string.Empty;
    public bool Conforms { get; set; }
    public List<string> FailingLocationNames { get; set; } = new();
    public List<string> UnconfiguredLocationNames { get; set; } = new();
    public List<string> MissingResultLocationNames { get; set; } = new();
}

public class CoaSimpleResultDto
{
    public List<CoaSimpleRowDto> Rows { get; set; } = new();
    public bool OverallComplies { get; set; }
}

public class CoaSimpleRowDto
{
    public int TestOrderId { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public string TestDisplayName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? AnalystName { get; set; }
    public DateTime? AnalystAt { get; set; }
    public bool Conform { get; set; }
    public bool LimitsNotConfigured { get; set; }
    public bool NoResult { get; set; }
}
