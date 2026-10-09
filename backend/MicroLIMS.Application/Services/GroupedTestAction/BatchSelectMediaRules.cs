using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

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
            throw new InvalidOperationException("Grouped actions cannot be executed against analytical result-entry operations. Result entry must be performed individually.");
        }
    }

    public static void EnsureMediaLotUsable(Media media, DateTime now)
    {
        if (!media.IsReleasedForUse || media.Status == MediaStatus.OutOfStock || media.Status == MediaStatus.QuarantineFailed)
        {
            throw new InvalidOperationException($"Media lot \"{media.LotNumber}\" is not released for use, out of stock, or rejected.");
        }
        if (media.ExpiryDate <= now)
        {
            throw new InvalidOperationException($"Media lot \"{media.LotNumber}\" is expired (expired on {media.ExpiryDate:yyyy-MM-dd}).");
        }
    }

    public static void EnsureIncubatorUsable(Equipment? incubator, int incubatorEquipmentId, DateTime now)
    {
        if (incubator == null)
        {
            throw new InvalidOperationException($"Incubator equipment {incubatorEquipmentId} was not found or is not an incubator.");
        }
        if (incubator.SetPointTemperature == null)
        {
            throw new InvalidOperationException($"Incubator \"{incubator.Code}\" has no calibrated set point temperature.");
        }
        if (incubator.CalibrationDueDate != null && incubator.CalibrationDueDate < now)
        {
            throw new InvalidOperationException($"Incubator \"{incubator.Code}\" calibration expired on {incubator.CalibrationDueDate:yyyy-MM-dd}.");
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
