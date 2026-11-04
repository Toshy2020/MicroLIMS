using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class NotApplicableTestConfiguration : IEntityTypeConfiguration<NotApplicableTest>
{
    public void Configure(EntityTypeBuilder<NotApplicableTest> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TestCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Grade).HasMaxLength(20);
        builder.Property(x => x.Justification).IsRequired();
        builder.Property(x => x.Criticality).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ImmediateAction).IsRequired();
        builder.Property(x => x.LongTermAction).IsRequired();
        builder.Property(x => x.Reference).HasMaxLength(100);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HeadSignature)
            .WithMany()
            .HasForeignKey(x => x.HeadSignatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FinalSignature)
            .WithMany()
            .HasForeignKey(x => x.FinalSignatureId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique: one non-withdrawn record per (ItemId, Grade, TestCode).
        // Withdrawn = 3.
        builder.HasIndex(n => new { n.ItemId, n.Grade, n.TestCode })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("\"Status\" != 3");
    }
}
