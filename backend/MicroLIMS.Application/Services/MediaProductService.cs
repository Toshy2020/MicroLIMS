using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public record MediaProductDto(int Id, string Name, string Code, int ConfigurationCount, int BatchCount);

// Master catalog of dehydrated media products. One MediaProduct exists per
// medium, holding a free-text Name and a unique short Code (2-10 chars)
// that prefixes all prepared lot numbers ({Code}/{seq:D2}/{yy}).
//
// Configurations (MediaConfiguration) define the incubation/temperature
// profiles and challenge specifications for this product. Batches
// (Material rows of type DehydratedMedia) snapshot Name and Code at receiving.
// Renaming updates all child configurations to keep their display copy in
// sync, but leaves already-received batch snapshots intact. Changing Code
// requires Section Head electronic signature and reason, leaving already-
// received batches and earlier prepared lots untouched while starting a new
// /01/ series for future lots under the new code.
public class MediaProductService
{
    public const string CodeChangedActionCode = "MediaProductCodeChanged";

    private static readonly Regex CodeRegex = new(@"^[A-Za-z0-9][A-Za-z0-9.-]{1,9}$", RegexOptions.Compiled);

    private readonly MicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatureService;
    private readonly IAuditEventService _auditEventService;

    public MediaProductService(
        MicroLimsDbContext db,
        IElectronicSignatureService signatureService,
        IAuditEventService auditEventService)
    {
        _db = db;
        _signatureService = signatureService;
        _auditEventService = auditEventService;
    }

    public async Task<List<MediaProductDto>> GetAllAsync()
    {
        return await _db.MediaProducts
            .OrderBy(p => p.Name)
            .Select(p => new MediaProductDto(
                p.Id,
                p.Name,
                p.Code,
                p.Configurations.Count,
                _db.Materials.Count(m => m.MediaProductId == p.Id)))
            .ToListAsync();
    }

    public async Task<MediaProduct> CreateAsync(string name, string code)
    {
        var trimmedName = ValidateName(name);
        var trimmedCode = ValidateCode(code);

        await EnsureUniqueNameAsync(trimmedName);
        await EnsureUniqueCodeAsync(trimmedCode);

        var product = new MediaProduct
        {
            Name = trimmedName,
            Code = trimmedCode
        };

        _db.MediaProducts.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<MediaProduct> RenameAsync(int id, string name)
    {
        var trimmedName = ValidateName(name);

        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Media product with ID {id} not found.");

        await EnsureUniqueNameAsync(trimmedName, id);

        product.Name = trimmedName;

        // Keep the display copy on every MediaConfiguration of this product in sync.
        // Material.MaterialName snapshots on received batches are deliberately NOT changed.
        var configs = await _db.MediaConfigurations
            .Where(c => c.MediaProductId == id)
            .ToListAsync();

        foreach (var config in configs)
        {
            config.Name = trimmedName;
        }

        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<MediaProduct> ChangeCodeAsync(
        int id, string newCode, string reason, string password, int userId, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for the code change is required.");
        var trimmedReason = reason.Trim();

        var trimmedCode = ValidateCode(newCode);

        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Media product with ID {id} not found.");

        if (string.Equals(product.Code, trimmedCode, StringComparison.Ordinal))
            throw new InvalidOperationException("The new code must be different from the current code.");

        await EnsureUniqueCodeAsync(trimmedCode, id);

        var oldCode = product.Code;

        // Signs first - a wrong password throws SignatureVerificationException and
        // nothing may change. Follows the exact ordering and pattern of SampleCorrectionService.
        _db.CurrentUserId = userId;
        await _signatureService.SignAsync(
            userId,
            password,
            SignatureMeaning.MasterDataChanged,
            ReviewEntityTypes.MediaProduct,
            id,
            $"Code {oldCode} -> {trimmedCode}: {trimmedReason}",
            ipAddress);

        product.Code = trimmedCode;

        var changes = new[] { new AuditFieldChange("Code", oldCode, trimmedCode) };

        // Persist the code change and record the audit event together.
        // Already-received batches retain their Material.Code snapshot.
        await _auditEventService.RecordUserEventAsync(
            CodeChangedActionCode,
            AuditActionCategory.Configuration,
            ReviewEntityTypes.MediaProduct,
            reason: trimmedReason,
            changes: changes,
            entityId: id.ToString());

        return product;
    }

    public async Task DeleteAsync(int id)
    {
        var product = await _db.MediaProducts.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Media product with ID {id} not found.");

        var configCount = await _db.MediaConfigurations.CountAsync(c => c.MediaProductId == id);
        var batchCount = await _db.Materials.CountAsync(m => m.MediaProductId == id);

        if (configCount > 0 || batchCount > 0)
        {
            throw new InvalidOperationException(
                $"Cannot delete media product '{product.Name}' because it is referenced by {configCount} media configuration(s) and {batchCount} material batch(es).");
        }

        _db.MediaProducts.Remove(product);
        await _db.SaveChangesAsync();
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Media product name is required.");
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
            throw new InvalidOperationException("Media product name cannot exceed 200 characters.");
        return trimmed;
    }

    private static string ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Media product code is required.");
        var trimmed = code.Trim();
        if (!CodeRegex.IsMatch(trimmed))
            throw new InvalidOperationException(
                "Media product code must be 2-10 characters, start with an alphanumeric character, and contain only letters, digits, '.', or '-'.");
        return trimmed;
    }

    private async Task EnsureUniqueNameAsync(string trimmedName, int? excludeId = null)
    {
        var lower = trimmedName.ToLower();
        var clashing = await _db.MediaProducts
            .FirstOrDefaultAsync(p => (excludeId == null || p.Id != excludeId.Value) && p.Name.ToLower() == lower);

        if (clashing != null)
            throw new InvalidOperationException($"A media product named '{clashing.Name}' already exists.");
    }

    private async Task EnsureUniqueCodeAsync(string trimmedCode, int? excludeId = null)
    {
        var lower = trimmedCode.ToLower();
        var clashing = await _db.MediaProducts
            .FirstOrDefaultAsync(p => (excludeId == null || p.Id != excludeId.Value) && p.Code.ToLower() == lower);

        if (clashing != null)
            throw new InvalidOperationException($"A media product with code '{clashing.Code}' already exists ('{clashing.Name}').");
    }
}
