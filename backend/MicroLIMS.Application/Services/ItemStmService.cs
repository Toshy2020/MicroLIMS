using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// Read-only: resolves Item.SopNumber to the Active document with that company
// code and reports whether its effective revision has a controlled PDF.
public class ItemStmService(IMicroLimsDbContext db)
{
    public async Task<ItemStmResponse> GetForItemAsync(int itemId, int userId)
    {
        var item = await db.Items.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.SopNumber })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Item {itemId} not found.");

        var sop = (item.SopNumber ?? string.Empty).Trim();
        if (sop.Length == 0)
            return Empty(ItemStmStatus.NotLinked, null, false);

        var canDownload = await CanDownloadAsync(userId);

        var key = sop.ToUpperInvariant();
        var master = await db.DocumentMasters.AsNoTracking()
            .Where(m => m.RecordStatus == DocumentRecordStatus.Active && m.CompanyDocumentCode.ToUpper() == key)
            .Select(m => new
            {
                m.Id,
                m.CompanyDocumentCode,
                m.Title,
                TypeCode = m.DocumentType.Code,
                Revision = m.CurrentEffectiveRevision == null
                    || m.CurrentEffectiveRevision.RevisionStatus != DocumentRevisionStatus.Effective
                    ? null
                    : new
                    {
                        m.CurrentEffectiveRevision.RevisionNumber,
                        m.CurrentEffectiveRevision.EffectiveDate,
                        Pdf = m.CurrentEffectiveRevision.Files
                            .Where(f => f.FileRole == FileRole.ControlledPdf && f.IsActive)
                            .OrderByDescending(f => f.FileVersion)
                            .Select(f => new { f.Id, f.FileName })
                            .FirstOrDefault()
                    }
            })
            .FirstOrDefaultAsync();

        if (master == null)
            return Empty(ItemStmStatus.NotFound, sop, canDownload);

        if (master.Revision?.Pdf == null)
            return new ItemStmResponse(ItemStmStatus.NoEffectiveRevision, sop, master.Id, master.CompanyDocumentCode,
                master.Title, master.TypeCode, null, null, null, null, canDownload);

        return new ItemStmResponse(ItemStmStatus.Effective, sop, master.Id, master.CompanyDocumentCode,
            master.Title, master.TypeCode, master.Revision.RevisionNumber, master.Revision.EffectiveDate,
            master.Revision.Pdf.Id, master.Revision.Pdf.FileName, canDownload);
    }

    private static ItemStmResponse Empty(ItemStmStatus status, string? sop, bool canDownload) =>
        new(status, sop, null, null, null, null, null, null, null, null, canDownload);

    // Same rule as DocumentFileService (controlled copy policy): download is
    // allowed unless the setting is "false", except for admins and section heads.
    private async Task<bool> CanDownloadAsync(int userId)
    {
        var setting = await db.ConfigurationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == "DocumentControl.ControlledCopy.DownloadPermitted");
        if (setting == null || !string.Equals(setting.SettingValue, "false", StringComparison.OrdinalIgnoreCase))
            return true;

        var role = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Role != null ? (RoleType?)u.Role.Type : null)
            .FirstOrDefaultAsync();
        return role == RoleType.SystemAdministrator || role == RoleType.SectionHead;
    }
}
