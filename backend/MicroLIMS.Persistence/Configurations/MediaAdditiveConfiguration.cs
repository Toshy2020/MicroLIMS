using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaAdditiveConfiguration : IEntityTypeConfiguration<MediaAdditive>
{
    public void Configure(EntityTypeBuilder<MediaAdditive> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Quantity).HasColumnType("decimal(12,4)");
        builder.Property(a => a.Unit).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Code).IsRequired().HasMaxLength(100);

        builder.HasOne(a => a.Media).WithMany(m => m.Additives).HasForeignKey(a => a.MediaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.Material).WithMany().HasForeignKey(a => a.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.SolutionPreparation).WithMany().HasForeignKey(a => a.SolutionPreparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.MediaId);
    }
}
