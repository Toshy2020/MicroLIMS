using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestAnalysisConfiguration : IEntityTypeConfiguration<TestAnalysis>
{
    public void Configure(EntityTypeBuilder<TestAnalysis> builder)
    {
        builder.ToTable("TestAnalyses");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AnalysisType).IsRequired();
        builder.Property(a => a.AnalysedAt).IsRequired();
        builder.Property(a => a.UnitAmount).HasPrecision(18, 6);
        builder.Property(a => a.ConditionsJson).HasColumnType("jsonb");
        builder.Property(a => a.ValidityRecordType).HasMaxLength(40);
        builder.Property(a => a.Comment).HasMaxLength(1000);
        builder.Property(a => a.IsActive).HasDefaultValue(true);

        builder.HasOne(a => a.TestOrder)
            .WithMany()
            .HasForeignKey(a => a.TestOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Equipment)
            .WithMany()
            .HasForeignKey(a => a.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.EnteredByUser)
            .WithMany()
            .HasForeignKey(a => a.EnteredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Signature)
            .WithMany()
            .HasForeignKey(a => a.SignatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TestOrderId, a.IsActive });
        builder.HasIndex(a => a.EnteredAt);
        builder.HasIndex(a => a.EnteredByUserId);
        builder.HasIndex(a => a.SignatureId);
        builder.HasIndex(a => a.EquipmentId);
    }
}
