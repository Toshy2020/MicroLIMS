using MicroLIMS.Shared.Exceptions;
namespace MicroLIMS.Application.Helpers;

// ICP assay. Pure - no I/O, no rounding (display rounding is the evaluator's job).
//   solid : assay % = result x Th.Wt x 100 / (target x Act.Wt)
//   liquid: assay % = result x Th.Vol x Sp.gr x 100 / (target x Act.Wt)
// result and target are in ppm; weights in g, volume in mL.
public static class IcpContentCalculator
{
    public static decimal AssayPercent(decimal resultPpm, decimal targetPpm, decimal theoreticalAmount,
        decimal? specificGravity, decimal actualWeightG)
    {
        if (targetPpm <= 0m) throw new BusinessRuleException("The target concentration must be greater than zero.");
        if (theoreticalAmount <= 0m) throw new BusinessRuleException("The theoretical weight or volume must be greater than zero.");
        if (actualWeightG <= 0m) throw new BusinessRuleException("The actual weight must be greater than zero.");
        if (specificGravity is <= 0m) throw new BusinessRuleException("The specific gravity must be greater than zero.");

        return resultPpm * theoreticalAmount * (specificGravity ?? 1m) * 100m / (targetPpm * actualWeightG);
    }
}
