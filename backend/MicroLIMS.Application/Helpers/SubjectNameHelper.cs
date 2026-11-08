using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Helpers;

public static class SubjectNameHelper
{
    public static string? FormatSingleLocationSubject(IReadOnlyList<SampleLocation>? locations)
    {
        if (locations == null || locations.Count != 1) return null;
        var loc = locations[0];
        var part = loc.MachinePartConfiguration?.MachinePart?.Name;
        if (string.IsNullOrWhiteSpace(part)) return null;
        var machine = loc.MachinePartConfiguration?.MachinePart?.Machine?.Name ?? loc.Sample?.Machine?.Name;
        return string.IsNullOrWhiteSpace(machine) ? part : $"{machine} \u2013 {part}";
    }
}
