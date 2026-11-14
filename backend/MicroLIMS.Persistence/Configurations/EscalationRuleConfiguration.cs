using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class EscalationRuleConfiguration : IEntityTypeConfiguration<EscalationRule>
{
    public void Configure(EntityTypeBuilder<EscalationRule> builder)
    {
        builder.ToTable("EscalationRules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.ThresholdWorkingHours).HasPrecision(6, 2);

        builder.HasOne(r => r.Section)
            .WithMany()
            .HasForeignKey(r => r.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CreatedByUser)
            .WithMany()
            .HasForeignKey(r => r.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.RetiredByUser)
            .WithMany()
            .HasForeignKey(r => r.RetiredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReplacesRule)
            .WithMany()
            .HasForeignKey(r => r.ReplacesRuleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.SectionId, r.Trigger })
            .HasFilter("\"RetiredAt\" IS NULL")
            .HasDatabaseName("IX_EscalationRules_SectionId_Trigger_Active");

        builder.HasIndex(r => r.CreatedByUserId);
        builder.HasIndex(r => r.RetiredByUserId);
        builder.HasIndex(r => r.ReplacesRuleId);
    }
}
