using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class CalibrationRunConfiguration : IEntityTypeConfiguration<CalibrationRun>
{
    public const string CodeIndexName = "IX_CalibrationRuns_Code";

    public void Configure(EntityTypeBuilder<CalibrationRun> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code).IsRequired().HasMaxLength(50);
        builder.HasIndex(r => r.Code).IsUnique();

        builder.Property(r => r.Comment).HasMaxLength(500);
        builder.Property(r => r.WithdrawalReason).HasMaxLength(1000);

        builder.HasOne(r => r.TestDefinition)
            .WithMany()
            .HasForeignKey(r => r.TestDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Section)
            .WithMany()
            .HasForeignKey(r => r.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Equipment)
            .WithMany()
            .HasForeignKey(r => r.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CalibrationStandardMaterial)
            .WithMany()
            .HasForeignKey(r => r.CalibrationStandardMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.IcvStandardMaterial)
            .WithMany()
            .HasForeignKey(r => r.IcvStandardMaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PerformedByUser)
            .WithMany()
            .HasForeignKey(r => r.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Signature)
            .WithMany()
            .HasForeignKey(r => r.SignatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.WithdrawnByUser)
            .WithMany()
            .HasForeignKey(r => r.WithdrawnByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.WithdrawalSignature)
            .WithMany()
            .HasForeignKey(r => r.WithdrawalSignatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Document)
            .WithOne(d => d.CalibrationRun)
            .HasForeignKey<CalibrationRunDocument>(d => d.CalibrationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Analytes)
            .WithOne(a => a.CalibrationRun)
            .HasForeignKey(a => a.CalibrationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.SectionId);
        builder.HasIndex(r => r.TestDefinitionId);
        builder.HasIndex(r => r.PerformedAt);
        builder.HasIndex(r => r.CalibrationAt);
        builder.HasIndex(r => r.Status);
    }
}

