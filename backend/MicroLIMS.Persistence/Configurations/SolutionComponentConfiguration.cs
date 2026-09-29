using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SolutionComponentConfiguration : IEntityTypeConfiguration<SolutionComponent>
{
    public void Configure(EntityTypeBuilder<SolutionComponent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Quantity).HasColumnType("decimal(12,4)");

        builder.HasIndex(e => new { e.SolutionMasterId, e.Order });

        builder.HasOne(e => e.MaterialMasterEntry).WithMany().HasForeignKey(e => e.MaterialMasterEntryId).OnDelete(DeleteBehavior.Restrict);
    }
}
