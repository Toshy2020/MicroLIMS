using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired();
        builder.Property(e => e.Code).IsRequired();
        builder.Property(e => e.Vendor).HasMaxLength(100);
        builder.Property(e => e.ConnectionSettings).HasColumnType("jsonb");

        builder.HasOne(e => e.Section)
            .WithMany()
            .HasForeignKey(e => e.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.SectionId);
        builder.HasIndex(e => e.Code);
    }
}
