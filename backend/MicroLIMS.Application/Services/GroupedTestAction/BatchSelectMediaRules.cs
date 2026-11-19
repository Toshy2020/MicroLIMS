using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Messages;
using System.Globalization;

namespace MicroLIMS.Application.Services;

// Pure preconditions of a batch media selection, moved out of ExecuteBatchSelectMediaAsync.
public static class BatchSelectMediaRules
{
    // Domain Guard: Explicitly disallow any request targeting a result-entry operation
    public static void EnsureNotResultEntry(string stepName)
    {
        var stepNameLower = stepName.ToLowerInvariant();
        if (stepNameLower.Contains("result") || stepNameLower.Contains("reading") ||
            (stepNameLower.Contains("count") && !stepNameLower.Contains("incubation") && !stepNameLower.Contains("tamc") && !stepNameLower.Contains("tymc")))
        {
            throw new BusinessRuleException(
                MessageCodes.MediaGroupedActionResultEntryBlocked,
                "Grouped actions cannot be executed against analytical result-entry operations. Result entry must be performed individually.");
        }
    }

    public static bool IsMediaExpiredAt(DateTime expiryUtc, DateTime nowUtc) => expiryUtc <= nowUtc;

    // Exactly the statuses the selection gate has always refused; do not widen it here.
    public static bool IsMediaStatusBlocked(MediaStatus status) =>
        status is MediaStatus.OutOfStock or MediaStatus.QuarantineFailed;

    public static void EnsureMediaLotUsable(Media media, DateTime now)
    {
        if (!media.IsReleasedForUse || IsMediaStatusBlocked(media.Status))
        {
            throw new BusinessRuleException(
                MessageCodes.IncubationMediaLotNotReleased,
                $"Media lot \"{media.LotNumber}\" is not released for use, out of stock, or rejected.",
                new Dictionary<string, string> { ["lot"] = media.LotNumber });
        }
        if (IsMediaExpiredAt(media.ExpiryDate, now))
        {
            throw new BusinessRuleException(
                MessageCodes.MediaMediaLotExpired,
                $"Media lot \"{media.LotNumber}\" is expired (expired on {media.ExpiryDate:yyyy-MM-dd}).",
                new Dictionary<string, string> { ["lot"] = media.LotNumber, ["expiryDate"] = media.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }
    }

    public static void EnsureIncubatorUsable([System.Diagnostics.CodeAnalysis.NotNull] Equipment? incubator, int incubatorEquipmentId, DateTime now)
    {
        if (incubator == null)
        {
            throw new BusinessRuleException(
                MessageCodes.MediaIncubatorNotFound,
                $"Incubator equipment {incubatorEquipmentId} was not found or is not an incubator.",
                new Dictionary<string, string> { ["incubatorId"] = incubatorEquipmentId.ToString(CultureInfo.InvariantCulture) });
        }
        if (incubator.SetPointTemperature == null)
        {
            throw new BusinessRuleException(
                MessageCodes.MediaIncubatorNoSetPoint,
                $"Incubator \"{incubator.Code}\" has no calibrated set point temperature.",
                new Dictionary<string, string> { ["code"] = incubator.Code });
        }
        if (incubator.CalibrationDueDate != null && incubator.CalibrationDueDate < now)
        {
            throw new BusinessRuleException(
                MessageCodes.MediaIncubatorCalibrationExpired,
                $"Incubator \"{incubator.Code}\" calibration expired on {incubator.CalibrationDueDate:yyyy-MM-dd}.",
                new Dictionary<string, string> { ["code"] = incubator.Code, ["calibrationDueDate"] = incubator.CalibrationDueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        }
    }

    // Why a ready-to-read open incubation cannot be handled by a grouped action
    // (analytical result entry), or null when it can.
    public static string? ResultEntrySkipReason(TestWorkflowStep activeStep, Incubation activeInc, bool isReady)
    {
        if (!isReady) return null;
        if (activeStep.StepType is StepType.PlateCount && (!activeStep.RequiresIncubationTransfer || activeInc.StageNumber == 2))
            return "Test order requires individual colony count entry. Result entry cannot be grouped.";
        if (activeStep.StepType is StepType.SelectivePlating)
            return "Test order requires individual selective plate observation. Observations cannot be grouped.";
        if (activeStep.StepType is StepType.ConfirmatoryPlating)
            return "Test order requires individual confirmatory readout. Observations cannot be grouped.";
        return null;
    }

    public static string NotPreparedMessage(Sample? sample) =>
        sample?.Category is SampleCategory.EnvironmentalMonitoring or SampleCategory.AfterCleaning
            ? "Sample preparation is not complete (locations/swabs not verified)."
            : "Test Preparation is not complete for this sample.";
}
