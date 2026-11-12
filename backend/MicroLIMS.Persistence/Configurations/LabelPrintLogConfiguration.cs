using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class LabelPrintLogConfiguration : IEntityTypeConfiguration<LabelPrintLog>
{
    public void Configure(EntityTypeBuilder<LabelPrintLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).IsRequired().HasMaxLength(250);
        builder.Property(l => l.Reason).HasMaxLength(500);

        builder.HasIndex(l => new { l.EntityType, l.EntityId, l.PrintNumber }).IsUnique();
        builder.HasIndex(l => l.BatchId);

        builder.HasOne<LabelFormat>()
            .WithMany()
            .HasForeignKey(l => l.LabelFormatId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(l => l.PrintedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
