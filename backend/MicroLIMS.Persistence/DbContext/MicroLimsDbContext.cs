using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Domain.Attributes;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.Helpers;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace MicroLIMS.Persistence.DbContext;

public class MicroLimsDbContext : Microsoft.EntityFrameworkCore.DbContext, IMicroLimsDbContext
{
    // Set by JwtMiddleware from the authenticated token so SaveChanges can
    // stamp the audit trail with who made the change.
    public int? CurrentUserId { get; set; }

    // Set by AuditMiddleware for an instrument agent (AgentKey scheme), which
    // has no user id. Only used when CurrentUserId is null: a user always wins.
    public string? CurrentAgentCode { get; set; }

    // Set by a hosted worker that writes outside any request (e.g. the
    // calibration-overdue job): the audit trail records the System actor and
    // this process name instead of "user 0". Only used when there is no user
    // and no agent code.
    public string? CurrentSystemProcessName { get; set; }
    public uint? ExpectedVersion { get; set; }
    public string? CurrentReason { get; set; }

    public MicroLimsDbContext(DbContextOptions<MicroLimsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<AdminPasswordRecovery> AdminPasswordRecoveries => Set<AdminPasswordRecovery>();
    public DbSet<ElectronicSignature> ElectronicSignatures => Set<ElectronicSignature>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Specification> Specifications => Set<Specification>();
    public DbSet<SpecificationStage> SpecificationStages => Set<SpecificationStage>();
    public DbSet<Media> Media => Set<Media>();
    public DbSet<MediaEvaluation> MediaEvaluations => Set<MediaEvaluation>();
    public DbSet<MediaEvaluationChallenge> MediaEvaluationChallenges => Set<MediaEvaluationChallenge>();
    public DbSet<MediaStorageEntry> MediaStorageEntries => Set<MediaStorageEntry>();
    public DbSet<Cryovial> Cryovials => Set<Cryovial>();
    public DbSet<IdentityConfirmationEntry> IdentityConfirmationEntries => Set<IdentityConfirmationEntry>();
    public DbSet<ThawEvent> ThawEvents => Set<ThawEvent>();
    public DbSet<BiologicalWasteDestruction> BiologicalWasteDestructions => Set<BiologicalWasteDestruction>();
    public DbSet<BiologicalWasteDestructionItem> BiologicalWasteDestructionItems => Set<BiologicalWasteDestructionItem>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<SampleSectionSignoff> SampleSectionSignoffs => Set<SampleSectionSignoff>();
    public DbSet<SampleTest> SampleTests => Set<SampleTest>();
    public DbSet<TestOrder> TestOrders => Set<TestOrder>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Incubation> Incubations => Set<Incubation>();
    public DbSet<CountTestReading> CountTestReadings => Set<CountTestReading>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<LabCalendarDate> LabCalendarDates => Set<LabCalendarDate>();
    public DbSet<MonitoringSettings> MonitoringSettings => Set<MonitoringSettings>();
    public DbSet<MonitoringPlanRevision> MonitoringPlanRevisions => Set<MonitoringPlanRevision>();
    public DbSet<MonitoringOccurrence> MonitoringOccurrences => Set<MonitoringOccurrence>();
    public DbSet<MonitoringOccurrenceItem> MonitoringOccurrenceItems => Set<MonitoringOccurrenceItem>();
    public DbSet<MonitoringOccurrenceEvent> MonitoringOccurrenceEvents => Set<MonitoringOccurrenceEvent>();
    public DbSet<MonitoringDeviation> MonitoringDeviations => Set<MonitoringDeviation>();
    public DbSet<EquipmentPlanRevision> EquipmentPlanRevisions => Set<EquipmentPlanRevision>();
    public DbSet<EquipmentTask> EquipmentTasks => Set<EquipmentTask>();
    public DbSet<EquipmentTaskEvent> EquipmentTaskEvents => Set<EquipmentTaskEvent>();
    public DbSet<MaintenanceLogEntry> MaintenanceLogEntries => Set<MaintenanceLogEntry>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<CalendarEventDocument> CalendarEventDocuments => Set<CalendarEventDocument>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachinePart> MachineParts => Set<MachinePart>();
    public DbSet<WaterSamplingPoint> WaterSamplingPoints => Set<WaterSamplingPoint>();
    public DbSet<WaterDepartment> WaterDepartments => Set<WaterDepartment>();
    public DbSet<EMRoom> EMRooms => Set<EMRoom>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AuditEventChange> AuditEventChanges => Set<AuditEventChange>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<WorkflowHistory> WorkflowHistories => Set<WorkflowHistory>();
    public DbSet<ReviewWorkflowEvent> ReviewWorkflowEvents => Set<ReviewWorkflowEvent>();
    public DbSet<TestReturnEvent> TestReturnEvents => Set<TestReturnEvent>();
    public DbSet<ArchivedRecord> ArchivedRecords => Set<ArchivedRecord>();
    public DbSet<MediaUsage> MediaUsages => Set<MediaUsage>();
    public DbSet<RoomMonitoring> RoomMonitorings => Set<RoomMonitoring>();
    public DbSet<SampleLocation> SampleLocations => Set<SampleLocation>();
    public DbSet<MachinePartConfiguration> MachinePartConfigurations => Set<MachinePartConfiguration>();
    public DbSet<SamplingConfiguration> SamplingConfigurations => Set<SamplingConfiguration>();
    public DbSet<PathogenObservation> PathogenObservations => Set<PathogenObservation>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();
    public DbSet<ResultRecord> ResultRecords => Set<ResultRecord>();
    public DbSet<DataExportLog> DataExportLogs => Set<DataExportLog>();
    public DbSet<MasterDataImport> MasterDataImports => Set<MasterDataImport>();
    public DbSet<MasterDataImportRecord> MasterDataImportRecords => Set<MasterDataImportRecord>();

    // Phase 1 additions
    public DbSet<CauseOfTesting> CausesOfTesting => Set<CauseOfTesting>();
    public DbSet<DiluentType> DiluentTypes => Set<DiluentType>();
    public DbSet<Neutralizer> Neutralizers => Set<Neutralizer>();
    public DbSet<Sampler> Samplers => Set<Sampler>();
    public DbSet<ProductionStage> ProductionStages => Set<ProductionStage>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<ChromatographyColumn> ChromatographyColumns => Set<ChromatographyColumn>();
    public DbSet<TestDefinitionStageReplicate> TestDefinitionStageReplicates => Set<TestDefinitionStageReplicate>();
    public DbSet<TestAnalysis> TestAnalyses => Set<TestAnalysis>();
    public DbSet<ParameterResult> ParameterResults => Set<ParameterResult>();
    public DbSet<ResultReading> ResultReadings => Set<ResultReading>();
    public DbSet<IncubatorSetPointHistory> IncubatorSetPointHistories => Set<IncubatorSetPointHistory>();
    public DbSet<AutoclaveProgram> AutoclavePrograms => Set<AutoclaveProgram>();
    public DbSet<AutoclaveProgramHistory> AutoclaveProgramHistories => Set<AutoclaveProgramHistory>();
    public DbSet<RoomTestConfiguration> RoomTestConfigurations => Set<RoomTestConfiguration>();
    public DbSet<SamplePreparation> SamplePreparations => Set<SamplePreparation>();
    public DbSet<ItemPreparationConfiguration> ItemPreparationConfigurations => Set<ItemPreparationConfiguration>();
    public DbSet<Organism> Organisms => Set<Organism>();

    // Workload Weights
    public DbSet<WorkloadWeight> WorkloadWeights => Set<WorkloadWeight>();
    public DbSet<WorkloadWeightHistory> WorkloadWeightHistories => Set<WorkloadWeightHistory>();
    public DbSet<WorkflowProfileVersion> WorkflowProfileVersions => Set<WorkflowProfileVersion>();
    public DbSet<KpiSetting> KpiSettings => Set<KpiSetting>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<SystemSettingVersion> SystemSettingVersions => Set<SystemSettingVersion>();
    public DbSet<AnalystAvailability> AnalystAvailabilities => Set<AnalystAvailability>();

    // Media Configuration
    public DbSet<MediaProduct> MediaProducts => Set<MediaProduct>();
    public DbSet<MediaIncubationCondition> MediaIncubationConditions => Set<MediaIncubationCondition>();
    public DbSet<MediaConfiguration> MediaConfigurations => Set<MediaConfiguration>();
    public DbSet<MediaConfigurationChallenge> MediaConfigurationChallenges => Set<MediaConfigurationChallenge>();

    // Inventory module
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialMasterEntry> MaterialMasterEntries => Set<MaterialMasterEntry>();
    public DbSet<SolutionMaster> SolutionMasters => Set<SolutionMaster>();
    public DbSet<SolutionComponent> SolutionComponents => Set<SolutionComponent>();
    public DbSet<HplcMethod> HplcMethods => Set<HplcMethod>();
    public DbSet<HplcMethodAnalyte> HplcMethodAnalytes => Set<HplcMethodAnalyte>();
    public DbSet<IcpMethod> IcpMethods => Set<IcpMethod>();
    public DbSet<IcpMethodElement> IcpMethodElements => Set<IcpMethodElement>();
    public DbSet<HplcMethodMobilePhase> HplcMethodMobilePhases => Set<HplcMethodMobilePhase>();
    public DbSet<HplcMethodGradientStep> HplcMethodGradientSteps => Set<HplcMethodGradientStep>();
    public DbSet<HplcMethodOvenStep> HplcMethodOvenSteps => Set<HplcMethodOvenStep>();
    public DbSet<SolutionPreparation> SolutionPreparations => Set<SolutionPreparation>();
    public DbSet<SolutionPreparationComponent> SolutionPreparationComponents => Set<SolutionPreparationComponent>();
    public DbSet<SolutionPreparationStatusHistory> SolutionPreparationStatusHistories => Set<SolutionPreparationStatusHistory>();
    public DbSet<TitrantStandardization> TitrantStandardizations => Set<TitrantStandardization>();
    public DbSet<TitrantStandardizationReplicate> TitrantStandardizationReplicates => Set<TitrantStandardizationReplicate>();
    public DbSet<HplcRun> HplcRuns => Set<HplcRun>();
    public DbSet<HplcRunMobilePhase> HplcRunMobilePhases => Set<HplcRunMobilePhase>();
    public DbSet<HplcSstRecord> HplcSstRecords => Set<HplcSstRecord>();
    public DbSet<HplcSstAnalyte> HplcSstAnalytes => Set<HplcSstAnalyte>();
    public DbSet<HplcSstInjection> HplcSstInjections => Set<HplcSstInjection>();
    public DbSet<HplcRunSample> HplcRunSamples => Set<HplcRunSample>();
    public DbSet<WorkingStandardQualification> WorkingStandardQualifications => Set<WorkingStandardQualification>();
    public DbSet<WorkingStandardDocument> WorkingStandardDocuments => Set<WorkingStandardDocument>();
    public DbSet<InstrumentAgent> InstrumentAgents => Set<InstrumentAgent>();
    public DbSet<InstrumentIngest> InstrumentIngests => Set<InstrumentIngest>();
    public DbSet<InstrumentIngestVersion> InstrumentIngestVersions => Set<InstrumentIngestVersion>();
    public DbSet<InstrumentIngestPayload> InstrumentIngestPayloads => Set<InstrumentIngestPayload>();
    public DbSet<HplcSampleReplicate> HplcSampleReplicates => Set<HplcSampleReplicate>();
    public DbSet<HplcReplicateResponse> HplcReplicateResponses => Set<HplcReplicateResponse>();
    public DbSet<HplcEvidence> HplcEvidences => Set<HplcEvidence>();
    public DbSet<IcpRun> IcpRuns => Set<IcpRun>();
    public DbSet<IcpCalibration> IcpCalibrations => Set<IcpCalibration>();
    public DbSet<IcpCalibrationElement> IcpCalibrationElements => Set<IcpCalibrationElement>();
    public DbSet<IcpCcvReading> IcpCcvReadings => Set<IcpCcvReading>();
    public DbSet<IcpRunSample> IcpRunSamples => Set<IcpRunSample>();
    public DbSet<IcpSampleReplicate> IcpSampleReplicates => Set<IcpSampleReplicate>();
    public DbSet<IcpReplicateConcentration> IcpReplicateConcentrations => Set<IcpReplicateConcentration>();
    public DbSet<IcpEvidence> IcpEvidences => Set<IcpEvidence>();
    public DbSet<EquipmentInventory> EquipmentInventories => Set<EquipmentInventory>();
    public DbSet<EquipmentStatusHistory> EquipmentStatusHistories => Set<EquipmentStatusHistory>();
    public DbSet<MaterialDocument> MaterialDocuments => Set<MaterialDocument>();
    public DbSet<MaterialDocumentAccessLog> MaterialDocumentAccessLogs => Set<MaterialDocumentAccessLog>();
    public DbSet<EquipmentDocument> EquipmentDocuments => Set<EquipmentDocument>();
    public DbSet<EquipmentDocumentAccessLog> EquipmentDocumentAccessLogs => Set<EquipmentDocumentAccessLog>();
    public DbSet<ItemDocument> ItemDocuments => Set<ItemDocument>();
    public DbSet<ItemDocumentAccessLog> ItemDocumentAccessLogs => Set<ItemDocumentAccessLog>();
    public DbSet<OosInvestigationDocument> OosInvestigationDocuments => Set<OosInvestigationDocument>();

    // Test Master
    public DbSet<TestDefinition> TestDefinitions => Set<TestDefinition>();
    public DbSet<TestWorkflowStep> TestWorkflowSteps => Set<TestWorkflowStep>();
    public DbSet<TestWorkflowStepMedia> TestWorkflowStepMedias => Set<TestWorkflowStepMedia>();
    public DbSet<TestWorkflowStepIncubationStage> TestWorkflowStepIncubationStages => Set<TestWorkflowStepIncubationStage>();
    public DbSet<TestWorkflowStepPhenotypicTest> TestWorkflowStepPhenotypicTests => Set<TestWorkflowStepPhenotypicTest>();
    public DbSet<WorkflowStepResult> WorkflowStepResults => Set<WorkflowStepResult>();
    public DbSet<ConfirmatoryMediaSelection> ConfirmatoryMediaSelections => Set<ConfirmatoryMediaSelection>();
    public DbSet<ConfirmatoryPlateObservation> ConfirmatoryPlateObservations => Set<ConfirmatoryPlateObservation>();
    public DbSet<LocationPathogenObservation> LocationPathogenObservations => Set<LocationPathogenObservation>();

    // Discussions & Messages module (V1)
    public DbSet<DiscussionPost> DiscussionPosts => Set<DiscussionPost>();
    public DbSet<DiscussionPostVersion> DiscussionPostVersions => Set<DiscussionPostVersion>();
    public DbSet<DiscussionComment> DiscussionComments => Set<DiscussionComment>();
    public DbSet<DiscussionAttachment> DiscussionAttachments => Set<DiscussionAttachment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

    // Document Control Module (Release 1a)
    public DbSet<DocumentMaster> DocumentMasters => Set<DocumentMaster>();
    public DbSet<DocumentRevision> DocumentRevisions => Set<DocumentRevision>();
    public DbSet<DocumentMasterAssignment> DocumentMasterAssignments => Set<DocumentMasterAssignment>();
    public DbSet<DocumentKeyword> DocumentKeywords => Set<DocumentKeyword>();
    public DbSet<DocumentMasterSection> DocumentMasterSections => Set<DocumentMasterSection>();
    public DbSet<RevisionFile> RevisionFiles => Set<RevisionFile>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentDepartment> DocumentDepartments => Set<DocumentDepartment>();
    public DbSet<DocumentSection> DocumentSections => Set<DocumentSection>();
    public DbSet<UserOrgMembership> UserOrgMemberships => Set<UserOrgMembership>();
    public DbSet<DocumentNumberingConfiguration> DocumentNumberingConfigurations => Set<DocumentNumberingConfiguration>();
    public DbSet<ConfigurationSetting> ConfigurationSettings => Set<ConfigurationSetting>();

    // Document Control Module (Release 1b - WP1 Technical Review)
    public DbSet<DocumentReviewTask> DocumentReviewTasks => Set<DocumentReviewTask>();
    public DbSet<DocumentReviewFinding> DocumentReviewFindings => Set<DocumentReviewFinding>();

    // Document Control Module (Release 1b - WP2 Revision Management)
    public DbSet<RevisionChangeItem> RevisionChangeItems => Set<RevisionChangeItem>();
    public DbSet<RevisionImpactAssessment> RevisionImpactAssessments => Set<RevisionImpactAssessment>();

    // Document Control Module (Release 1b - WP3 Approval Workflow)
    public DbSet<DocumentApprovalTask> DocumentApprovalTasks => Set<DocumentApprovalTask>();

    // Document Control Module (Release 1b - WP6 Periodic Review Engine)
    public DbSet<PeriodicReviewTask> PeriodicReviewTasks => Set<PeriodicReviewTask>();
    public DbSet<PeriodicReviewFinding> PeriodicReviewFindings => Set<PeriodicReviewFinding>();

    // Document Control Module (Release 1c - WP1/WP3/WP4 Training Matrix, Acknowledgements & Escalations)
    public DbSet<DocumentTrainingAssignment> DocumentTrainingAssignments => Set<DocumentTrainingAssignment>();
    public DbSet<DocumentTrainingConfiguration> DocumentTrainingConfigurations => Set<DocumentTrainingConfiguration>();
    public DbSet<DocumentRoleCurriculum> DocumentRoleCurricula => Set<DocumentRoleCurriculum>();
    public DbSet<DocumentRoleCurriculumItem> DocumentRoleCurriculumItems => Set<DocumentRoleCurriculumItem>();
    public DbSet<DocumentAcknowledgementRecord> DocumentAcknowledgementRecords => Set<DocumentAcknowledgementRecord>();
    public DbSet<DocumentEscalationRecord> DocumentEscalationRecords => Set<DocumentEscalationRecord>();

    // Error Log & Monitoring - operational/technical data, not a GxP
    // record. Excluded from CaptureAuditEntries below and prunable.
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<BackgroundJobRun> BackgroundJobRuns => Set<BackgroundJobRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Individual per-table configuration classes live in
        // MicroLIMS.Persistence/Configurations and are applied here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MicroLimsDbContext).Assembly);

        // Optimistic concurrency on every table. PostgreSQL bumps the xmin
        // system column on each write, so an UPDATE or DELETE that still
        // carries the xmin it read fails when someone else wrote the row in
        // between - DbUpdateConcurrencyException, answered as 409 - instead
        // of silently overwriting their change. xmin is a system column:
        // nothing is added to the schema. PostgreSQL only; the InMemory
        // unit tests have no such column.
        if (Database.IsNpgsql())
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                         // Shared-type many-to-many join rows are only ever inserted
                         // or deleted, never updated, so they need no token.
                         .Where(t => t.BaseType is null && !t.IsOwned() && !t.HasSharedClrType && t.FindPrimaryKey() is not null && t.GetTableName() is not null)
                         .ToList())
            {
                // Records edited through a form expose it as Version, so the
                // client can send back the version its form was loaded with.
                var property = typeof(IVersionedEntity).IsAssignableFrom(entityType.ClrType)
                    ? modelBuilder.Entity(entityType.ClrType).Property<uint>(nameof(IVersionedEntity.Version))
                    : modelBuilder.Entity(entityType.ClrType).Property<uint>("xmin");
                property
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
            }
        }

        // Water sample locations hang off a WaterDepartment (mirrors EM's
        // Department -> Room). Optional FK; deletion is guarded in the
        // controller, so keep existing points intact rather than cascading.
        modelBuilder.Entity<WaterSamplingPoint>()
            .HasOne(p => p.WaterDepartment)
            .WithMany(d => d.SamplingPoints)
            .HasForeignKey(p => p.WaterDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Existing rows become active. ValueGeneratedNever so an explicit
        // IsActive = false is saved as false rather than replaced by the
        // column default.
        modelBuilder.Entity<Department>().Property(e => e.IsActive).HasDefaultValue(true).ValueGeneratedNever();

        // Existing roles start at revision 1; the application increments it.
        modelBuilder.Entity<Role>().Property(e => e.PermissionVersion).HasDefaultValue(1).ValueGeneratedNever();
        modelBuilder.Entity<Room>().Property(e => e.IsActive).HasDefaultValue(true).ValueGeneratedNever();

        // An LAF's parent Room. Restrict: a Room with LAFs cannot be removed
        // out from under them.
        modelBuilder.Entity<Room>()
            .HasOne(r => r.ParentRoom).WithMany()
            .HasForeignKey(r => r.ParentRoomId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LabCalendarDate>().HasIndex(d => d.Date).IsUnique();

        // Annual Monitoring Plan. Nothing here is deleted once approved, so
        // every foreign key restricts; only an occurrence's own items go
        // with it (a draft being rebuilt).
        modelBuilder.Entity<MonitoringPlanRevision>(e =>
        {
            e.HasIndex(r => new { r.Year, r.RevisionNumber }).IsUnique();
            // At most one approved revision per year. 1 = PlanRevisionStatus.Approved.
            e.HasIndex(r => r.Year).IsUnique().HasFilter("\"Status\" = 1").HasDatabaseName("IX_MonitoringPlanRevisions_Year_Approved");
            e.HasOne(r => r.ApprovalSignature).WithMany().HasForeignKey(r => r.ApprovalSignatureId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MonitoringOccurrence>(e =>
        {
            e.HasOne(o => o.CreatedByRevision).WithMany(r => r.CreatedOccurrences).HasForeignKey(o => o.CreatedByRevisionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.RetiredByRevision).WithMany().HasForeignKey(o => o.RetiredByRevisionId).OnDelete(DeleteBehavior.Restrict);
            e.Property(o => o.PeriodKey).HasMaxLength(40);
            e.Property(o => o.FrequencySnapshot).HasMaxLength(200);
            e.HasIndex(o => new { o.TargetType, o.WaterSamplingPointId, o.RoomId, o.MachineId, o.PeriodKey, o.Sequence })
                // Two of the three target ids are always null, so nulls must compare equal.
                .IsUnique().AreNullsDistinct(false).HasDatabaseName("IX_MonitoringOccurrences_Target_Period");
            e.HasIndex(o => o.WindowStart);
            e.HasIndex(o => o.WindowEnd);
            e.HasIndex(o => o.WaterSamplingPointId);
            e.HasIndex(o => o.RoomId);
            e.HasIndex(o => o.MachineId);
            e.HasIndex(o => o.CreatedByRevisionId);
            e.ToTable(t => t.HasCheckConstraint("CK_MonitoringOccurrences_OneTarget",
                "(CASE WHEN \"WaterSamplingPointId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"RoomId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"MachineId\" IS NULL THEN 0 ELSE 1 END) = 1"));
        });
        modelBuilder.Entity<MonitoringOccurrenceItem>(e =>
        {
            e.HasOne(i => i.MonitoringOccurrence).WithMany(o => o.Items).HasForeignKey(i => i.MonitoringOccurrenceId).OnDelete(DeleteBehavior.Cascade);
            e.Property(i => i.TestCode).HasMaxLength(100);
            e.HasIndex(i => i.MonitoringOccurrenceId);
        });
        modelBuilder.Entity<SampleLocation>(e =>
        {
            e.HasOne(l => l.MonitoringOccurrence).WithMany().HasForeignKey(l => l.MonitoringOccurrenceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => l.MonitoringOccurrenceId);
        });
        modelBuilder.Entity<MonitoringOccurrenceEvent>(e =>
        {
            e.HasOne(v => v.MonitoringOccurrence).WithMany().HasForeignKey(v => v.MonitoringOccurrenceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.ElectronicSignature).WithMany().HasForeignKey(v => v.ElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.SecondElectronicSignature).WithMany().HasForeignKey(v => v.SecondElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(v => v.SecondElectronicSignatureId);
            e.HasIndex(v => v.MonitoringOccurrenceId);
        });
        modelBuilder.Entity<MonitoringDeviation>(e =>
        {
            e.Property(d => d.Number).HasMaxLength(30);
            e.Property(d => d.Description).HasMaxLength(500);
            e.Property(d => d.ClosureReason).HasMaxLength(1000);
            e.HasOne(d => d.MonitoringOccurrence).WithMany().HasForeignKey(d => d.MonitoringOccurrenceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.FollowUpOccurrence).WithMany().HasForeignKey(d => d.FollowUpOccurrenceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.ClosureSignature).WithMany().HasForeignKey(d => d.ClosureSignatureId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.ClosureSecondSignature).WithMany().HasForeignKey(d => d.ClosureSecondSignatureId).OnDelete(DeleteBehavior.Restrict);
            // One deviation of each kind per occurrence, so a scan can run again and again.
            e.HasIndex(d => new { d.MonitoringOccurrenceId, d.Kind }).IsUnique();
            e.HasIndex(d => d.Status);
            e.HasIndex(d => d.FollowUpOccurrenceId);
            e.HasIndex(d => d.ClosureSignatureId);
            e.HasIndex(d => d.ClosureSecondSignatureId);
        });

        // Equipment Plan revisions, tasks, events, logbook, and calendar events.
        modelBuilder.Entity<EquipmentPlanRevision>(e =>
        {
            e.HasIndex(r => new { r.Year, r.RevisionNumber }).IsUnique();
            // At most one approved revision per year. 1 = PlanRevisionStatus.Approved.
            e.HasIndex(r => r.Year).IsUnique().HasFilter("\"Status\" = 1").HasDatabaseName("IX_EquipmentPlanRevisions_Year_Approved");
            e.HasOne(r => r.ApprovalSignature).WithMany().HasForeignKey(r => r.ApprovalSignatureId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EquipmentTask>(e =>
        {
            e.HasIndex(t => new { t.EquipmentInventoryId, t.TaskType, t.Year, t.Sequence }).IsUnique();
            e.HasOne(t => t.EquipmentInventory).WithMany().HasForeignKey(t => t.EquipmentInventoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.CreatedByRevision).WithMany(r => r.CreatedTasks).HasForeignKey(t => t.CreatedByRevisionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.RetiredByRevision).WithMany().HasForeignKey(t => t.RetiredByRevisionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.CalibrationDocument).WithMany().HasForeignKey(t => t.CalibrationDocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.MaintenanceLogEntry).WithMany().HasForeignKey(t => t.MaintenanceLogEntryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(t => t.IntervalSnapshot).HasMaxLength(200);
            e.HasIndex(t => t.EquipmentInventoryId);
            e.HasIndex(t => t.DueDate);
            e.HasIndex(t => t.CreatedByRevisionId);
            e.HasIndex(t => t.RetiredByRevisionId);
            e.HasIndex(t => t.CalibrationDocumentId);
            e.HasIndex(t => t.MaintenanceLogEntryId);
        });
        modelBuilder.Entity<EquipmentTaskEvent>(e =>
        {
            e.HasOne(v => v.EquipmentTask).WithMany(t => t.Events).HasForeignKey(v => v.EquipmentTaskId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.ElectronicSignature).WithMany().HasForeignKey(v => v.ElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);
            e.Property(v => v.Reason).HasMaxLength(1000);
            e.HasIndex(v => v.EquipmentTaskId);
            e.HasIndex(v => v.ElectronicSignatureId);
        });
        modelBuilder.Entity<MaintenanceLogEntry>(e =>
        {
            e.HasOne(l => l.EquipmentInventory).WithMany().HasForeignKey(l => l.EquipmentInventoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.ElectronicSignature).WithMany().HasForeignKey(l => l.ElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.CalendarEvent).WithMany().HasForeignKey(l => l.CalendarEventId).OnDelete(DeleteBehavior.Restrict);
            e.Property(l => l.WorkDone).IsRequired().HasMaxLength(2000);
            e.Property(l => l.PartsReplaced).HasMaxLength(1000);
            e.Property(l => l.Findings).HasMaxLength(2000);
            e.Property(l => l.VoidReason).HasMaxLength(1000);
            e.HasIndex(l => l.EquipmentInventoryId);
            e.HasIndex(l => l.PerformedOn);
            e.HasIndex(l => l.ElectronicSignatureId);
            e.HasIndex(l => l.CalendarEventId);
        });
        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.HasOne(c => c.EquipmentInventory).WithMany().HasForeignKey(c => c.EquipmentInventoryId).OnDelete(DeleteBehavior.Restrict);
            e.Property(c => c.Title).IsRequired().HasMaxLength(200);
            e.Property(c => c.Month).HasMaxLength(7);
            e.Property(c => c.Note).HasMaxLength(2000);
            e.Property(c => c.CancelReason).HasMaxLength(1000);
            e.HasIndex(c => c.Date);
            e.HasIndex(c => c.Month);
            e.HasIndex(c => c.EquipmentInventoryId);
            e.HasIndex(c => c.Status);
            e.ToTable(t => t.HasCheckConstraint("CK_CalendarEvents_DateOrMonth", "(\"Date\" IS NULL) <> (\"Month\" IS NULL)"));
        });
        modelBuilder.Entity<CalendarEventDocument>(e =>
        {
            e.Property(d => d.OriginalFileName).IsRequired().HasMaxLength(500);
            e.Property(d => d.StorageKey).IsRequired().HasMaxLength(500);
            e.Property(d => d.FileExtension).IsRequired().HasMaxLength(10);
            e.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
            e.Property(d => d.ContentSha256).IsRequired().HasMaxLength(64);
            e.Property(d => d.VoidReason).HasMaxLength(1000);

            e.HasOne(d => d.CalendarEvent)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.CalendarEventId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(d => d.UploadedByUser)
                .WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(d => d.CalendarEventId);
            e.HasIndex(d => new { d.CalendarEventId, d.Status });
        });
        modelBuilder.Entity<RoomTestConfiguration>().Property(e => e.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        modelBuilder.Entity<WaterDepartment>().Property(e => e.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        modelBuilder.Entity<WaterSamplingPoint>().Property(e => e.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        modelBuilder.Entity<Machine>().Property(e => e.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        modelBuilder.Entity<MachinePart>().Property(e => e.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        modelBuilder.Entity<MachinePartConfiguration>().Property(e => e.IsActive).HasDefaultValue(true).ValueGeneratedNever();
    }

    public Task<bool> TrySaveChangesAsync(params string[] uniqueIndexNames) =>
        UniqueIndexSave.TrySaveChangesAsync(this, uniqueIndexNames);

    // Frozen Principle #5 - Traceability. Captures every insert/update/
    // delete automatically so no service can forget to log a change.
    //
    // Two phases, because a new row has no real key until it is inserted:
    // EF gives it a temporary one (on PostgreSQL a large negative number),
    // and so does every foreign key pointing at it. Deletes and previous
    // values are read before the save, while the tracker still holds them;
    // the record ids and new values are read after it, once the database
    // has assigned the real keys. Both saves share one transaction, so a
    // change is never committed without its audit rows.
    public override int SaveChanges()
    {
        var pending = CapturePendingAuditEntries();
        if (pending.Count == 0)
            return base.SaveChanges();

        var ownsTransaction = Database.IsRelational() && Database.CurrentTransaction is null;
        using var tx = ownsTransaction ? Database.BeginTransaction() : null;
        var written = base.SaveChanges();
        AddAuditLogs(pending);
        written += base.SaveChanges();
        tx?.Commit();
        return written;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pending = CapturePendingAuditEntries();
        if (pending.Count == 0)
            return await base.SaveChangesAsync(cancellationToken);

        var ownsTransaction = Database.IsRelational() && Database.CurrentTransaction is null;
        await using var tx = ownsTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;
        var written = await base.SaveChangesAsync(cancellationToken);
        AddAuditLogs(pending);
        written += await base.SaveChangesAsync(cancellationToken);
        if (tx is not null)
            await tx.CommitAsync(cancellationToken);
        return written;
    }

    private sealed record PendingAuditEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry,
        string Action,
        string? PreviousValue,
        AuditLog? Completed);

    // Entity types the automatic capture skips. Public so the redaction
    // architecture test scans exactly the entities that DO reach AuditLogs.
    public static readonly IReadOnlySet<Type> AuditCaptureExcludedTypes = new HashSet<Type>
    {
        typeof(AuditLog),
        typeof(AuditEventChange), // append-only; excluded to prevent recursive audit
        typeof(MaterialDocumentAccessLog), // append-only; excluded to prevent recursive audit
        typeof(EquipmentDocumentAccessLog), // append-only; excluded to prevent recursive audit
        typeof(Incident), // operational error monitoring; not a GxP record
        typeof(ErrorLog), // operational error monitoring; not a GxP record
        typeof(BackgroundJobRun), // operational run log; a row per pass would be noise. Manual triggers are audited as security events
        typeof(RefreshToken), // session state; recorded in the Security Audit Trail instead
        typeof(NotificationLog), // derived reminders and read state; not a GxP record
        typeof(InstrumentIngestPayload), // append-only by DB trigger; hashes carried by the explicit ingest audit event
        typeof(SecurityAuditEvent), // security evidence, not a GxP record; also prevents recursive audit
        typeof(SystemSetting), // audited explicitly (Configuration category, with reason and signature) by SystemSettingsService
        typeof(SystemSettingVersion) // append-only by DB trigger; is itself the change history
    };

    private List<PendingAuditEntry> CapturePendingAuditEntries()
    {
        var entries = ChangeTracker.Entries()
            // IsInstanceOfType, not an exact-type lookup: a subclass of an
            // excluded type stays excluded, as the original `is not` chain did.
            .Where(e => !AuditCaptureExcludedTypes.Any(t => t.IsInstanceOfType(e.Entity)) &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList();

        var pending = new List<PendingAuditEntry>(entries.Count);
        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    pending.Add(new PendingAuditEntry(entry, "Create", null, null));
                    break;
                case EntityState.Modified:
                    pending.Add(new PendingAuditEntry(entry, "Update", SerializeValues(entry.OriginalValues), null));
                    break;
                case EntityState.Deleted:
                    // A deleted row already has its real key, and after the
                    // save the tracker lets go of it - finish it now.
                    var previousValue = SerializeValues(entry.OriginalValues);
                    pending.Add(new PendingAuditEntry(entry, "Delete", previousValue, BuildAuditLog(entry, "Delete", previousValue, null)));
                    break;
            }
        }
        return pending;
    }

    private void AddAuditLogs(List<PendingAuditEntry> pending)
    {
        foreach (var p in pending)
        {
            AuditLogs.Add(p.Completed ?? BuildAuditLog(p.Entry, p.Action, p.PreviousValue, SerializeValues(p.Entry.CurrentValues)));
        }
    }

    // A credential hash (marked [AuditRedacted]) must never reach the
    // immutable audit table: the change is still recorded, the value is not.
    private static readonly ConcurrentDictionary<PropertyInfo, bool> RedactedProperties = new();

    private static bool IsAuditRedacted(IProperty property) =>
        // Shadow properties have no CLR member, so nothing can mark them.
        property.PropertyInfo is { } member &&
        RedactedProperties.GetOrAdd(member, m => m.IsDefined(typeof(AuditRedactedAttribute), inherit: true));

    private static string SerializeValues(Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues values) =>
        JsonSerializer.Serialize(values.Properties.ToDictionary(
            p => p.Name,
            p => IsAuditRedacted(p) ? AuditRedactedAttribute.RedactedValue : values[p]));

    private AuditLog BuildAuditLog(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string action, string? previousValue, string? newValue)
    {
        var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");

        // An instrument agent has no user id: record it as the System actor
        // with the agent's code, and no user. If a user id is also present the
        // user wins and the agent code is ignored. A background worker (a
        // process name and neither of the above) is recorded the same way.
        var byAgent = CurrentUserId is null && !string.IsNullOrEmpty(CurrentAgentCode);
        var byProcess = CurrentUserId is null && !byAgent && !string.IsNullOrEmpty(CurrentSystemProcessName);
        var bySystem = byAgent || byProcess;

        return new AuditLog
        {
            EntityName = entry.Entity.GetType().Name,
            EntityId = idProperty?.CurrentValue?.ToString() ?? "unknown",
            Action = action,
            PreviousValue = previousValue,
            NewValue = newValue,
            UserId = bySystem ? null : CurrentUserId ?? 0,
            ActorType = bySystem ? Domain.Enums.ActorType.System : null,
            SystemProcessName = byAgent ? $"InstrumentAgent:{CurrentAgentCode}" : byProcess ? CurrentSystemProcessName : null,
            Timestamp = DateTime.UtcNow,
            Reason = CurrentReason,
            BatchNumber = GetPropertyAsString(entry, "BatchNumber"),
            ControlNumber = GetPropertyAsString(entry, "ControlNumber"),
            SampleReferenceNumber = GetPropertyAsString(entry, "ReferenceNumber"),
            MediaLotNumber = GetPropertyAsString(entry, "LotNumber"),
            ReferenceStrainCode = entry.Entity is Cryovial ? GetPropertyAsString(entry, "Code") : null,
            CryovialCode = entry.Entity is Cryovial ? GetPropertyAsString(entry, "Code") : null,
            SampleId = GetPropertyAsInt(entry, "SampleId") ?? (entry.Entity is Sample ? int.TryParse(idProperty?.CurrentValue?.ToString(), out var sid) ? sid : null : null),
            TestOrderId = GetPropertyAsInt(entry, "TestOrderId") ?? (entry.Entity is TestOrder ? int.TryParse(idProperty?.CurrentValue?.ToString(), out var tid) ? tid : null : null)
        };
    }

    // Reads a named property off the entity's current values, if it
    // exists, so the searchable AuditLog columns populate themselves
    // automatically without every service having to set them manually.
    private static string? GetPropertyAsString(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName)
    {
        var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);
        return prop?.CurrentValue?.ToString();
    }

    private static int? GetPropertyAsInt(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName)
    {
        var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == propertyName);
        return prop?.CurrentValue is int i ? i : null;
    }
}
