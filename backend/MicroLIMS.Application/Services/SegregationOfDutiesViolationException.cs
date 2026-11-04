using MicroLIMS.Shared.Constants;

namespace MicroLIMS.Application.Services;

// A role's permissions would mix system administration with GxP execution
// or approval (see SegregationOfDutiesRules). The API reports it as 409.
public class SegregationOfDutiesViolationException : InvalidOperationException
{
    public SegregationOfDutiesViolationException(IReadOnlyList<(string Administration, string Gxp)> conflicts)
        : base($"A role cannot hold both system administration and GxP execution or approval permissions: {SegregationOfDutiesRules.Describe(conflicts[0])}"
               + (conflicts.Count > 1 ? $" (and {conflicts.Count - 1} more)." : "."))
    {
        Conflicts = conflicts;
    }

    public IReadOnlyList<(string Administration, string Gxp)> Conflicts { get; }
}
