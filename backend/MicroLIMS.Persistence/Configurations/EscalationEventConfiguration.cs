using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class EscalationEventConfiguration : IEntityTypeConfiguration<EscalationEvent>
{
    public void Configure(EntityTypeBuilder<EscalationEvent> builder)
    {
        builder.ToTable("EscalationEvents");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.RecordType).IsRequired().HasMaxLength(64);
        builder.Property(e => e.RecipientUserIds).IsRequired().HasColumnType("jsonb");

        builder.HasOne(e => e.EscalationRule)
            .WithMany()
            .HasForeignKey(e => e.EscalationRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.EscalationRuleId, e.RecordType, e.RecordId })
            .IsUnique()
            .HasDatabaseName("IX_EscalationEvents_Rule_Record");
    }
}
