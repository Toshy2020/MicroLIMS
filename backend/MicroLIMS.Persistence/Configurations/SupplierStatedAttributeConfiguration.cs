using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SupplierStatedAttributeConfiguration : IEntityTypeConfiguration<SupplierStatedAttribute>
{
    public void Configure(EntityTypeBuilder<SupplierStatedAttribute> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Grade).HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.StatedValue).IsRequired().HasMaxLength(500);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);

        builder.HasOne(x => x.Item)
            .WithMany()
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
