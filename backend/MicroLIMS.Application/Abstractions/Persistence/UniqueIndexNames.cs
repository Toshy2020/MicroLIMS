namespace MicroLIMS.Application.Abstractions.Persistence;

// Unique indexes that guard a generated identifier. The services that
// generate one retry on a clash (IMicroLimsDbContext.TrySaveChangesAsync);
// the Persistence configurations create the indexes under these names.
public static class UniqueIndexNames
{
    public const string CalibrationRunCode = "IX_CalibrationRuns_Code";
    public const string SystemSuitabilityRunCode = "IX_SystemSuitabilityRuns_Code";
    public const string MediaLotNumber = "IX_Media_LotNumber";
    public const string CryovialCode = "IX_Cryovials_Code";
}
