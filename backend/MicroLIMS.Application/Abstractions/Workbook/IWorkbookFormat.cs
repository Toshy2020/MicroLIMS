using MicroLIMS.Application.Services.MasterDataWorkbook;

namespace MicroLIMS.Application.Abstractions.Workbook;

// The seam between the import and the .xlsx file format. Only the
// Infrastructure implementation knows about the spreadsheet library.
public interface IWorkbookFormat
{
    byte[] Write(WorkbookContent content);

    // Every data sheet (not "Instructions" or "Lists"): row 1 is the header,
    // data starts on row 2, fully blank rows are skipped. Throws
    // WorkbookFormatException when the stream is not a readable .xlsx.
    IReadOnlyList<WorkbookSheet> Read(Stream xlsx);
}

public class WorkbookFormatException : Exception
{
    public WorkbookFormatException(string message, Exception? inner = null) : base(message, inner) { }
}
