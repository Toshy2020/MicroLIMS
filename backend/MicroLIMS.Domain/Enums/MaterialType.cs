namespace MicroLIMS.Domain.Enums;

// Categories from the Microbiology Lab material stock list. Drives the
// default Unit suggested on the Materials Stock form (see
// MaterialService.DefaultUnitFor).
//
// Lyophilized microorganism packs are received into Materials Stock
// like any other material, and cryovial batches are prepared directly
// from them - see CryovialService.PrepareCryovialsAsync.
public enum MaterialType
{
    DehydratedMedia,
    LyophilizedMicroorganism,
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
