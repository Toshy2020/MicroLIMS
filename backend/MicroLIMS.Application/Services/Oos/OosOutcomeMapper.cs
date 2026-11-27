using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Oos;

public static class OosOutcomeMapper
{
    public static OosOutcomeClass Map(TestOrderOutcome outcome) => outcome.Status switch
    {
        ResultStatus.ActionLimitExceeded => OosOutcomeClass.ActionLimitExceeded,
        ResultStatus.AlertLimitExceeded => OosOutcomeClass.AlertLimitExceeded,
        _ => outcome.Fails ? OosOutcomeClass.Fails : OosOutcomeClass.Conforms
    };
}
