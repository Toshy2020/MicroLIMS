namespace MicroLIMS.Domain.Enums;

// Replaces the old GrowthObserved boolean. "Conforming" means growth
// matching the expected appearance for the target organism on that
// medium; growth that does not match is not the organism being sought.
public enum GrowthObservation
{
    NoGrowth,
    GrowthNonConforming,
    GrowthConforming
}
