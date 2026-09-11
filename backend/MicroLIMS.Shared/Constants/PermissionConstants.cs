namespace MicroLIMS.Shared.Constants;

// The permission codes from rbac-permission-catalog.md, reproducing
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
    public const string DiscussionsView = "Discussions.View";
    public const string DiscussionsCreate = "Discussions.Create";
    public const string DiscussionsEditAny = "Discussions.EditAny";
    public const string MessagesUse = "Messages.Use";

    // Error Log & Monitoring admin page. Permission-gated from day
    // one - there is no legacy [Authorize(Roles=...)] equivalent.
    public const string SystemViewErrorLog = "System.ViewErrorLog";

    // Security Audit Trail (SecurityAuditEvent). Separate from AuditView,
    // which governs the GxP audit trail - the two are different domains
    // and are deliberately not granted together.
    public const string SystemViewSecurityAudit = "System.ViewSecurityAudit";

    // Document Control module. URS v1.1 §5 splits authority in two: "Global role
    // permissions determine which categories of function a user may access" and
    // "per-document assignments determine on which documents a user may exercise
    // review or approval authority", with both required. These codes are that
    // first half - category access only.
    //
    // Deliberately NOT permissions, and left as fixed rules in
    // DocumentAuthorizationService:
    //   * Voiding a Document Master. FS-1a-111 reserves it for the Document
    //     Controller "not even SystemAdministrator", and System Administrator is
    //     seeded with All - so a Documents.Void code would hand admins exactly
    //     the capability the specification denies them.
    //   * The segregation-of-duties invariants (author != reviewer != approver).
    //     BR-013 states the restriction "cannot be overridden administratively",
    //     so it must not be expressible as a grant.
    //   * Overriding a revision number. FRS-1B §3.2:184 reserves it for the
    //     Document Controller and the FRS-1A matrix records System Administrator
    //     as No. Unlike Review or Approve it is sufficient on its own - there is
    //     no second per-document gate behind it - so as a permission inside All
    //     it would hand admins the capability outright.
    //
    // The codes below are all necessary-but-not-sufficient or legitimately the
    // administrator's: Review, Approve and PeriodicReview still require the
    // per-document assignment and pass the SoD checks, per the URS's
    // both-conditions rule.
    public const string DocumentsRegister = "Documents.Register";
    public const string DocumentsDraftEdit = "Documents.DraftEdit";
    public const string DocumentsRevisionCreate = "Documents.RevisionCreate";
    public const string DocumentsReview = "Documents.Review";
    public const string DocumentsApprove = "Documents.Approve";
    public const string DocumentsPeriodicReview = "Documents.PeriodicReview";
    public const string DocumentsTrainingAssign = "Documents.TrainingAssign";
    public const string DocumentsTrainingViewMatrix = "Documents.TrainingViewMatrix";
    public const string DocumentsConfigManage = "Documents.ConfigManage";

    public static readonly IReadOnlyList<string> All = new[]
    {
        UsersManage, RolesManage, AuditView, ReportingAdmin,
        SamplesReview, SamplesApprove, SignaturesManage,
        TestWorkflowExecute, TestWorkflowBiochemicalDecision,
        CryovialsManage, CryovialsApprove,
        MaterialsManage, MaterialsDocumentControl,
        EquipmentManage, EquipmentDocumentControl,
        ItemsManage, ItemsDocumentUpload,
        MasterDataManage,
        DiscussionsView, DiscussionsCreate, DiscussionsEditAny, MessagesUse,
        SystemViewErrorLog, SystemViewSecurityAudit,
        DocumentsRegister, DocumentsDraftEdit, DocumentsRevisionCreate,
        DocumentsReview, DocumentsApprove, DocumentsPeriodicReview,
        DocumentsTrainingAssign, DocumentsTrainingViewMatrix, DocumentsConfigManage
    };
}
