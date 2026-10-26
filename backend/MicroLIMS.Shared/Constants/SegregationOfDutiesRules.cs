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
        PermissionConstants.HplcOperate, PermissionConstants.SolutionsPrepare, PermissionConstants.WorkingStandardsQualify,
        PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease,
        PermissionConstants.CryovialsManage, PermissionConstants.CryovialsApprove,
        PermissionConstants.MonitoringPlanApprove, PermissionConstants.EquipmentPlanApprove, PermissionConstants.EquipmentWorkRecord,
        PermissionConstants.OosManage,
        PermissionConstants.DocumentsReview, PermissionConstants.DocumentsApprove
    };

    // Every (administration, GxP) pair the codes contain, in a stable order.
    public static List<(string Administration, string Gxp)> FindConflicts(IEnumerable<string> codes)
    {
        var held = codes.ToHashSet();
        var administration = AdministrationCodes.Where(held.Contains).OrderBy(c => c, StringComparer.Ordinal).ToList();
        var gxp = GxpExecutionAndApprovalCodes.Where(held.Contains).OrderBy(c => c, StringComparer.Ordinal).ToList();
        return administration.SelectMany(a => gxp.Select(g => (a, g))).ToList();
    }

    // "Users.Manage + Samples.Approve", the form shown to users.
    public static string Describe((string Administration, string Gxp) conflict) =>
        $"{conflict.Administration} + {conflict.Gxp}";
}
