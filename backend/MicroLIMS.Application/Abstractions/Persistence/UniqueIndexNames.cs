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

    // Unlike the codes above, a clash here is not retried with the next
    // number - SolutionPreparationService.CompleteAsync reports the clash
    // and leaves the preparation InProgress for the analyst to sign again
    // (HPLC chain S4 plan's "keep it simple" simplification).
    public const string SolutionPreparationCode = "IX_SolutionPreparations_Code";
}
