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
        Assert.Equal(22, SegregationOfDutiesRules.GxpExecutionAndApprovalCodes.Count);
    }
}
