using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// Materials Stock register under Inventory - one row per received batch/lot
// of a lab material (dehydrated media, discs, kits, reagents, chemicals,
// indicators, reference buffers, disposable tools). Mirrors the existing
// paper/Excel "List of materials in Microbiology Lab".
//
// Reference strains are intentionally NOT a MaterialType here (design
// fork resolved as Option B - separate tables, unified only in the
// Inventory UI): ReferenceStrain keeps its own QC lifecycle (identity
// confirmation, approval gate, passage history) and its own
// DiscsRemaining consumption field. See ReferenceStrain.cs.
//
// QuantityReceived is set once at receiving and never changes -
// QuantityRemaining is the live, decrementable balance. Consumption
// happens through MaterialService.ConsumeAsync (called from
// MediaPreparationService.PrepareAsync for DehydratedMedia), never by
// editing QuantityRemaining directly from the list screen.
//
// Every insert/update is captured automatically by
// MicroLimsDbContext.SaveChanges into AuditLog (Frozen Principle #5).
public class Material
{
    public int Id { get; set; }

    public MaterialType MaterialType { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;

    // Named BatchNumber (not BatchLotNumber) on purpose: MicroLimsDbContext's
    // audit capture reads this exact property name into AuditLog.BatchNumber,
    // so every material's batch/lot becomes searchable from Audit Search
    // for free (Frozen Principle #5).
    public string BatchNumber { get; set; } = string.Empty;

    public DateTime ReceivingDate { get; set; }
    public DateTime? ExpiryDate { get; set; } // nullable - a few rows in the source list have no expiry (e.g. NaCl)
    public string? Code { get; set; }
    public string Location { get; set; } = string.Empty;

    public decimal QuantityReceived { get; set; }
    public decimal QuantityRemaining { get; set; }
    public MaterialUnit Unit { get; set; }
    public decimal? MinimumStockLevel { get; set; } // optional low-stock threshold, not a hard gate

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int LastModifiedByUserId { get; set; }
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

    // Not mapped - computed from live data so it can never drift from
    // the truth. The print view (MaterialService.GetForPrintAsync)
    // excludes Expired and Depleted.
    public StockStatus Status =>
        ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.UtcNow.Date ? StockStatus.Expired :
        QuantityRemaining <= 0 ? StockStatus.Depleted :
        StockStatus.InStock;

    // Used by MediaPreparationService's consumption guard before any
    // mutation - same shape as EquipmentGuard-style checks elsewhere.
    public bool IsUsable => Status == StockStatus.InStock;
}
