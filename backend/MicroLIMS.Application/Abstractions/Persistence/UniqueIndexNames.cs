namespace MicroLIMS.Application.Abstractions.Persistence;

// Unique indexes that guard a generated identifier. The services that
// generate one retry on a clash (IMicroLimsDbContext.TrySaveChangesAsync);
// the Persistence configurations create the indexes under these names.
public static class UniqueIndexNames
{
    public const string MediaLotNumber = "IX_Media_LotNumber";
    public const string CryovialCode = "IX_Cryovials_Code";
    public const string SampleReferenceNumber = "IX_Samples_ReferenceNumber";

    // Unlike the codes above, a clash here is not retried with the next
    // number - SolutionPreparationService.CompleteAsync reports the clash
    // and leaves the preparation InProgress for the analyst to sign again
    // (HPLC chain S4 plan's "keep it simple" simplification).
    public const string SolutionPreparationCode = "IX_SolutionPreparations_Code";

    // Like SolutionPreparationCode: a clash is reported, not retried
    // (WorkingStandardService asks the user to sign again).
    public const string WorkingStandardCode = "IX_Materials_WorkingStandardCode";
    public const string WorkingStandardQualificationCode = "IX_WorkingStandardQualifications_Code";

    // Instrument ingest (InstrumentIngestService.ReceiveAsync): two receipts of
    // the same injection or the same report race on these; the loser re-reads
    // and returns the winner's row. These are EF's default names (the first is
    // cut to PostgreSQL's 63 characters); a test pins them to the model.
    public const string InstrumentIngestIdentity = "IX_InstrumentIngests_EquipmentId_SequenceName_InjectionNumber_~";
    public const string InstrumentIngestVersionHash = "IX_InstrumentIngestVersions_InstrumentIngestId_ReportSha256";
    public const string InstrumentIngestVersionNo = "IX_InstrumentIngestVersions_InstrumentIngestId_VersionNo";
}
