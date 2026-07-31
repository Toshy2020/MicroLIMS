namespace MicroLIMS.Domain.Enums;

// Categories from the Microbiology Lab material stock list. Drives the
// default Unit suggested on the Materials Stock form (see
// MaterialService.DefaultUnitFor).
//
// LyophilizedMicroorganism is deliberately absent - reference strains
// are tracked as their own ReferenceStrain entity (identity confirmation,
// approval gate, DiscsRemaining), not as a generic Material row. See
// Material.cs for why.
public enum MaterialType
{
    DehydratedMedia,
    Supplement,
    AntibioticDisc,
    IdentificationKit,
    IdentificationReagent,
    Chemical,
    Indicator,
    ReferenceBuffer,
    DisposableTool,
    Other
}
