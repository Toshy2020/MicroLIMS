using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SolutionPreparationComponentConfiguration : IEntityTypeConfiguration<SolutionPreparationComponent>
{
    public void Configure(EntityTypeBuilder<SolutionPreparationComponent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntryCode).IsRequired().HasMaxLength(50);
        builder.Property(e => e.EntryName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.RecipeQuantity).HasColumnType("decimal(12,4)");
        builder.Property(e => e.QuantityUsed).HasColumnType("decimal(12,4)");

        builder.HasOne(e => e.Material).WithMany().HasForeignKey(e => e.MaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}
