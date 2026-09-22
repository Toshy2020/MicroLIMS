namespace MicroLIMS.Domain.Enums;

// Fixed, code-meaningful classification of a ProductionStage row (Bulk,
// In-Process, Finished, Stability, or anything else a lab has not yet
// reclassified). Code branches on Role - never on ProductionStage.Name,
// which stays a freely renameable Section-Head-managed label. Other = 0
// so a row that has never been classified (or a name the migration did
// not recognize) defaults to "not keyed to anything" rather than
// silently landing on a meaningful role.
public enum ProductionStageRole
{
    Other = 0,
    Bulk = 1,
    InProcess = 2,
    Finished = 3,
    Stability = 4
}
