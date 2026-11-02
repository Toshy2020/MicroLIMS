using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Abstractions.Storage;
using MicroLIMS.Application.Abstractions.Workbook;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.MasterDataWorkbook;

// Master data workbook: download the current data in workbook form, check
// an uploaded workbook (dry run), load it (signed), and verify a load
// (second person, signed). Every record is created by the existing
// master-data service for its type - see SheetHandler.
public class MasterDataWorkbookService
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const long MaxFileBytes = 10 * 1024 * 1024;
    // Column sizes of MasterDataImport (MasterDataImportConfiguration).
    private const int MaxFileNameLength = 260;
    private const int MaxCommentLength = 2000;

    private readonly IMicroLimsDbContext _db;
    private readonly IWorkbookFormat _format;
    private readonly IFileStorageService _storage;
    private readonly IElectronicSignatureService _signatures;
    private readonly TimeProvider _time;
    private readonly ItemService _items;
    private readonly MaterialMasterService _materialMaster;
    private readonly ReferenceOrganismService _referenceOrganisms;
    private readonly IReadOnlyList<SheetHandler> _sheets;
    private readonly MasterDataWorkbookRunner _runner;

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
        ReferenceOrganismService referenceOrganisms,
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
        _referenceOrganisms = referenceOrganisms;
        _sheets = MasterDataSheets.Create(organisms, referenceLists, em, water, materialMaster, referenceOrganisms, mediaProducts, equipment, items, specifications);
        _runner = new MasterDataWorkbookRunner(db, _sheets);
    }

    // A role holding Sections.ViewAll gets every section's records; everyone
    // else only the sections they belong to.
    public async Task<(byte[] Content, string FileName)> DownloadAsync(int userId)
    {
        var scope = await new UserSectionScopeService(_db).GetReadableSectionIdsAsync(userId);
        var sheets = new List<WorkbookSheet>();
        var choices = new List<WorkbookColumnChoices>();
        var help = new List<WorkbookInstructionRow>();

        foreach (var handler in _sheets)
        {
            var columns = handler.Columns.Select(c => c.Name).ToList();
            var rows = (await handler.ExportAsync(_db, scope))
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

    // Dry run: everything a load would do, then rolled back.
    public async Task<WorkbookReport> ValidateAsync(byte[] file, int userId)
    {
        var sheets = ReadSheets(file);
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var (report, _) = await _runner.RunAsync(sheets, userId);
            return report;
        }
        finally
        {
            await tx.RollbackAsync();
            _db.ChangeTracker.Clear();
        }
    }

    private sealed class RejectedException : Exception
    {
        public RejectedException(WorkbookReport report) { Report = report; }
        public WorkbookReport Report { get; }
    }

    // Signed load. The import record is signed FIRST, so a wrong password
    // fails before any row is touched; UnitOfWork keeps the refused
    // attempt on record even though everything else rolls back. Any row
    // error rolls the whole load back - nothing is half-loaded.
    public async Task<MasterDataImportResult> ImportAsync(byte[] file, string fileName, int userId, string password, string? comment, string? ipAddress)
    {
        fileName = Path.GetFileName(fileName);
        if (fileName.Length > MaxFileNameLength)
            throw new InvalidOperationException($"The file name is longer than {MaxFileNameLength} characters. Rename the file and try again.");
        CheckCommentLength(comment);
        var sheets = ReadSheets(file);
        try
        {
            var importId = await UnitOfWork.RunAsync(_db, async () =>
            {
                _db.CurrentUserId = userId;
                var import = new MasterDataImport
                {
                    FileName = fileName,
                    FileSha256 = Convert.ToHexString(SHA256.HashData(file)),
                    ImportedByUserId = userId,
                    ImportedAt = _time.GetUtcNow().UtcDateTime,
                    Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                    Status = MasterDataImportStatus.Loaded,
                };
                _db.MasterDataImports.Add(import);
                await _db.SaveChangesAsync();

                await _signatures.SignAsync(userId, password, SignatureMeaning.MasterDataChanged,
                    ReviewEntityTypes.MasterDataImport, import.Id, import.Comment ?? $"Loaded {import.FileName}", ipAddress);
                await _db.SaveChangesAsync();
                var id = import.Id;

                var (report, created) = await _runner.RunAsync(sheets, userId);
                if (report.HasErrors)
                    throw new RejectedException(report);

                // ponytail: the file is stored before the final commit; if
                // that commit then fails, the stored copy is orphaned (no row
                // points at it). Harmless, and rare.
                var key = await _storage.SaveAsync(
                    $"master-data-imports/{_time.GetUtcNow().UtcDateTime:yyyyMMddHHmmss}_{Guid.NewGuid():N}.xlsx", file);

                var saved = await _db.MasterDataImports.FirstAsync(i => i.Id == id);
                saved.StorageKey = key;
                saved.SummaryJson = JsonSerializer.Serialize(report.Sheets);
                _db.MasterDataImportRecords.AddRange(created.Select(c => new MasterDataImportRecord
                {
                    MasterDataImportId = id, EntityType = c.EntityType, EntityId = c.EntityId, Sheet = c.Sheet, RowNumber = c.RowNumber,
                }));
                await _db.SaveChangesAsync();
                return id;
            });

            var dto = (await LoadDtosAsync(i => i.Id == importId)).Single();
            return new MasterDataImportResult(true, new WorkbookReport(dto.Sheets, Array.Empty<WorkbookRowError>()), dto);
        }
        catch (RejectedException rejected)
        {
            return new MasterDataImportResult(false, rejected.Report, null);
        }
    }

    public Task<List<MasterDataImportDto>> GetImportsAsync() => LoadDtosAsync(_ => true);

    // Second-person verification: a different user checks the load against
    // the approved source documents and signs; that activates the items and
    // material master entries it created.
    public async Task<MasterDataImportDto> VerifyAsync(int id, int userId, string password, string? comment, string? ipAddress)
    {
        CheckCommentLength(comment);
        await UnitOfWork.RunAsync(_db, async () =>
        {
            var import = await _db.MasterDataImports.Include(i => i.Records).FirstOrDefaultAsync(i => i.Id == id)
                ?? throw new NotFoundException($"Master data import {id} not found.");
            if (import.Status != MasterDataImportStatus.Loaded)
                throw new InvalidOperationException("This load has already been verified.");
            if (import.ImportedByUserId == userId)
                throw new InvalidOperationException("The person who loaded the workbook cannot verify it. Ask a second person.");

            // The verifier must be able to see what they verify: material
            // master entries belong to lab sections. Checked before signing.
            var scope = await new UserSectionScopeService(_db).GetConfigurableSectionIdsAsync(userId);
            if (scope is not null)
            {
                var entryIds = import.Records.Where(r => r.EntityType == "MaterialMasterEntry").Select(r => r.EntityId).ToList();
                var outside = await _db.MaterialMasterEntries.Where(e => entryIds.Contains(e.Id) && !scope.Contains(e.SectionId))
                    .Select(e => e.Section!.Code).Distinct().OrderBy(c => c).ToListAsync();
                var strainIds = import.Records.Where(r => r.EntityType == "ReferenceOrganism").Select(r => r.EntityId).ToList();
                outside.AddRange(await _db.ReferenceOrganisms.Where(r => strainIds.Contains(r.Id) && !scope.Contains(r.OwnerSectionId))
                    .Select(r => r.OwnerSection!.Code).Distinct().ToListAsync());
                outside = outside.Distinct().OrderBy(c => c).ToList();
                if (outside.Count > 0)
                    throw new InvalidOperationException(
                        $"This load added material master entries or reference organisms to lab section(s) you are not a member of: {string.Join(", ", outside)}. Ask a Section Head of that section to verify it.");
            }

            _db.CurrentUserId = userId;
            await _signatures.SignAsync(userId, password, SignatureMeaning.Reviewed,
                ReviewEntityTypes.MasterDataImport, id, string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), ipAddress);

            import.Status = MasterDataImportStatus.Verified;
            import.VerifiedByUserId = userId;
            import.VerifiedAt = _time.GetUtcNow().UtcDateTime;
            import.VerificationComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            await _db.SaveChangesAsync();

            foreach (var record in import.Records.Where(r => r.EntityType == "Item"))
                if (await _db.Items.AnyAsync(i => i.Id == record.EntityId))
                    await _items.SetActiveAsync(record.EntityId, true);
            foreach (var record in import.Records.Where(r => r.EntityType == "MaterialMasterEntry"))
                if (await _db.MaterialMasterEntries.AnyAsync(e => e.Id == record.EntityId))
                    await _materialMaster.SetActiveAsync(record.EntityId, true, userId);
            foreach (var record in import.Records.Where(r => r.EntityType == "ReferenceOrganism"))
                if (await _db.ReferenceOrganisms.AnyAsync(e => e.Id == record.EntityId))
                    await _referenceOrganisms.SetActiveAsync(record.EntityId, true, userId);
        });

        return (await LoadDtosAsync(i => i.Id == id)).Single();
    }

    public async Task<(byte[] Content, string FileName)> GetFileAsync(int id)
    {
        var import = await _db.MasterDataImports.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new NotFoundException($"Master data import {id} not found.");
        return (await _storage.ReadAsync(import.StorageKey), import.FileName);
    }

    private async Task<List<MasterDataImportDto>> LoadDtosAsync(System.Linq.Expressions.Expression<Func<MasterDataImport, bool>> filter)
    {
        var imports = await _db.MasterDataImports.AsNoTracking()
            .Include(i => i.ImportedByUser).Include(i => i.VerifiedByUser)
            .Include(i => i.Records.Where(r => r.EntityType == "Item")) // only items are listed
            .Where(filter).OrderByDescending(i => i.ImportedAt).ToListAsync();

        var itemIds = imports.SelectMany(i => i.Records).Where(r => r.EntityType == "Item").Select(r => r.EntityId).Distinct().ToList();
        var withoutSpecs = await _db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id) && !_db.Specifications.Any(s => s.ItemId == i.Id))
            .Select(i => new { i.Id, i.Code }).ToListAsync();

        return imports.Select(i => new MasterDataImportDto(
            i.Id, i.FileName, i.FileSha256, i.ImportedByUser?.FullName ?? string.Empty, i.ImportedAt, i.Comment,
            i.Status.ToString(), i.VerifiedByUser?.FullName, i.VerifiedAt,
            JsonSerializer.Deserialize<List<WorkbookSheetSummary>>(i.SummaryJson) ?? new List<WorkbookSheetSummary>(),
            withoutSpecs.Where(w => i.Records.Any(r => r.EntityType == "Item" && r.EntityId == w.Id)).Select(w => w.Code).ToList()))
            .ToList();
    }

    private static void CheckCommentLength(string? comment)
    {
        if (comment?.Trim().Length > MaxCommentLength)
            throw new InvalidOperationException($"The comment is longer than {MaxCommentLength} characters. Shorten it and sign again.");
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
