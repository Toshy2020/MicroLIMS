using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Helpers;

public record LotCheck(bool Usable, string? Reason);

// Decides which Material stock lots the Solution Preparation area may pick
// (HPLC chain S4, spec 4). requiredQuantity null means only linkage, expiry
// and "some stock left" are checked (e.g. listing lots for a picker before
// a quantity has been entered yet).
public static class LotUsability
{
    public static LotCheck Check(Material lot, int masterEntryId, decimal? requiredQuantity, DateOnly today)
    {
        if (lot.MaterialMasterEntryId != masterEntryId)
            return new(false, "Lot is not linked to this reagent.");

        if (lot.ExpiryDate.HasValue && DateOnly.FromDateTime(lot.ExpiryDate.Value) < today)
            return new(false, $"Expired on {lot.ExpiryDate:yyyy-MM-dd}.");

        if (lot.QuantityRemaining <= 0)
            return new(false, "No stock left.");

        if (requiredQuantity.HasValue && lot.QuantityRemaining < requiredQuantity.Value)
            return new(false, $"Only {lot.QuantityRemaining} {lot.Unit} left.");

        return new(true, null);
    }
}
