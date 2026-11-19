using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Labels;

/// <summary>
/// Pure result-to-sentence usability mappers for barcode label lookups.
/// </summary>
public static class LabelUsability
{
    public static (bool Usable, string? Reason) Media(
        bool isReleasedForUse,
        DateTime? expiryUtc,
        DateTime nowUtc,
        MediaStatus status = MediaStatus.Active)
    {
        if (status == MediaStatus.Destroyed)
        {
            return (false, "Destroyed.");
        }

        if (status == MediaStatus.QuarantineFailed)
        {
            return (false, "Rejected.");
        }

        if (status == MediaStatus.OutOfStock)
        {
            return (false, "Out of stock.");
        }

        if (!isReleasedForUse)
        {
            return (false, "Not released for use.");
        }

        if (status == MediaStatus.Expired || (expiryUtc.HasValue && BatchSelectMediaRules.IsMediaExpiredAt(expiryUtc.Value, nowUtc)))
        {
            return (false, "Expired.");
        }

        return (true, null);
    }

    public static (bool Usable, string? Reason) Solution(
        string effectiveStatus,
        ApprovalGateStatus? approvalStatus = null)
    {
        if (!Enum.TryParse<SolutionPreparationStatus>(effectiveStatus, ignoreCase: true, out var status))
        {
            return (false, "Not prepared.");
        }

        if (status == SolutionPreparationStatus.Expired)
        {
            return (false, "Expired.");
        }

        if (status == SolutionPreparationStatus.InProgress)
        {
            return (false, "In progress.");
        }

        if (status == SolutionPreparationStatus.Discarded)
        {
            return (false, "Discarded.");
        }

        if (status == SolutionPreparationStatus.Cancelled)
        {
            return (false, "Cancelled.");
        }

        if (status != SolutionPreparationStatus.Prepared)
        {
            return (false, "Not prepared.");
        }

        return approvalStatus switch
        {
            ApprovalGateStatus.PendingReview => (false, "Awaiting approval."),
            ApprovalGateStatus.Rejected => (false, "Rejected."),
            _ => (true, null)
        };
    }

    public static (bool Usable, string? Reason) Cryovial(
        bool approved,
        int vialsRemaining,
        bool isDestroyed = false,
        bool isExpired = false)
    {
        if (isDestroyed)
        {
            return (false, "Destroyed.");
        }

        if (!approved)
        {
            return (false, "Not approved for use.");
        }

        if (isExpired)
        {
            return (false, "Expired.");
        }

        if (vialsRemaining <= 0)
        {
            return (false, "No vials remaining.");
        }

        return (true, null);
    }

    public static (bool Usable, string? Reason) StockLot(
        bool isUsable,
        bool expired,
        bool depleted,
        bool awaitingRelease = false,
        bool rejected = false)
    {
        if (expired)
        {
            return (false, "Expired.");
        }

        if (depleted)
        {
            return (false, "Depleted.");
        }

        if (rejected)
        {
            return (false, "Rejected.");
        }

        if (awaitingRelease)
        {
            return (false, "Awaiting release.");
        }

        return isUsable ? (true, null) : (false, "Not usable.");
    }

    public static (bool Usable, string? Reason) Equipment(EquipmentOperationalStatus status)
    {
        if (EquipmentInventoryService.IsInService(status))
        {
            return (true, null);
        }

        return status switch
        {
            EquipmentOperationalStatus.OutOfService => (false, "Out of service."),
            EquipmentOperationalStatus.Retired => (false, "Retired."),
            _ => (false, "Not in service.")
        };
    }
}
