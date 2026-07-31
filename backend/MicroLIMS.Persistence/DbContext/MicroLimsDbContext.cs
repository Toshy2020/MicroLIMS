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
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Specification> Specifications => Set<Specification>();
    public DbSet<Media> Media => Set<Media>();
    public DbSet<GptChallengeResult> GptChallengeResults => Set<GptChallengeResult>();
    public DbSet<ReferenceStrain> ReferenceStrains => Set<ReferenceStrain>();
    public DbSet<Cryovial> Cryovials => Set<Cryovial>();
    public DbSet<PassageEvent> PassageEvents => Set<PassageEvent>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<SampleTest> SampleTests => Set<SampleTest>();
    public DbSet<TestOrder> TestOrders => Set<TestOrder>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Incubation> Incubations => Set<Incubation>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<MachinePart> MachineParts => Set<MachinePart>();
    public DbSet<WaterSamplingPoint> WaterSamplingPoints => Set<WaterSamplingPoint>();
    public DbSet<EMRoom> EMRooms => Set<EMRoom>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<WorkflowHistory> WorkflowHistories => Set<WorkflowHistory>();
    public DbSet<MediaUsage> MediaUsages => Set<MediaUsage>();
    public DbSet<RoomMonitoring> RoomMonitorings => Set<RoomMonitoring>();
    public DbSet<MachinePartConfiguration> MachinePartConfigurations => Set<MachinePartConfiguration>();
    public DbSet<SamplingConfiguration> SamplingConfigurations => Set<SamplingConfiguration>();
    public DbSet<PathogenObservation> PathogenObservations => Set<PathogenObservation>();
    public DbSet<ReportSnapshot> ReportSnapshots => Set<ReportSnapshot>();

    // Phase 1 additions
    public DbSet<CauseOfTesting> CausesOfTesting => Set<CauseOfTesting>();
    public DbSet<DiluentType> DiluentTypes => Set<DiluentType>();
    public DbSet<Neutralizer> Neutralizers => Set<Neutralizer>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<MediaType> MediaTypes => Set<MediaType>();
    public DbSet<RoomTestConfiguration> RoomTestConfigurations => Set<RoomTestConfiguration>();
    public DbSet<SamplePreparation> SamplePreparations => Set<SamplePreparation>();
    public DbSet<ExpectedIndicationResult> ExpectedIndicationResults => Set<ExpectedIndicationResult>();

    // Inventory module
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<EquipmentInventory> EquipmentInventories => Set<EquipmentInventory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Individual per-table configuration classes live in
        // MicroLIMS.Persistence/Configurations and are applied here.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MicroLimsDbContext).Assembly);
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
                ReferenceStrainCode = entry.Entity is Cryovial or ReferenceStrain ? GetPropertyAsString(entry, "Code") : null,
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
