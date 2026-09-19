using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ElementalAssayEntryConfiguration : IEntityTypeConfiguration<ElementalAssayEntry>
{
    public void Configure(EntityTypeBuilder<ElementalAssayEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UnitAmount).HasPrecision(18, 6);
        builder.Property(e => e.SampleMatrix).IsRequired();
        builder.Property(e => e.AnalysedAt).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.Comment).HasMaxLength(1000);

        builder.HasOne(e => e.TestOrder)
            .WithMany()
            .HasForeignKey(e => e.TestOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EnteredByUser)
            .WithMany()
            .HasForeignKey(e => e.EnteredByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Signature)
            .WithMany()
            .HasForeignKey(e => e.SignatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TestOrderId, e.IsActive });
        builder.HasIndex(e => e.EnteredAt);
        builder.HasIndex(e => e.EnteredByUserId);
        builder.HasIndex(e => e.SignatureId);
    }
}
