namespace MicroLIMS.Domain.Enums;

// Computed at read time from ExpiryDate/QuantityRemaining (Material.Status)
// - never stored, so it can never drift from the truth. The print view
// (MaterialService.GetForPrintAsync) excludes Expired and Depleted.
public enum StockStatus
{
    InStock,
    Depleted,
    Expired
}
