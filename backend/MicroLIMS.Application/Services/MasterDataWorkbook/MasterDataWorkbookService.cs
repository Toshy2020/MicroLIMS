using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Abstractions.Workbook;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services.MasterData;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// Master data workbook: download the current data in workbook form, check
// an uploaded workbook (dry run), load it (signed), and verify a load
// (second person, signed). Every record is created by the existing
// master-data service for its type - see SheetHandler.
public class MasterDataWorkbookService
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private readonly IMicroLimsDbContext _db;
    private readonly IWorkbookFormat _format;
    private readonly IFileStorageService _storage;
    private readonly IElectronicSignatureService _signatures;
    private readonly TimeProvider _time;
    private readonly ItemService _items;
    private readonly MaterialMasterService _materialMaster;
    private readonly IReadOnlyList<SheetHandler> _sheets;

    public MasterDataWorkbookService(
        IMicroLimsDbContext db,
        IWorkbookFormat format,
        IFileStorageService storage,
        IElectronicSignatureService signatures,
        TimeProvider time,
        OrganismMasterDataService organisms,
        ReferenceListMasterDataService referenceLists,
        EnvironmentalMonitoringMasterDataService em,
        WaterMasterDataService water,
        MaterialMasterService materialMaster,
        MediaProductService mediaProducts,
        EquipmentMasterDataService equipment,
        ItemService items,
        SpecificationMasterDataService specifications)
    {
        _db = db;
        _format = format;
        _storage = storage;
        _signatures = signatures;
        _time = time;
        _items = items;
        _materialMaster = materialMaster;
        _sheets = MasterDataSheets.Create(organisms, referenceLists, em, water, materialMaster, mediaProducts, equipment, items, specifications);
    }

    public async Task<(byte[] Content, string FileName)> DownloadAsync()
    {
        var sheets = new List<WorkbookSheet>();
        var choices = new List<WorkbookColumnChoices>();
        var help = new List<WorkbookInstructionRow>();

        foreach (var handler in _sheets)
        {
            var columns = handler.Columns.Select(c => c.Name).ToList();
            var rows = (await handler.ExportAsync(_db))
                .Select((cells, i) => new WorkbookRow(i + 2, cells))
                .ToList();
            sheets.Add(new WorkbookSheet(handler.Name, columns, rows));

            foreach (var column in handler.Columns)
            {
                help.Add(new WorkbookInstructionRow(handler.Name, column.Name, column.Required ? "Yes" : "", column.Help));
                if (column.EnumType is not null)
                    choices.Add(new WorkbookColumnChoices(handler.Name, column.Name, Values: Enum.GetNames(column.EnumType)));
                else if (column.SourceSheet is not null)
                    choices.Add(new WorkbookColumnChoices(handler.Name, column.Name, SourceSheet: column.SourceSheet, SourceColumn: column.SourceColumn));
                else if (await handler.DbChoicesAsync(_db, column.Name) is { Count: > 0 } values)
                    choices.Add(new WorkbookColumnChoices(handler.Name, column.Name, Values: values));
            }
        }

        var bytes = _format.Write(new WorkbookContent(sheets, choices, MasterDataSheets.Instructions, help));
        var stamp = _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd");
        return (bytes, $"MicroLIMS-master-data-{stamp}.xlsx");
    }

    internal IReadOnlyList<WorkbookSheet> ReadSheets(byte[] file)
    {
        if (file.Length == 0)
            throw new InvalidOperationException("The file is empty.");
        if (file.Length > MaxFileBytes)
            throw new InvalidOperationException($"The file is larger than {MaxFileBytes / (1024 * 1024)} MB. Split it into several workbooks.");
        try
        {
            return _format.Read(new MemoryStream(file));
        }
        catch (WorkbookFormatException e)
        {
            throw new InvalidOperationException(e.Message);
        }
    }
}
