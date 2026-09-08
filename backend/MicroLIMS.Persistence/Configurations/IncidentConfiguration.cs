using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("Incidents");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(i => i.Summary)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.ResolutionNotes)
            .HasMaxLength(4000);

        // Resolver is informational - clearing it on user deletion is
        // preferable to blocking the deletion of a departed admin.
        builder.HasOne(i => i.ResolvedByUser)
            .WithMany()
            .HasForeignKey(i => i.ResolvedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.CorrelationId);

        // Primary admin-UI filter columns.
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.Severity);

        // The incident list is ordered newest-first and date-range
        // filtered on LastSeenUtc.
        builder.HasIndex(i => i.LastSeenUtc);
    }
}
