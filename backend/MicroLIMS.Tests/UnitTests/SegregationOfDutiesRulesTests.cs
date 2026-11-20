using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SegregationOfDutiesRulesTests
{
    [Fact]
    public void FindConflicts_ReturnsEveryAdministrationAndGxpPair()
    {
        var conflicts = SegregationOfDutiesRules.FindConflicts(new[]
        {
            PermissionConstants.UsersManage, PermissionConstants.RolesManage,
            PermissionConstants.SamplesApprove, PermissionConstants.HplcOperate, PermissionConstants.AuditView
        });

        Assert.Equal(4, conflicts.Count);
        Assert.Contains((PermissionConstants.UsersManage, PermissionConstants.SamplesApprove), conflicts);
        Assert.Contains((PermissionConstants.RolesManage, PermissionConstants.HplcOperate), conflicts);
    }

    [Fact]
    public void FindConflicts_IsEmptyForAdministrationAlone_GxpAlone_AndNeutralCodes()
    {
        Assert.Empty(SegregationOfDutiesRules.FindConflicts(new[] { PermissionConstants.UsersManage, PermissionConstants.RolesManage, PermissionConstants.AuditView }));
        Assert.Empty(SegregationOfDutiesRules.FindConflicts(new[] { PermissionConstants.SamplesApprove, PermissionConstants.MediaRelease }));
        Assert.Empty(SegregationOfDutiesRules.FindConflicts(new[] { PermissionConstants.AuditView, PermissionConstants.DocumentsRegister }));
        Assert.Empty(SegregationOfDutiesRules.FindConflicts(Array.Empty<string>()));
    }

    [Fact]
    public void FindConflicts_IgnoresDuplicates_AndNamesThePairAdministrationFirst()
    {
        var conflicts = SegregationOfDutiesRules.FindConflicts(new[]
        {
            PermissionConstants.SamplesReview, PermissionConstants.UsersManage, PermissionConstants.SamplesReview
        });

        Assert.Single(conflicts);
        Assert.Equal("Users.Manage + Samples.Review", SegregationOfDutiesRules.Describe(conflicts[0]));
    }

    [Fact]
    public void TheTwoSets_AreDisjointAndOnlyHoldKnownCodes()
    {
        Assert.Empty(SegregationOfDutiesRules.AdministrationCodes.Intersect(SegregationOfDutiesRules.GxpExecutionAndApprovalCodes));
        Assert.All(SegregationOfDutiesRules.AdministrationCodes.Concat(SegregationOfDutiesRules.GxpExecutionAndApprovalCodes),
            code => Assert.Contains(code, PermissionConstants.All));
        Assert.Equal(2, SegregationOfDutiesRules.AdministrationCodes.Count);
        Assert.Equal(32, SegregationOfDutiesRules.GxpExecutionAndApprovalCodes.Count);
        Assert.Contains(PermissionConstants.OosContribute, SegregationOfDutiesRules.GxpExecutionAndApprovalCodes);
    }

    public static IEnumerable<object[]> QaForbiddenCodeRows() =>
        SegregationOfDutiesRules.QaForbiddenCodes
            .OrderBy(c => c, StringComparer.Ordinal)
            .Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(QaForbiddenCodeRows))]
    public void Every_forbidden_code_conflicts_for_a_qa_role(string forbiddenCode)
    {
        var conflicts = SegregationOfDutiesRules.FindQaConflicts(
            isQaRoleType: true,
            PermissionConstants.QaGrants.Append(forbiddenCode));

        Assert.Equal(new[] { forbiddenCode }, conflicts);
    }

    [Fact]
    public void QaForbiddenCodes_is_exactly_the_specified_set()
    {
        var eightNamedConfigurationCodes = new[]
        {
            "Sections.ConfigureAll",
            "System.ConfigureSettings",
            "MasterData.Manage",
            "Items.Manage",
            "Documents.ConfigManage",
            "Kpi.ConfigureProfiles",
            "Signatures.Manage",
            "System.ManageJobs"
        };

        var expected = SegregationOfDutiesRules.AdministrationCodes
            .Concat(SegregationOfDutiesRules.GxpExecutionAndApprovalCodes
                .Where(c => c != "Documents.Review" && c != "Documents.Approve"))
            .Concat(eightNamedConfigurationCodes)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(PermissionConstants.DocumentsReview, SegregationOfDutiesRules.QaForbiddenCodes);
        Assert.DoesNotContain(PermissionConstants.DocumentsApprove, SegregationOfDutiesRules.QaForbiddenCodes);
        Assert.Equal(
            expected.OrderBy(c => c, StringComparer.Ordinal),
            SegregationOfDutiesRules.QaForbiddenCodes.OrderBy(c => c, StringComparer.Ordinal));
    }

    [Fact]
    public void Documents_approve_and_review_are_allowed_for_qa()
    {
        Assert.DoesNotContain(PermissionConstants.DocumentsReview, SegregationOfDutiesRules.QaForbiddenCodes);
        Assert.DoesNotContain(PermissionConstants.DocumentsApprove, SegregationOfDutiesRules.QaForbiddenCodes);

        var conflicts = SegregationOfDutiesRules.FindQaConflicts(
            isQaRoleType: true,
            new[] { PermissionConstants.DocumentsReview, PermissionConstants.DocumentsApprove });

        Assert.Empty(conflicts);
    }

    [Fact]
    public void QaGrants_contains_no_forbidden_code()
    {
        Assert.Empty(SegregationOfDutiesRules.FindQaConflicts(isQaRoleType: true, PermissionConstants.QaGrants));
    }

    [Fact]
    public void Reviewer_with_audit_review_conflicts_on_samples_review()
    {
        var withAuditReview = SegregationOfDutiesRules.FindQaConflicts(
            isQaRoleType: false,
            new[] { PermissionConstants.SamplesReview, PermissionConstants.AuditReview });

        Assert.Equal(new[] { PermissionConstants.SamplesReview }, withAuditReview);

        var withAccessReviewSign = SegregationOfDutiesRules.FindQaConflicts(
            isQaRoleType: false,
            new[] { PermissionConstants.SamplesReview, PermissionConstants.AccessReviewSign });

        Assert.Equal(new[] { PermissionConstants.SamplesReview }, withAccessReviewSign);
    }

    [Fact]
    public void Non_qa_role_without_qa_codes_has_no_qa_conflict()
    {
        var conflicts = SegregationOfDutiesRules.FindQaConflicts(
            isQaRoleType: false,
            new[] { PermissionConstants.SamplesReview, PermissionConstants.UsersManage });

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Conflicts_are_ordinal_sorted_and_distinct()
    {
        var input = new[]
        {
            PermissionConstants.UsersManage,
            PermissionConstants.SamplesReview,
            PermissionConstants.UsersManage,
            PermissionConstants.AuditView,
            PermissionConstants.ItemsManage,
            PermissionConstants.SamplesReview,
            PermissionConstants.AccessReviewSign
        };

        var conflicts = SegregationOfDutiesRules.FindQaConflicts(isQaRoleType: false, input);

        Assert.Equal(
            new[] { PermissionConstants.ItemsManage, PermissionConstants.SamplesReview, PermissionConstants.UsersManage },
            conflicts);
    }

    [Fact]
    public void DescribeQaConflicts_names_every_code()
    {
        Assert.Equal(
            "A Quality Assurance role cannot hold: Samples.Review.",
            SegregationOfDutiesRules.DescribeQaConflicts(new[] { PermissionConstants.SamplesReview }));

        Assert.Equal(
            "A Quality Assurance role cannot hold: Items.Manage, Samples.Review, Users.Manage.",
            SegregationOfDutiesRules.DescribeQaConflicts(new[]
            {
                PermissionConstants.ItemsManage,
                PermissionConstants.SamplesReview,
                PermissionConstants.UsersManage
            }));
    }
}
