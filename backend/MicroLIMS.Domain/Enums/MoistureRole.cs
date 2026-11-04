namespace MicroLIMS.Domain.Enums;

// Which moisture measurement a test supplies, so a dried/anhydrous-basis assay
// can find it on the same sample.
public enum MoistureRole
{
    None = 0,
    LossOnDrying = 1,
    Water = 2
}
