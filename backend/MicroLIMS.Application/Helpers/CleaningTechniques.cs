using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Helpers;

public static class CleaningTechniques
{
    public const string Uv = "UV";
    public const string Hplc = "HPLC";
    public const string Icp = "ICP";

    public static readonly string[] Allowed = [Uv, Hplc, Icp];

    public static string Of(WorkflowType workflowType) =>
        workflowType switch
        {
            WorkflowType.HplcMethodAssay => Hplc,
            WorkflowType.IcpMethodAssay => Icp,
            _ => Uv
        };

    public static string Normalize(IEnumerable<string>? techniques)
    {
        if (techniques == null)
            throw new BusinessRuleException("At least one cleaning technique is required.");

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in techniques)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var trimmed = raw.Trim();
            var match = Allowed.FirstOrDefault(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException($"Invalid cleaning technique '{trimmed}'. Allowed techniques are UV, HPLC, ICP.");
            set.Add(match);
        }

        if (set.Count == 0)
            throw new BusinessRuleException("At least one cleaning technique is required.");

        return string.Join(",", Allowed.Where(set.Contains));
    }
}
