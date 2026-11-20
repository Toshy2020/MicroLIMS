using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services;

// Read-only: resolves Item.SopNumber to the Active document with that company
// code and reports whether its effective revision has a controlled PDF.
public class ItemStmService(IMicroLimsDbContext db)
{
    private readonly ItemSopResolver _resolver = new(db);

    public async Task<ItemStmResponse> GetForItemAsync(int itemId, int userId, CancellationToken cancellationToken = default)
    {
        var sop = await _resolver.GetSopNumberAsync(itemId, cancellationToken);
        if (sop.Length == 0)
            return new(ItemStmStatus.NotLinked, null, null, null, null, null, null, null, null, null, false);

        var canDownload = await CanDownloadAsync(userId, cancellationToken);
        return await _resolver.ResolveForItemStmAsync(sop, canDownload, cancellationToken);
    }

    // Same rule as DocumentFileService (controlled copy policy): download is
    // allowed unless the setting is "false", except for section heads and roles
    // holding Audit.View (read-only oversight; System Administrator is not exempt by role).
    private async Task<bool> CanDownloadAsync(int userId, CancellationToken cancellationToken)
    {
        var setting = await db.ConfigurationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SettingKey == "DocumentControl.ControlledCopy.DownloadPermitted", cancellationToken);
        if (setting == null || !string.Equals(setting.SettingValue, "false", StringComparison.OrdinalIgnoreCase))
            return true;

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.RoleId, Type = u.Role != null ? (RoleType?)u.Role.Type : null })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null) return false;
        return user.Type == RoleType.SectionHead
            || await db.RolePermissions.AsNoTracking()
                .AnyAsync(rp => rp.RoleId == user.RoleId && rp.Permission!.Code == PermissionConstants.AuditView, cancellationToken);
    }
}
