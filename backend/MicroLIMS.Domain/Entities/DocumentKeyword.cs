namespace MicroLIMS.Domain.Entities;

public class DocumentKeyword
{
    public int Id { get; set; }

    public int DocumentMasterId { get; set; }
    public DocumentMaster DocumentMaster { get; set; } = null!;

    public string Keyword { get; set; } = string.Empty;
}
