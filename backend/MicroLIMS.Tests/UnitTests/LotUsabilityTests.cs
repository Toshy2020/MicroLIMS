using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class LotUsabilityTests
{
    private static Material Lot(int masterEntryId = 10, decimal quantityRemaining = 100, DateTime? expiryDate = null, MaterialUnit unit = MaterialUnit.Gram) => new()
    {
        MaterialMasterEntryId = masterEntryId,
        QuantityRemaining = quantityRemaining,
        ExpiryDate = expiryDate,
        Unit = unit,
        MaterialType = MaterialType.Chemical,
        MaterialName = "Test reagent",
        ManufacturerName = "Acme",
        BatchNumber = "B1",
        Location = "Shelf 1"
    };

    private static readonly DateOnly Today = new(2026, 9, 29);

    [Fact]
    public void Check_LotOfOtherEntry_NotUsable()
    {
        var result = LotUsability.Check(Lot(masterEntryId: 99), masterEntryId: 10, requiredQuantity: null, Today);

        Assert.False(result.Usable);
        Assert.Equal("Lot is not linked to this reagent.", result.Reason);
    }

    [Fact]
    public void Check_ExpiredYesterday_NotUsable()
    {
        var lot = Lot(expiryDate: Today.AddDays(-1).ToDateTime(TimeOnly.MinValue));

        var result = LotUsability.Check(lot, masterEntryId: 10, requiredQuantity: null, Today);

        Assert.False(result.Usable);
        Assert.Contains("Expired on", result.Reason);
    }

    [Fact]
    public void Check_ExpiresToday_StillUsable()
    {
        var lot = Lot(expiryDate: Today.ToDateTime(TimeOnly.MinValue));

        var result = LotUsability.Check(lot, masterEntryId: 10, requiredQuantity: null, Today);

        Assert.True(result.Usable);
    }

    [Fact]
    public void Check_ZeroStock_NotUsable()
    {
        var result = LotUsability.Check(Lot(quantityRemaining: 0), masterEntryId: 10, requiredQuantity: null, Today);

        Assert.False(result.Usable);
        Assert.Equal("No stock left.", result.Reason);
    }

    [Fact]
    public void Check_InsufficientForRequiredQuantity_NotUsable()
    {
        var lot = Lot(quantityRemaining: 5, unit: MaterialUnit.Milliliter);

        var result = LotUsability.Check(lot, masterEntryId: 10, requiredQuantity: 10m, Today);

        Assert.False(result.Usable);
        Assert.Equal("Only 5 Milliliter left.", result.Reason);
    }

    [Fact]
    public void Check_LinkedNotExpiredEnoughStock_Usable()
    {
        var lot = Lot(quantityRemaining: 50, expiryDate: Today.AddDays(30).ToDateTime(TimeOnly.MinValue));

        var result = LotUsability.Check(lot, masterEntryId: 10, requiredQuantity: 20m, Today);

        Assert.True(result.Usable);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Check_NoRequiredQuantity_OnlyExpiryLinkAndStockChecked()
    {
        var lot = Lot(quantityRemaining: 1);

        var result = LotUsability.Check(lot, masterEntryId: 10, requiredQuantity: null, Today);

        Assert.True(result.Usable);
    }
}
