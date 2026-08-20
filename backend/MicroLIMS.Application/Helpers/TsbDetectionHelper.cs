using System;
using System.Collections.Generic;
using System.Linq;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Helpers;

public static class TsbDetectionHelper
{
    /// <summary>
    /// Returns true if shared TSB enrichment is complete
    /// (minHours elapsed OR manually completed).
    /// </summary>
    public static bool IsTsbComplete(
        Incubation? sharedTsb,
        decimal minHours,
        DateTime utcNow)
    {
        if (sharedTsb == null) return false;
        if (sharedTsb.CompletedAt.HasValue) return true;
        DateTime? start = sharedTsb.IncubationStartUtc ?? sharedTsb.StartedAt;
        if (!start.HasValue) return false;

        var minReadyAt = start.Value.AddHours((double)minHours);
        return utcNow >= minReadyAt;
    }

    /// <summary>
    /// Returns true if shared TSB enrichment is currently incubating
    /// (started, minHours NOT yet elapsed, not manually completed).
    /// </summary>
    public static bool IsTsbIncubating(
        Incubation? sharedTsb,
        decimal minHours,
        DateTime utcNow)
    {
        if (sharedTsb == null) return false;
        if (sharedTsb.CompletedAt.HasValue) return false;
        DateTime? start = sharedTsb.IncubationStartUtc ?? sharedTsb.StartedAt;
        if (!start.HasValue) return false;

        var minReadyAt = start.Value.AddHours((double)minHours);
        return utcNow < minReadyAt;
    }

    /// <summary>
    /// Computes minReadyAt for shared TSB.
    /// </summary>
    public static DateTime? GetTsbMinReadyAt(
        Incubation? sharedTsb,
        decimal minHours)
    {
        DateTime? start = sharedTsb?.IncubationStartUtc ?? sharedTsb?.StartedAt;
        if (!start.HasValue) return null;
        return start.Value.AddHours((double)minHours);
    }

    /// <summary>
    /// Helper to find shared TSB incubation from a collection of incubations.
    /// </summary>
    public static Incubation? FindSharedTsbIncubation(IEnumerable<Incubation> incubations)
    {
        return incubations.FirstOrDefault(i =>
            !string.IsNullOrEmpty(i.StepName) &&
            (i.StepName.Contains("TSB", StringComparison.OrdinalIgnoreCase) ||
             i.StepName.Contains("Enrichment", StringComparison.OrdinalIgnoreCase)));
    }
}
