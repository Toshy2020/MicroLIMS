using System.Globalization;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace MicroLIMS.Application.Labels;

public sealed record LabelRecord(
    LabelEntityType Type,
    int Id,
    string? Code,
    string DisplayName,
    string Status,
    bool Usable,
    string? BlockedReason,
    int? SampleId,
    string CreatePermission,
    LabelContent? Content,
    int? LinkedEquipmentId);

public interface ILabelDataSource
{
    // null when the record does not exist OR the user may not read it
    Task<LabelRecord?> FindByIdAsync(LabelEntityType type, int id, int userId, CancellationToken ct);
    Task<LabelRecord?> FindByCodeAsync(LabelEntityType type, string code, int userId, CancellationToken ct);
}

/// <summary>
/// Section-scoped, read-only lookup of label records and their printable fields.
/// </summary>
public sealed class LabelDataSource(
    IMicroLimsDbContext db,
    IUserSectionScopeService scope,
    ILabClock clock) : ILabelDataSource
{
    private readonly IMicroLimsDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly IUserSectionScopeService _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    private readonly ILabClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public Task<LabelRecord?> FindByIdAsync(LabelEntityType type, int id, int userId, CancellationToken ct) =>
        type switch
        {
            LabelEntityType.Sample => FindSampleByIdAsync(id, userId, ct),
            LabelEntityType.MediaLot => FindMediaByIdAsync(id, userId, ct),
            LabelEntityType.SolutionPreparation => FindSolutionByIdAsync(id, userId, ct),
            LabelEntityType.Cryovial => FindCryovialByIdAsync(id, userId, ct),
            LabelEntityType.MaterialStockLot => FindMaterialByIdAsync(id, userId, ct),
            LabelEntityType.EquipmentAsset => FindEquipmentByIdAsync(id, userId, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported label entity type.")
        };

    public async Task<LabelRecord?> FindByCodeAsync(LabelEntityType type, string code, int userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalizedLower = code.Trim().ToLowerInvariant();
        int? id = type switch
        {
            LabelEntityType.Sample => await _db.Samples.AsNoTracking()
                .Where(s => s.ReferenceNumber.ToLower() == normalizedLower)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct),
            LabelEntityType.MediaLot => await _db.Media.AsNoTracking()
                .Where(m => m.LotNumber.ToLower() == normalizedLower)
                .Select(m => (int?)m.Id)
                .FirstOrDefaultAsync(ct),
            LabelEntityType.SolutionPreparation => await _db.SolutionPreparations.AsNoTracking()
                .Where(p => p.Code != null && p.Code.ToLower() == normalizedLower)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct),
            LabelEntityType.Cryovial => await _db.Cryovials.AsNoTracking()
                .Where(c => c.Code.ToLower() == normalizedLower)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct),
            LabelEntityType.MaterialStockLot => await _db.Materials.AsNoTracking()
                .Where(m => m.StockId != null && m.StockId.ToLower() == normalizedLower)
                .Select(m => (int?)m.Id)
                .FirstOrDefaultAsync(ct),
            LabelEntityType.EquipmentAsset => await _db.EquipmentInventories.AsNoTracking()
                .Where(e => e.Code.ToLower() == normalizedLower)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync(ct),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported label entity type.")
        };

        return id.HasValue ? await FindByIdAsync(type, id.Value, userId, ct) : null;
    }

    private async Task<LabelRecord?> FindSampleByIdAsync(int id, int userId, CancellationToken ct)
    {
        if (!await CanReadAsync(() => _scope.EnsureSampleAccessAsync(userId, id, ct, SectionAccess.Read)))
        {
            return null;
        }

        var row = await _db.Samples
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.ReferenceNumber,
                s.Category,
                ItemName = s.Item != null ? s.Item.Name : null,
                MachineName = s.Machine != null ? s.Machine.Name : null,
                WaterPointCode = s.WaterSamplingPoint != null ? s.WaterSamplingPoint.Code : null,
                DepartmentName = s.Department != null ? s.Department.Name : null,
                s.BatchNumber,
                s.ReceivedAt,
                s.Status
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var displayName = row.ItemName
            ?? row.MachineName
            ?? row.WaterPointCode
            ?? row.DepartmentName
            ?? $"{row.Category} Sample";

        var standardFields = new List<LabelField>(3)
        {
            new("Item", displayName)
        };
        if (!string.IsNullOrWhiteSpace(row.BatchNumber))
        {
            standardFields.Add(new("Batch", row.BatchNumber.Trim()));
        }
        standardFields.Add(new("Received", FormatLabDate(row.ReceivedAt)));

        var content = new LabelContent(
            LabelEntityType.Sample,
            row.ReferenceNumber,
            standardFields,
            Array.Empty<LabelField>());

        return new LabelRecord(
            LabelEntityType.Sample,
            row.Id,
            row.ReferenceNumber,
            displayName,
            row.Status.ToString(),
            Usable: true,
            BlockedReason: null,
            SampleId: row.Id,
            CreatePermission: PermissionConstants.SamplesReceiveOwnLab,
            Content: content,
            LinkedEquipmentId: null);
    }

    private async Task<LabelRecord?> FindMediaByIdAsync(int id, int userId, CancellationToken ct)
    {
        if (!await CanReadAsync(() => _scope.EnsureMediaAccessAsync(userId, id, ct, SectionAccess.Read)))
        {
            return null;
        }

        var row = await _db.Media
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new
            {
                m.Id,
                m.LotNumber,
                MediaName = m.Material != null ? m.Material.MaterialName : null,
                m.PreparedAt,
                m.ExpiryDate,
                m.PreparedByUserId,
                m.Status,
                m.IsReleasedForUse,
                PreparedByFullName = _db.Users
                    .Where(u => u.Id == m.PreparedByUserId)
                    .Select(u => u.FullName)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var (usable, blockedReason) = LabelUsability.Media(row.IsReleasedForUse, row.ExpiryDate, nowUtc, row.Status);
        var displayName = !string.IsNullOrWhiteSpace(row.MediaName) ? row.MediaName : row.LotNumber;
        var expiryText = FormatLabDate(row.ExpiryDate);

        var standardFields = new List<LabelField>(4)
        {
            new("Media", displayName),
            new("Prepared", FormatLabDate(row.PreparedAt)),
            new("Expiry", expiryText)
        };
        var initials = InitialsOf(row.PreparedByFullName);
        if (!string.IsNullOrEmpty(initials))
        {
            standardFields.Add(new("By", initials));
        }

        var content = new LabelContent(
            LabelEntityType.MediaLot,
            row.LotNumber,
            standardFields,
            new LabelField[] { new("Expiry", expiryText) });

        return new LabelRecord(
            LabelEntityType.MediaLot,
            row.Id,
            row.LotNumber,
            displayName,
            row.Status.ToString(),
            usable,
            blockedReason,
            SampleId: null,
            CreatePermission: PermissionConstants.MediaPrepare,
            Content: content,
            LinkedEquipmentId: null);
    }

    private async Task<LabelRecord?> FindSolutionByIdAsync(int id, int userId, CancellationToken ct)
    {
        var row = await _db.SolutionPreparations
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.SectionId,
                p.Code,
                MasterName = p.SolutionMaster != null ? p.SolutionMaster.Name : null,
                p.RecipeSnapshotJson,
                p.Status,
                p.PreparedAt,
                p.ExpiresAt,
                p.ApprovalStatus,
                PreparedByFullName = p.PreparedByUserId.HasValue
                    ? _db.Users.Where(u => u.Id == p.PreparedByUserId.Value).Select(u => u.FullName).FirstOrDefault()
                    : null
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var readableSections = await _scope.GetReadableSectionIdsAsync(userId, ct);
        if (readableSections is not null && !readableSections.Contains(row.SectionId))
        {
            return null;
        }

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var usability = SolutionPreparationService.EvaluateUsability(
            new SolutionPreparation
            {
                Status = row.Status,
                ExpiresAt = row.ExpiresAt,
                ApprovalStatus = row.ApprovalStatus
            },
            nowUtc);

        var (usable, blockedReason) = LabelUsability.Solution(
            usability.EffectiveStatus.ToString(),
            usability.ApprovalStatus);

        var snapshot = SolutionPreparationService.TryReadSnapshot(row.RecipeSnapshotJson);
        var code = string.IsNullOrWhiteSpace(row.Code) ? null : row.Code;
        var displayName = !string.IsNullOrWhiteSpace(snapshot?.Name)
            ? snapshot.Name
            : !string.IsNullOrWhiteSpace(row.MasterName)
                ? row.MasterName
                : code ?? $"#{row.Id}";

        LabelContent? content = null;
        if (code is not null)
        {
            var standardFields = new List<LabelField>(5)
            {
                new("Solution", displayName)
            };
            if (row.PreparedAt.HasValue)
            {
                standardFields.Add(new("Prepared", FormatLabDate(row.PreparedAt.Value)));
            }
            if (row.ExpiresAt.HasValue)
            {
                standardFields.Add(new("Expiry", FormatLabDate(row.ExpiresAt.Value)));
            }
            var initials = InitialsOf(row.PreparedByFullName);
            if (!string.IsNullOrEmpty(initials))
            {
                standardFields.Add(new("By", initials));
            }
            if (!string.IsNullOrWhiteSpace(snapshot?.StorageCondition))
            {
                standardFields.Add(new("Storage", snapshot.StorageCondition.Trim()));
            }

            IReadOnlyList<LabelField> smallFields = row.ExpiresAt.HasValue
                ? new LabelField[] { new("Expiry", FormatLabDate(row.ExpiresAt.Value)) }
                : Array.Empty<LabelField>();

            content = new LabelContent(LabelEntityType.SolutionPreparation, code, standardFields, smallFields);
        }

        return new LabelRecord(
            LabelEntityType.SolutionPreparation,
            row.Id,
            code,
            displayName,
            usability.EffectiveStatus.ToString(),
            usable,
            blockedReason,
            SampleId: null,
            CreatePermission: PermissionConstants.SolutionsPrepare,
            Content: content,
            LinkedEquipmentId: null);
    }

    private async Task<LabelRecord?> FindCryovialByIdAsync(int id, int userId, CancellationToken ct)
    {
        if (!await CanReadAsync(() => _scope.EnsureCryovialAccessAsync(userId, id, ct, SectionAccess.Read)))
        {
            return null;
        }

        var row = await _db.Cryovials
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.OrganismNameSnapshot,
                OrganismName = c.Organism != null ? c.Organism.ScientificName : null,
                c.PreparedAt,
                c.ExpiryDate,
                c.VialsRemaining,
                c.ApprovalStatus,
                c.IsDestroyed
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var check = CryovialThaw.EvaluateUsability(
            row.ApprovalStatus,
            row.IsDestroyed,
            row.ExpiryDate,
            row.VialsRemaining,
            nowUtc);

        var (usable, blockedReason) = LabelUsability.Cryovial(
            check.IsApproved,
            check.VialsRemaining,
            check.IsDestroyed,
            check.IsExpired);

        var displayName = !string.IsNullOrWhiteSpace(row.OrganismNameSnapshot)
            ? row.OrganismNameSnapshot
            : !string.IsNullOrWhiteSpace(row.OrganismName)
                ? row.OrganismName
                : row.Code;

        var expiryText = FormatLabDate(row.ExpiryDate);
        var content = new LabelContent(
            LabelEntityType.Cryovial,
            row.Code,
            new LabelField[]
            {
                new("Organism", displayName),
                new("Prepared", FormatLabDate(row.PreparedAt)),
                new("Expiry", expiryText)
            },
            new LabelField[] { new("Expiry", expiryText) });

        return new LabelRecord(
            LabelEntityType.Cryovial,
            row.Id,
            row.Code,
            displayName,
            row.IsDestroyed ? "Destroyed" : row.ApprovalStatus.ToString(),
            usable,
            blockedReason,
            SampleId: null,
            CreatePermission: PermissionConstants.CryovialsManage,
            Content: content,
            LinkedEquipmentId: null);
    }

    private async Task<LabelRecord?> FindMaterialByIdAsync(int id, int userId, CancellationToken ct)
    {
        if (!await CanReadAsync(() => _scope.EnsureMaterialAccessAsync(userId, id, ct, SectionAccess.Read)))
        {
            return null;
        }

        var row = await _db.Materials
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new
            {
                m.Id,
                m.StockId,
                m.Code,
                m.MaterialType,
                m.MaterialName,
                m.BatchNumber,
                m.ReceivingDate,
                m.ExpiryDate,
                m.StorageCondition,
                m.QuantityRemaining,
                m.ApprovalStatus
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var material = new Material
        {
            ExpiryDate = row.ExpiryDate,
            QuantityRemaining = row.QuantityRemaining,
            ApprovalStatus = row.ApprovalStatus
        };

        var nowUtc = _clock.UtcNow.UtcDateTime;
        var stockStatus = material.StatusAt(nowUtc);
        var isUsable = material.IsUsableAt(nowUtc);
        var (usable, blockedReason) = LabelUsability.StockLot(
            isUsable,
            stockStatus == StockStatus.Expired,
            stockStatus == StockStatus.Depleted,
            material.IsAwaitingRelease,
            material.IsRejected);

        var code = string.IsNullOrWhiteSpace(row.StockId) ? null : row.StockId;
        var displayName = row.MaterialType == MaterialType.WorkingStandard && !string.IsNullOrWhiteSpace(row.Code)
            ? $"{row.MaterialName} ({row.Code})"
            : row.MaterialName;

        LabelContent? content = null;
        if (code is not null)
        {
            var standardFields = new List<LabelField>(5)
            {
                new("Material", displayName)
            };
            if (!string.IsNullOrWhiteSpace(row.BatchNumber))
            {
                standardFields.Add(new("Batch", row.BatchNumber.Trim()));
            }
            standardFields.Add(new("Received", FormatLabDate(row.ReceivingDate)));
            if (row.ExpiryDate.HasValue)
            {
                standardFields.Add(new("Expiry", FormatLabDate(row.ExpiryDate.Value)));
            }
            if (!string.IsNullOrWhiteSpace(row.StorageCondition))
            {
                standardFields.Add(new("Storage", row.StorageCondition.Trim()));
            }

            IReadOnlyList<LabelField> smallFields = row.ExpiryDate.HasValue
                ? new LabelField[] { new("Expiry", FormatLabDate(row.ExpiryDate.Value)) }
                : Array.Empty<LabelField>();

            content = new LabelContent(LabelEntityType.MaterialStockLot, code, standardFields, smallFields);
        }

        return new LabelRecord(
            LabelEntityType.MaterialStockLot,
            row.Id,
            code,
            displayName,
            stockStatus.ToString(),
            usable,
            blockedReason,
            SampleId: null,
            CreatePermission: PermissionConstants.MaterialsManage,
            Content: content,
            LinkedEquipmentId: null);
    }

    private async Task<LabelRecord?> FindEquipmentByIdAsync(int id, int userId, CancellationToken ct)
    {
        if (!await CanReadAsync(() => _scope.EnsureEquipmentInventoryAccessAsync(userId, id, ct, SectionAccess.Read)))
        {
            return null;
        }

        var row = await _db.EquipmentInventories
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id,
                e.Code,
                e.InstrumentType,
                e.Location,
                e.Status,
                LinkedEquipmentId = _db.Equipment
                    .Where(eq => eq.EquipmentInventoryId == e.Id)
                    .Select(eq => (int?)eq.Id)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var (usable, blockedReason) = LabelUsability.Equipment(row.Status);
        var displayName = $"{row.Code} — {row.InstrumentType}";

        var standardFields = new List<LabelField>(2)
        {
            new("Type", row.InstrumentType)
        };
        if (!string.IsNullOrWhiteSpace(row.Location))
        {
            standardFields.Add(new("Location", row.Location.Trim()));
        }

        var content = new LabelContent(
            LabelEntityType.EquipmentAsset,
            row.Code,
            standardFields,
            Array.Empty<LabelField>());

        return new LabelRecord(
            LabelEntityType.EquipmentAsset,
            row.Id,
            row.Code,
            displayName,
            row.Status.ToString(),
            usable,
            blockedReason,
            SampleId: null,
            CreatePermission: PermissionConstants.EquipmentManage,
            Content: content,
            LinkedEquipmentId: row.LinkedEquipmentId);
    }

    private static async Task<bool> CanReadAsync(Func<Task> ensureAccess)
    {
        try
        {
            await ensureAccess();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string FormatLabDate(DateTime utcDateTime) =>
        _clock.ToLabLocal(utcDateTime).ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    internal static string InitialsOf(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return string.Empty;
        }

        var parts = fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0])));
    }
}
