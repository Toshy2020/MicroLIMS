using MicroLIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MicroLIMS.Application.Abstractions.Persistence;

// The database as the Application layer sees it. IMicroLimsDbContext
// (Persistence) implements it; Application depends only on this, so the
// provider, the model configuration and the audit-trail machinery stay in
// Persistence.
public interface IMicroLimsDbContext
{
    // Who is making the change, for the audit trail. Set per request.
    int? CurrentUserId { get; set; }

    // The Version of the record the client's form was loaded with, from the
    // request's If-Match header. Null when the client sent none. Checked by
    // RecordVersion.EnsureCurrent.
    uint? ExpectedVersion { get; set; }

    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<LoginHistory> LoginHistories { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SecurityAuditEvent> SecurityAuditEvents { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<PasswordHistory> PasswordHistories { get; }
    DbSet<AdminPasswordRecovery> AdminPasswordRecoveries { get; }
    DbSet<ElectronicSignature> ElectronicSignatures { get; }
    DbSet<Item> Items { get; }
    DbSet<Specification> Specifications { get; }
    DbSet<SpecificationStage> SpecificationStages { get; }
    DbSet<Media> Media { get; }
    DbSet<MediaEvaluation> MediaEvaluations { get; }
    DbSet<MediaEvaluationChallenge> MediaEvaluationChallenges { get; }
    DbSet<Cryovial> Cryovials { get; }
    DbSet<IdentityConfirmationEntry> IdentityConfirmationEntries { get; }
    DbSet<ThawEvent> ThawEvents { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<Sample> Samples { get; }
    DbSet<SampleSectionSignoff> SampleSectionSignoffs { get; }
    DbSet<SampleTest> SampleTests { get; }
    DbSet<TestOrder> TestOrders { get; }
    DbSet<Result> Results { get; }
    DbSet<Incubation> Incubations { get; }
    DbSet<CountTestReading> CountTestReadings { get; }
    DbSet<Department> Departments { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Machine> Machines { get; }
    DbSet<MachinePart> MachineParts { get; }
    DbSet<WaterSamplingPoint> WaterSamplingPoints { get; }
    DbSet<WaterDepartment> WaterDepartments { get; }
    DbSet<EMRoom> EMRooms { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AuditEventChange> AuditEventChanges { get; }
    DbSet<Report> Reports { get; }
    DbSet<WorkflowHistory> WorkflowHistories { get; }
    DbSet<ReviewWorkflowEvent> ReviewWorkflowEvents { get; }
    DbSet<TestReturnEvent> TestReturnEvents { get; }
    DbSet<ArchivedRecord> ArchivedRecords { get; }
    DbSet<MediaUsage> MediaUsages { get; }
    DbSet<RoomMonitoring> RoomMonitorings { get; }
    DbSet<SampleLocation> SampleLocations { get; }
    DbSet<MachinePartConfiguration> MachinePartConfigurations { get; }
    DbSet<SamplingConfiguration> SamplingConfigurations { get; }
    DbSet<PathogenObservation> PathogenObservations { get; }
    DbSet<ReportSnapshot> ReportSnapshots { get; }
    DbSet<ResultRecord> ResultRecords { get; }
    DbSet<DataExportLog> DataExportLogs { get; }
    DbSet<CauseOfTesting> CausesOfTesting { get; }
    DbSet<DiluentType> DiluentTypes { get; }
    DbSet<Neutralizer> Neutralizers { get; }
    DbSet<Sampler> Samplers { get; }
    DbSet<ProductionStage> ProductionStages { get; }
    DbSet<Equipment> Equipment { get; }
    DbSet<ChromatographyColumn> ChromatographyColumns { get; }
    DbSet<SystemSuitabilityRun> SystemSuitabilityRuns { get; }
    DbSet<SystemSuitabilityRunAnalyte> SystemSuitabilityRunAnalytes { get; }
    DbSet<SystemSuitabilityStandardResponse> SystemSuitabilityStandardResponses { get; }
    DbSet<TestAnalyte> TestAnalytes { get; }
    DbSet<TestDefinitionStageReplicate> TestDefinitionStageReplicates { get; }
    DbSet<CalibrationRun> CalibrationRuns { get; }
    DbSet<CalibrationRunDocument> CalibrationRunDocuments { get; }
    DbSet<CalibrationRunAnalyte> CalibrationRunAnalytes { get; }
    DbSet<CalibrationRunCheck> CalibrationRunChecks { get; }
    DbSet<TestAnalysis> TestAnalyses { get; }
    DbSet<ParameterResult> ParameterResults { get; }
    DbSet<ResultReading> ResultReadings { get; }
    DbSet<IncubatorSetPointHistory> IncubatorSetPointHistories { get; }
    DbSet<AutoclaveProgram> AutoclavePrograms { get; }
    DbSet<AutoclaveProgramHistory> AutoclaveProgramHistories { get; }
    DbSet<RoomTestConfiguration> RoomTestConfigurations { get; }
    DbSet<SamplePreparation> SamplePreparations { get; }
    DbSet<ItemPreparationConfiguration> ItemPreparationConfigurations { get; }
    DbSet<Organism> Organisms { get; }
    DbSet<WorkloadWeight> WorkloadWeights { get; }
    DbSet<WorkloadWeightHistory> WorkloadWeightHistories { get; }
    DbSet<MediaProduct> MediaProducts { get; }
    DbSet<MediaIncubationCondition> MediaIncubationConditions { get; }
    DbSet<MediaConfiguration> MediaConfigurations { get; }
    DbSet<MediaConfigurationChallenge> MediaConfigurationChallenges { get; }
    DbSet<Material> Materials { get; }
    DbSet<MaterialMasterEntry> MaterialMasterEntries { get; }
    DbSet<SolutionMaster> SolutionMasters { get; }
    DbSet<SolutionComponent> SolutionComponents { get; }
    DbSet<HplcMethod> HplcMethods { get; }
    DbSet<HplcMethodAnalyte> HplcMethodAnalytes { get; }
    DbSet<HplcMethodMobilePhase> HplcMethodMobilePhases { get; }
    DbSet<HplcMethodGradientStep> HplcMethodGradientSteps { get; }
    DbSet<SolutionPreparation> SolutionPreparations { get; }
    DbSet<SolutionPreparationComponent> SolutionPreparationComponents { get; }
    DbSet<SolutionPreparationStatusHistory> SolutionPreparationStatusHistories { get; }
    DbSet<TitrantStandardization> TitrantStandardizations { get; }
    DbSet<TitrantStandardizationReplicate> TitrantStandardizationReplicates { get; }
    DbSet<HplcRun> HplcRuns { get; }
    DbSet<HplcRunMobilePhase> HplcRunMobilePhases { get; }
    DbSet<HplcSstRecord> HplcSstRecords { get; }
    DbSet<HplcSstAnalyte> HplcSstAnalytes { get; }
    DbSet<HplcSstInjection> HplcSstInjections { get; }
    DbSet<HplcRunSample> HplcRunSamples { get; }
    DbSet<HplcSampleReplicate> HplcSampleReplicates { get; }
    DbSet<HplcReplicateResponse> HplcReplicateResponses { get; }
    DbSet<HplcEvidence> HplcEvidences { get; }
    DbSet<EquipmentInventory> EquipmentInventories { get; }
    DbSet<EquipmentStatusHistory> EquipmentStatusHistories { get; }
    DbSet<MaterialDocument> MaterialDocuments { get; }
    DbSet<MaterialDocumentAccessLog> MaterialDocumentAccessLogs { get; }
    DbSet<EquipmentDocument> EquipmentDocuments { get; }
    DbSet<EquipmentDocumentAccessLog> EquipmentDocumentAccessLogs { get; }
    DbSet<ItemDocument> ItemDocuments { get; }
    DbSet<ItemDocumentAccessLog> ItemDocumentAccessLogs { get; }
    DbSet<OosInvestigationDocument> OosInvestigationDocuments { get; }
    DbSet<TestDefinition> TestDefinitions { get; }
    DbSet<TestWorkflowStep> TestWorkflowSteps { get; }
    DbSet<TestWorkflowStepMedia> TestWorkflowStepMedias { get; }
    DbSet<TestWorkflowStepIncubationStage> TestWorkflowStepIncubationStages { get; }
    DbSet<TestWorkflowStepPhenotypicTest> TestWorkflowStepPhenotypicTests { get; }
    DbSet<WorkflowStepResult> WorkflowStepResults { get; }
    DbSet<ConfirmatoryMediaSelection> ConfirmatoryMediaSelections { get; }
    DbSet<ConfirmatoryPlateObservation> ConfirmatoryPlateObservations { get; }
    DbSet<LocationPathogenObservation> LocationPathogenObservations { get; }
    DbSet<DiscussionPost> DiscussionPosts { get; }
    DbSet<DiscussionPostVersion> DiscussionPostVersions { get; }
    DbSet<DiscussionComment> DiscussionComments { get; }
    DbSet<DiscussionAttachment> DiscussionAttachments { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<ConversationParticipant> ConversationParticipants { get; }
    DbSet<DirectMessage> DirectMessages { get; }
    DbSet<DocumentMaster> DocumentMasters { get; }
    DbSet<DocumentRevision> DocumentRevisions { get; }
    DbSet<DocumentMasterAssignment> DocumentMasterAssignments { get; }
    DbSet<DocumentKeyword> DocumentKeywords { get; }
    DbSet<RevisionFile> RevisionFiles { get; }
    DbSet<DocumentType> DocumentTypes { get; }
    DbSet<DocumentDepartment> DocumentDepartments { get; }
    DbSet<DocumentSection> DocumentSections { get; }
    DbSet<UserOrgMembership> UserOrgMemberships { get; }
    DbSet<DocumentNumberingConfiguration> DocumentNumberingConfigurations { get; }
    DbSet<ConfigurationSetting> ConfigurationSettings { get; }
    DbSet<DocumentReviewTask> DocumentReviewTasks { get; }
    DbSet<DocumentReviewFinding> DocumentReviewFindings { get; }
    DbSet<RevisionChangeItem> RevisionChangeItems { get; }
    DbSet<RevisionImpactAssessment> RevisionImpactAssessments { get; }
    DbSet<DocumentApprovalTask> DocumentApprovalTasks { get; }
    DbSet<PeriodicReviewTask> PeriodicReviewTasks { get; }
    DbSet<PeriodicReviewFinding> PeriodicReviewFindings { get; }
    DbSet<DocumentTrainingAssignment> DocumentTrainingAssignments { get; }
    DbSet<DocumentTrainingConfiguration> DocumentTrainingConfigurations { get; }
    DbSet<DocumentRoleCurriculum> DocumentRoleCurricula { get; }
    DbSet<DocumentRoleCurriculumItem> DocumentRoleCurriculumItems { get; }
    DbSet<DocumentAcknowledgementRecord> DocumentAcknowledgementRecords { get; }
    DbSet<DocumentEscalationRecord> DocumentEscalationRecords { get; }
    DbSet<Incident> Incidents { get; }
    DbSet<ErrorLog> ErrorLogs { get; }

    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry Entry(object entity);
    EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry<TEntity> Update<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry<TEntity> Attach<TEntity>(TEntity entity) where TEntity : class;
    // Both overloads, as on DbContext: with only the params one, passing a
    // List<T> would compile and try to track the list itself as an entity.
    void AddRange(params object[] entities);
    void AddRange(IEnumerable<object> entities);
    void RemoveRange(params object[] entities);
    void RemoveRange(IEnumerable<object> entities);
    int SaveChanges();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Saves a record whose generated identifier is guarded by the named
    // unique index (see UniqueIndexNames). False when another user took the
    // same identifier first, so the caller can pick a fresh one and retry.
    Task<bool> TrySaveChangesAsync(string uniqueIndexName);
}
