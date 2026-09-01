using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using System.Text.Json;

namespace MicroLIMS.Persistence.DbContext;

public class MicroLimsDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    // Set by JwtMiddleware from the authenticated token so SaveChanges can
    // stamp the audit trail with who made the change.
    public int? CurrentUserId { get; set; }

    public MicroLimsDbContext(DbContextOptions<MicroLimsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<LoginHistory> LoginHistories => Set<LoginHistory>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<AdminPasswordRecovery> AdminPasswordRecoveries => Set<AdminPasswordRecovery>();
    public DbSet<ElectronicSignature> ElectronicSignatures => Set<ElectronicSignature>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Specification> Specifications => Set<Specification>();
    public DbSet<Media> Media => Set<Media>();
    public DbSet<MediaEvaluation> MediaEvaluations => Set<MediaEvaluation>();
    public DbSet<MediaEvaluationChallenge> MediaEvaluationChallenges => Set<MediaEvaluationChallenge>();
    public DbSet<Cryovial> Cryovials => Set<Cryovial>();
    public DbSet<IdentityConfirmationEntry> IdentityConfirmationEntries => Set<IdentityConfirmationEntry>();
    public DbSet<ThawEvent> ThawEvents => Set<ThawEvent>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<SampleTest> SampleTests => Set<SampleTest>();
    public DbSet<TestOrder> TestOrders => Set<TestOrder>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Incubation> Incubations => Set<Incubation>();
    public DbSet<CountTestReading> CountTestReadings => Set<CountTestReading>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachinePart> MachineParts => Set<MachinePart>();
    public DbSet<WaterSamplingPoint> WaterSamplingPoints => Set<WaterSamplingPoint>();
    public DbSet<WaterDepartment> WaterDepartments => Set<WaterDepartment>();
    public DbSet<EMRoom> EMRooms => Set<EMRoom>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
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

    // Phase 1 additions
    public DbSet<CauseOfTesting> CausesOfTesting => Set<CauseOfTesting>();
    public DbSet<DiluentType> DiluentTypes => Set<DiluentType>();
    public DbSet<Neutralizer> Neutralizers => Set<Neutralizer>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
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

    // Media Configuration
    public DbSet<MediaConfiguration> MediaConfigurations => Set<MediaConfiguration>();
    public DbSet<MediaConfigurationChallenge> MediaConfigurationChallenges => Set<MediaConfigurationChallenge>();

    // Inventory module
    public DbSet<Material> Materials => Set<Material>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Individual per-table configuration classes live in
        // MicroLIMS.Persistence/Configurations and are applied here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MicroLimsDbContext).Assembly);

        // Water sample locations hang off a WaterDepartment (mirrors EM's
        // Department -> Room). Optional FK; deletion is guarded in the
        // controller, so keep existing points intact rather than cascading.
        modelBuilder.Entity<WaterSamplingPoint>()
            .HasOne(p => p.WaterDepartment)
            .WithMany(d => d.SamplingPoints)
            .HasForeignKey(p => p.WaterDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // Frozen Principle #5 - Traceability. Captures every insert/update/
    // delete automatically so no service can forget to log a change.
    public override int SaveChanges()
    {
        CaptureAuditEntries();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CaptureAuditEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void CaptureAuditEntries()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog &&
                        e.Entity is not MaterialDocumentAccessLog && // append-only; excluded to prevent recursive audit
                        e.Entity is not EquipmentDocumentAccessLog && // append-only; excluded to prevent recursive audit
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .ToList();

        foreach (var entry in entries)
        {
            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => "Unknown"
            };

            string? previousValue = null;
            string? newValue = null;

            if (entry.State == EntityState.Modified)
            {
                previousValue = JsonSerializer.Serialize(entry.OriginalValues.Properties.ToDictionary(p => p.Name, p => entry.OriginalValues[p]));
                newValue = JsonSerializer.Serialize(entry.CurrentValues.Properties.ToDictionary(p => p.Name, p => entry.CurrentValues[p]));
            }
            else if (entry.State == EntityState.Added)
            {
                newValue = JsonSerializer.Serialize(entry.CurrentValues.Properties.ToDictionary(p => p.Name, p => entry.CurrentValues[p]));
            }
            else if (entry.State == EntityState.Deleted)
            {
                previousValue = JsonSerializer.Serialize(entry.OriginalValues.Properties.ToDictionary(p => p.Name, p => entry.OriginalValues[p]));
            }

            var idProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Id");

            AuditLogs.Add(new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                EntityId = idProperty?.CurrentValue?.ToString() ?? "unknown",
                Action = action,
                PreviousValue = previousValue,
                NewValue = newValue,
                UserId = CurrentUserId ?? 0,
                Timestamp = DateTime.UtcNow,
                BatchNumber = GetPropertyAsString(entry, "BatchNumber"),
                ControlNumber = GetPropertyAsString(entry, "ControlNumber"),
                SampleReferenceNumber = GetPropertyAsString(entry, "ReferenceNumber"),
                MediaLotNumber = GetPropertyAsString(entry, "LotNumber"),
                ReferenceStrainCode = entry.Entity is Cryovial ? GetPropertyAsString(entry, "Code") : null,
                CryovialCode = entry.Entity is Cryovial ? GetPropertyAsString(entry, "Code") : null,
                SampleId = GetPropertyAsInt(entry, "SampleId") ?? (entry.Entity is Sample ? int.TryParse(idProperty?.CurrentValue?.ToString(), out var sid) ? sid : null : null),
                TestOrderId = GetPropertyAsInt(entry, "TestOrderId") ?? (entry.Entity is TestOrder ? int.TryParse(idProperty?.CurrentValue?.ToString(), out var tid) ? tid : null : null)
            });
        }
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
