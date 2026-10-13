using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class WorkflowProfileVersionConfiguration : IEntityTypeConfiguration<WorkflowProfileVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowProfileVersion> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.TestCode).HasMaxLength(50);
        builder.Property(v => v.WorkUnits).HasPrecision(8, 2);
        builder.Property(v => v.TargetAnalystDays).HasPrecision(6, 2);
        builder.Property(v => v.EffectiveFrom).HasColumnType("date");
        builder.Property(v => v.ReasonForChange).HasMaxLength(500).IsRequired();

        builder.HasIndex(v => new { v.WorkflowType, v.TestCode, v.EffectiveFrom })
            .HasDatabaseName("IX_WorkflowProfileVersions_WorkflowType_TestCode_EffectiveFrom");

        builder.HasOne(v => v.ElectronicSignature).WithMany()
            .HasForeignKey(v => v.ElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_WorkflowProfileVersions_UnitsPositive", "\"WorkUnits\" IS NULL OR \"WorkUnits\" > 0");
            t.HasCheckConstraint("CK_WorkflowProfileVersions_TargetPositive", "\"TargetAnalystDays\" IS NULL OR \"TargetAnalystDays\" > 0");
            t.HasCheckConstraint("CK_WorkflowProfileVersions_AnyValue", "\"WorkUnits\" IS NOT NULL OR \"TargetAnalystDays\" IS NOT NULL");
        });
    }
}
