namespace MicroLIMS.Shared.Constants;

// Segregation of duties between who administers the system and who
// executes or approves GxP work (21 CFR Part 11 §11.10(d) and (g), EU GMP
// Annex 11 §12): administrators control the system, never GxP data. A role
// holding a code from each side could change its own access and then act
// on lab records with it, so no role may hold both. Enforced by
// RoleService when a role's permissions are set, and exposed to the Roles
// screen so it can show the conflict before the server refuses it.
//
// The GxP set lists the codes that execute or approve GxP work. View-only,
// configuration and master-data codes are deliberately not in it.
public static class SegregationOfDutiesRules
{
    public static readonly IReadOnlySet<string> AdministrationCodes = new HashSet<string>
    {
        PermissionConstants.UsersManage,
        PermissionConstants.RolesManage
    };

    public static readonly IReadOnlySet<string> GxpExecutionAndApprovalCodes = new HashSet<string>
    {
        PermissionConstants.SamplesReview, PermissionConstants.SamplesApprove, PermissionConstants.SamplesCorrect,
        PermissionConstants.SamplesReceive, PermissionConstants.SamplesReceiveOwnLab, PermissionConstants.SamplesAssignAnalyst,
        PermissionConstants.TestWorkflowExecute, PermissionConstants.TestWorkflowBiochemicalDecision, PermissionConstants.TestWorkflowSupervise,
        PermissionConstants.HplcOperate, PermissionConstants.SolutionsPrepare, PermissionConstants.SolutionsRelease, PermissionConstants.WorkingStandardsQualify,
        PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease, PermissionConstants.MaterialsRelease,
        PermissionConstants.CryovialsManage, PermissionConstants.CryovialsApprove,
        PermissionConstants.MonitoringPlanApprove, PermissionConstants.EquipmentPlanApprove, PermissionConstants.EquipmentWorkRecord,
        PermissionConstants.OosManage, PermissionConstants.OosContribute, PermissionConstants.CompetencyManage, PermissionConstants.CompetencyOverride,
        PermissionConstants.DocumentsReview, PermissionConstants.DocumentsApprove,
        PermissionConstants.SamplesPrepare, PermissionConstants.TestsOutsource, PermissionConstants.TestsSelfAssign,
        PermissionConstants.NotApplicableTestsManage, PermissionConstants.NotApplicableTestsFinalApprove
    };

    // Every (administration, GxP) pair the codes contain, in a stable order.
    public static List<(string Administration, string Gxp)> FindConflicts(IEnumerable<string> codes)
    {
        var held = codes.ToHashSet();
        var administration = AdministrationCodes.Where(held.Contains).OrderBy(c => c, StringComparer.Ordinal).ToList();
        var gxp = GxpExecutionAndApprovalCodes.Where(held.Contains).OrderBy(c => c, StringComparer.Ordinal).ToList();
        return administration.SelectMany(a => gxp.Select(g => (a, g))).ToList();
    }

    // Quality Assurance independence (B4): the quality unit reads everything and decides on
    // controlled documents, audit trail reviews and access reviews, and does nothing else.
    // A role of type QA, or any role holding a QA-only code, may hold no laboratory execution
    // or approval code (document review and approval excepted), no administration code and no
    // configuration code.
    public static readonly IReadOnlySet<string> QaOnlyCodes = new HashSet<string>
    {
        PermissionConstants.AuditReview,
        PermissionConstants.AccessReviewSign
    };

    public static readonly IReadOnlySet<string> QaForbiddenCodes = AdministrationCodes
        .Concat(GxpExecutionAndApprovalCodes.Where(c => c != PermissionConstants.DocumentsReview && c != PermissionConstants.DocumentsApprove))
        .Concat(new[]
        {
            PermissionConstants.SectionsConfigureAll, PermissionConstants.SystemConfigureSettings,
            PermissionConstants.MasterDataManage, PermissionConstants.ItemsManage,
            PermissionConstants.DocumentsConfigManage, PermissionConstants.KpiConfigureProfiles,
            PermissionConstants.SignaturesManage, PermissionConstants.SystemManageJobs
        })
        .ToHashSet();

    // The forbidden codes held, ordinal sorted, when the role is of type QA or holds a QA-only
    // code; otherwise empty. isQaRoleType is passed by the caller because RoleType lives in Domain.
    public static List<string> FindQaConflicts(bool isQaRoleType, IEnumerable<string> codes)
    {
        var held = codes.ToHashSet();
        if (!isQaRoleType && !held.Overlaps(QaOnlyCodes)) return [];
        return held.Where(QaForbiddenCodes.Contains).OrderBy(c => c, StringComparer.Ordinal).ToList();
    }

    public static string DescribeQaConflicts(IReadOnlyCollection<string> conflicts) =>
        $"A Quality Assurance role cannot hold: {string.Join(", ", conflicts)}.";

    // "Users.Manage + Samples.Approve", the form shown to users.
    public static string Describe((string Administration, string Gxp) conflict) =>
        $"{conflict.Administration} + {conflict.Gxp}";
}
