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
    public const string SamplesReceive = "Samples.Receive";
    public const string SamplesTrackAll = "Samples.TrackAll";
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
    //     Controller "not even SystemAdministrator", so a Documents.Void code
    //     could hand admins exactly the capability the specification denies them.
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

    // Gates that were [Authorize(Roles=...)] only, with no catalogue code
    // of their own. Each is granted by default to exactly the roles the
    // attribute named, so moving to them changed nobody's access.
    public const string DashboardsLabOverview = "Dashboards.LabOverview";
    public const string DashboardsReview = "Dashboards.Review";
    public const string KpiView = "Kpi.View";

    // KPI remodel S2: see other analysts' figures. Without it the server returns a viewer's own analyst figures only (D8).
    public const string KpiViewTeam = "Kpi.ViewTeam";

    // KPI remodel S1 - create and sign workflow profile versions (work units, analyst-stage targets).
    public const string KpiConfigureProfiles = "Kpi.ConfigureProfiles";
    public const string MediaPrepare = "Media.Prepare";
    public const string MediaRelease = "Media.Release";
    public const string OosManage = "Oos.Manage";
    public const string TestWorkflowSupervise = "TestWorkflow.Supervise";
    public const string SamplesReceiveOwnLab = "Samples.ReceiveOwnLab";
    public const string SamplesCorrect = "Samples.Correct";
    public const string SamplesAssignAnalyst = "Samples.AssignAnalyst";

    // HPLC chain S4 - Solution Preparation area (spec 4). Gates every write
    // on a SolutionPreparation (start, save, complete, cancel, discard).
    public const string SolutionsPrepare = "Solutions.Prepare";

    // HPLC chain S6 - HPLC Workspace (spec 5). Gates every write on an
    // HplcRun (start, save/confirm SST, assign/remove samples, replicate
    // entry, submission, evidence, abandon/complete).
    public const string HplcOperate = "Hplc.Operate";
    public const string WorkingStandardsQualify = "WorkingStandards.Qualify";

    // Annual Monitoring Plan: view the plan and calendar; generate revisions,
    // keep the lab calendar and settings, request skips; sign a revision.
    public const string MonitoringPlanView = "MonitoringPlan.View";
    public const string MonitoringPlanManage = "MonitoringPlan.Manage";
    public const string MonitoringPlanApprove = "MonitoringPlan.Approve";

    // Equipment Plan: manage schedules and revisions; sign a revision; record calibration and logbook; manage calendar events.
    public const string EquipmentPlanManage = "EquipmentPlan.Manage";
    public const string EquipmentPlanApprove = "EquipmentPlan.Approve";
    public const string EquipmentWorkRecord = "EquipmentWork.Record";
    public const string CalendarEventManage = "CalendarEvent.Manage";

    // Read every laboratory section's records without being a member. Read-only: writes always follow section membership, for administrators too (UserSectionScopeService).
    public const string SectionsViewAll = "Sections.ViewAll";

    // Configure master data and settings in every section, not only the user's own. Configuration only: GxP records still follow membership.
    public const string SectionsConfigureAll = "Sections.ConfigureAll";

    public static readonly IReadOnlyList<string> All = new[]
    {
        UsersManage, RolesManage, AuditView, ReportingAdmin,
        SamplesReview, SamplesApprove, SamplesReceive, SamplesTrackAll, SignaturesManage,
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
        DocumentsTrainingAssign, DocumentsTrainingViewMatrix, DocumentsConfigManage,
        DashboardsLabOverview, DashboardsReview, KpiView, KpiViewTeam, KpiConfigureProfiles,
        MediaPrepare, MediaRelease, OosManage, TestWorkflowSupervise,
        SamplesReceiveOwnLab, SamplesCorrect, SamplesAssignAnalyst,
        SolutionsPrepare, HplcOperate, WorkingStandardsQualify,
        MonitoringPlanView, MonitoringPlanManage, MonitoringPlanApprove,
        EquipmentPlanManage, EquipmentPlanApprove, EquipmentWorkRecord, CalendarEventManage,
        SectionsViewAll, SectionsConfigureAll
    };

    // What the built-in System Administrator role holds. Segregation of duties
    // (21 CFR Part 11 §11.10(d)(g), EU GMP Annex 11 §12): administrators control
    // the system - users, roles, configuration, master data - and may read the
    // GxP record, but never execute, review or approve GxP data. Codes that
    // perform or sign laboratory work (sample review/approval, test workflow,
    // media, cryovials, OOS, document review/approval, plan approval and so on)
    // must never be added here.
    public static readonly IReadOnlyList<string> SystemAdministratorGrants = new[]
    {
        UsersManage, RolesManage, AuditView, ReportingAdmin,
        SystemViewErrorLog, SystemViewSecurityAudit,
        MasterDataManage, ItemsManage, DocumentsConfigManage, KpiConfigureProfiles,
        DiscussionsView, DiscussionsCreate, MessagesUse,
        SignaturesManage,   // read-only signature trail
        DashboardsLabOverview, DashboardsReview, KpiView, KpiViewTeam,
        SamplesTrackAll, MonitoringPlanView, DocumentsTrainingViewMatrix,
        SectionsViewAll, SectionsConfigureAll
    };

    // The codes some endpoint or service actually checks. The rest are
    // declared for the Roles screen but not yet enforced anywhere; the
    // screen shows which is which (Permission.IsEnforced).
    public static readonly IReadOnlySet<string> Enforced = new HashSet<string>
    {
        AuditView, CryovialsApprove, CryovialsManage, DashboardsLabOverview, DashboardsReview,
        DiscussionsEditAny, DocumentsConfigManage, DocumentsRegister, DocumentsTrainingAssign, DocumentsTrainingViewMatrix,
        EquipmentDocumentControl, EquipmentManage, ItemsDocumentUpload, ItemsManage, KpiView, KpiViewTeam, KpiConfigureProfiles,
        MasterDataManage, MaterialsDocumentControl, MaterialsManage, MediaPrepare, MediaRelease, OosManage,
        ReportingAdmin, RolesManage, SamplesApprove, SamplesAssignAnalyst, SamplesCorrect, SamplesReceive,
        SamplesReceiveOwnLab, SamplesReview, SamplesTrackAll, SignaturesManage, SystemViewErrorLog, SystemViewSecurityAudit,
        TestWorkflowBiochemicalDecision, TestWorkflowExecute, TestWorkflowSupervise, UsersManage,
        SolutionsPrepare, HplcOperate, WorkingStandardsQualify,
        MonitoringPlanView, MonitoringPlanManage, MonitoringPlanApprove,
        EquipmentPlanManage, EquipmentPlanApprove, EquipmentWorkRecord, CalendarEventManage,
        SectionsViewAll, SectionsConfigureAll
    };
}
