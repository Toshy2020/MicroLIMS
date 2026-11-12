using MicroLIMS.Application.Labels;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class LabelUsabilityTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Media_not_released_is_blocked_with_reason()
    {
        var result = LabelUsability.Media(isReleasedForUse: false, expiryUtc: NowUtc.AddDays(30), NowUtc);

        Assert.False(result.Usable);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Equal("Not released for use.", result.Reason);
    }

    [Fact]
    public void Media_expired_at_now_is_blocked()
    {
        var media = new Media
        {
            LotNumber = "MED-BOUNDARY",
            IsReleasedForUse = true,
            Status = MediaStatus.Active,
            ExpiryDate = NowUtc
        };

        // Assert against the existing domain rule in BatchSelectMediaRules: expiry == now throws (expired).
        Assert.Throws<BusinessRuleException>(() => BatchSelectMediaRules.EnsureMediaLotUsable(media, NowUtc));

        var result = LabelUsability.Media(media.IsReleasedForUse, media.ExpiryDate, NowUtc, media.Status);
        Assert.False(result.Usable);
        Assert.Equal("Expired.", result.Reason);
    }

    [Theory]
    [InlineData(MediaStatus.OutOfStock, "Out of stock.")]
    [InlineData(MediaStatus.QuarantineFailed, "Rejected.")]
    [InlineData(MediaStatus.Destroyed, "Destroyed.")]
    [InlineData(MediaStatus.Expired, "Expired.")]
    public void Media_blocked_statuses_return_expected_reason(MediaStatus status, string expectedReason)
    {
        var result = LabelUsability.Media(isReleasedForUse: true, expiryUtc: NowUtc.AddDays(10), NowUtc, status);

        Assert.False(result.Usable);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Media_released_active_and_unexpired_is_usable()
    {
        var result = LabelUsability.Media(isReleasedForUse: true, expiryUtc: NowUtc.AddMinutes(1), NowUtc, MediaStatus.Active);

        Assert.True(result.Usable);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData(nameof(SolutionPreparationStatus.Expired), null, "Expired.")]
    [InlineData(nameof(SolutionPreparationStatus.InProgress), null, "In progress.")]
    [InlineData(nameof(SolutionPreparationStatus.Discarded), null, "Discarded.")]
    [InlineData(nameof(SolutionPreparationStatus.Cancelled), null, "Cancelled.")]
    [InlineData(nameof(SolutionPreparationStatus.Prepared), ApprovalGateStatus.PendingReview, "Awaiting approval.")]
    [InlineData(nameof(SolutionPreparationStatus.Prepared), ApprovalGateStatus.Rejected, "Rejected.")]
    public void Solution_non_prepared_or_unreleased_is_blocked_with_reason(
        string effectiveStatus,
        ApprovalGateStatus? approvalStatus,
        string expectedReason)
    {
        var result = LabelUsability.Solution(effectiveStatus, approvalStatus);

        Assert.False(result.Usable);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ApprovalGateStatus.Approved)]
    public void Solution_prepared_without_pending_or_rejected_gate_is_usable(ApprovalGateStatus? approvalStatus)
    {
        var result = LabelUsability.Solution(nameof(SolutionPreparationStatus.Prepared), approvalStatus);

        Assert.True(result.Usable);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData(false, 5, false, false, "Not approved for use.")]
    [InlineData(true, 0, false, false, "No vials remaining.")]
    [InlineData(true, 5, true, false, "Destroyed.")]
    [InlineData(true, 5, false, true, "Expired.")]
    public void Cryovial_blocked_states_return_expected_reason(
        bool approved,
        int vialsRemaining,
        bool isDestroyed,
        bool isExpired,
        string expectedReason)
    {
        var result = LabelUsability.Cryovial(approved, vialsRemaining, isDestroyed, isExpired);

        Assert.False(result.Usable);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Cryovial_approved_active_unexpired_with_vials_is_usable()
    {
        var result = LabelUsability.Cryovial(approved: true, vialsRemaining: 1, isDestroyed: false, isExpired: false);

        Assert.True(result.Usable);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData(false, true, false, false, false, "Expired.")]
    [InlineData(false, false, true, false, false, "Depleted.")]
    [InlineData(false, false, false, true, false, "Awaiting release.")]
    [InlineData(false, false, false, false, true, "Rejected.")]
    [InlineData(false, false, false, false, false, "Not usable.")]
    public void StockLot_blocked_states_return_expected_reason(
        bool isUsable,
        bool expired,
        bool depleted,
        bool awaitingRelease,
        bool rejected,
        string expectedReason)
    {
        var result = LabelUsability.StockLot(isUsable, expired, depleted, awaitingRelease, rejected);

        Assert.False(result.Usable);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void StockLot_in_stock_and_usable_is_usable()
    {
        var result = LabelUsability.StockLot(isUsable: true, expired: false, depleted: false);

        Assert.True(result.Usable);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData(EquipmentOperationalStatus.OutOfService, "Out of service.")]
    [InlineData(EquipmentOperationalStatus.Retired, "Retired.")]
    public void Equipment_out_of_service_or_retired_is_blocked(EquipmentOperationalStatus status, string expectedReason)
    {
        var result = LabelUsability.Equipment(status);

        Assert.False(result.Usable);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Fact]
    public void Equipment_in_service_is_usable()
    {
        var result = LabelUsability.Equipment(EquipmentOperationalStatus.InService);

        Assert.True(result.Usable);
        Assert.Null(result.Reason);
    }
}
