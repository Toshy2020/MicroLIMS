namespace MicroLIMS.Shared.Constants;

// The 18 permission codes from rbac-permission-catalog.md, reproducing
// today's 112 [Authorize(Roles=...)] occurrences exactly. Referenced by
// DbSeeder (seed rows), PermissionPolicyProvider (which policy names
// resolve dynamically), and eventually by controllers migrating off
// role-string attributes.
public static class PermissionConstants
{
    public const string UsersManage = "Users.Manage";
    public const string RolesManage = "Roles.Manage";
    public const string AuditView = "Audit.View";
    public const string ReportingAdmin = "Reporting.Admin";
    public const string SamplesReview = "Samples.Review";
    public const string SamplesApprove = "Samples.Approve";
    public const string SignaturesManage = "Signatures.Manage";
    public const string TestWorkflowExecute = "TestWorkflow.Execute";
    public const string TestWorkflowBiochemicalDecision = "TestWorkflow.BiochemicalDecision";
    public const string CryovialsManage = "Cryovials.Manage";
    public const string CryovialsApprove = "Cryovials.Approve";
    public const string MaterialsManage = "Materials.Manage";
    public const string MaterialsDocumentControl = "Materials.DocumentControl";
    public const string EquipmentManage = "Equipment.Manage";
    public const string EquipmentDocumentControl = "Equipment.DocumentControl";
    public const string ItemsManage = "Items.Manage";
    public const string ItemsDocumentUpload = "Items.DocumentUpload";
    public const string MasterDataManage = "MasterData.Manage";

    public static readonly IReadOnlyList<string> All = new[]
    {
        UsersManage, RolesManage, AuditView, ReportingAdmin,
        SamplesReview, SamplesApprove, SignaturesManage,
        TestWorkflowExecute, TestWorkflowBiochemicalDecision,
        CryovialsManage, CryovialsApprove,
        MaterialsManage, MaterialsDocumentControl,
        EquipmentManage, EquipmentDocumentControl,
        ItemsManage, ItemsDocumentUpload,
        MasterDataManage
    };
}
