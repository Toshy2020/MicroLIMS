using System.Linq.Expressions;
using MicroLIMS.Domain.Entities.Quality;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Quality;

// The one definition of "an affected specification or test definition that was never changed": the
// change-control closure refuses without a justification for such a link, and the record detail shows
// the same flag so the close form can ask for it up front.
public static class ChangeControlAffectedRules
{
    public static readonly Expression<Func<QualityRecordLink, bool>> UnchangedGatedTargetExpression = l =>
        l.IsAffectedByChange &&
        (l.TargetType == QualityLinkTargetType.Specification || l.TargetType == QualityLinkTargetType.TestDefinition) &&
        l.ChangedAtUtc == null;

    private static readonly Func<QualityRecordLink, bool> UnchangedGatedTargetCompiled = UnchangedGatedTargetExpression.Compile();

    public static bool IsUnchangedGatedTarget(QualityRecordLink link) => UnchangedGatedTargetCompiled(link);
}
