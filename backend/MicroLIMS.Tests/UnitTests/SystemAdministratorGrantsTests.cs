using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Segregation of duties (21 CFR Part 11 §11.10(d)(g), EU GMP Annex 11 §12): the built-in
// System Administrator controls the system, never GxP data. These guard the grant list itself;
// SystemAdministratorGrantsPostgresTests guards what the migrations leave in a database.
public class SystemAdministratorGrantsTests
{
    // Every code that executes, reviews, approves or signs GxP work. None may reach the administrator.
    public static readonly string[] GxpExecutionCodes =
    {
        PermissionConstants.SamplesReview, PermissionConstants.SamplesApprove, PermissionConstants.SamplesReceive,
        PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.SamplesCorrect, PermissionConstants.SamplesAssignAnalyst,
        PermissionConstants.TestWorkflowExecute, PermissionConstants.TestWorkflowBiochemicalDecision, PermissionConstants.TestWorkflowSupervise,
        PermissionConstants.HplcOperate, PermissionConstants.SolutionsPrepare, PermissionConstants.WorkingStandardsQualify,
        PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease,
        PermissionConstants.CryovialsManage, PermissionConstants.CryovialsApprove,
        PermissionConstants.OosManage,
        PermissionConstants.MaterialsManage, PermissionConstants.MaterialsDocumentControl,
        PermissionConstants.EquipmentManage, PermissionConstants.EquipmentDocumentControl,
        PermissionConstants.ItemsDocumentUpload, PermissionConstants.DiscussionsEditAny,
        PermissionConstants.DocumentsRegister, PermissionConstants.DocumentsDraftEdit, PermissionConstants.DocumentsRevisionCreate,
        PermissionConstants.DocumentsReview, PermissionConstants.DocumentsApprove, PermissionConstants.DocumentsPeriodicReview,
        PermissionConstants.DocumentsTrainingAssign,
        PermissionConstants.MonitoringPlanManage, PermissionConstants.MonitoringPlanApprove,
        PermissionConstants.EquipmentPlanManage, PermissionConstants.EquipmentPlanApprove,
        PermissionConstants.EquipmentWorkRecord, PermissionConstants.CalendarEventManage,
    };

    [Fact]
    public void SystemAdministratorGrants_AreKnownCodes_WithoutDuplicates()
    {
        Assert.Empty(PermissionConstants.SystemAdministratorGrants.Except(PermissionConstants.All));
        Assert.Equal(PermissionConstants.SystemAdministratorGrants.Count, PermissionConstants.SystemAdministratorGrants.Distinct().Count());
    }

    [Fact]
    public void SystemAdministratorGrants_ContainNoGxpExecutionCode()
    {
        var leaked = PermissionConstants.SystemAdministratorGrants.Intersect(GxpExecutionCodes).ToList();
        Assert.True(leaked.Count == 0, $"System Administrator must not hold GxP execution/approval codes: {string.Join(", ", leaked)}");
    }

    [Fact]
    public void GxpExecutionList_ContainsOnlyKnownCodes()
    {
        Assert.Empty(GxpExecutionCodes.Except(PermissionConstants.All));
    }

    [Fact]
    public void SectionsViewAll_IsEnforcedAndGrantedToTheAdministrator()
    {
        Assert.Contains(PermissionConstants.SectionsViewAll, PermissionConstants.All);
        Assert.Contains(PermissionConstants.SectionsViewAll, PermissionConstants.Enforced);
        Assert.Contains(PermissionConstants.SectionsViewAll, PermissionConstants.SystemAdministratorGrants);
    }
}
