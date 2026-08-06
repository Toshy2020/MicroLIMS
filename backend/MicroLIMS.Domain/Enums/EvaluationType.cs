namespace MicroLIMS.Domain.Enums;

// Which Media Evaluation mechanic applies - derived from MediaType.Class
// at auto-assignment time (see MediaPreparationService.PrepareAsync).
public enum EvaluationType
{
    GrowthPromotion,
    IndicationInhibition,
    EnrichmentCharacteristics
}
