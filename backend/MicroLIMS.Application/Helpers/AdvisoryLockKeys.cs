namespace MicroLIMS.Application.Helpers;

// PostgreSQL advisory lock keys for number generation. Values are chosen once and
// never reused or changed. A lock must cover generating the number and inserting
// the row in one transaction.
public static class AdvisoryLockKeys
{
    public const long SampleNumber = 7_300_000_001L;
    public const long OosNumber = 7_300_000_002L;
    public const long QualityDeviation = 7_300_000_003L;
    public const long QualityCapa = 7_300_000_004L;
    public const long QualityChangeControl = 7_300_000_005L;
    public const long QualityComplaint = 7_300_000_006L;
    public const long StabilityProtocol = 7_300_000_007L;
    public const long Isolate = 7_300_000_008L;
    public const long RetainedSample = 7_300_000_009L;
    public const long DisposalRequest = 7_300_000_010L;
}
