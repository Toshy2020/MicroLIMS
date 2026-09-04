using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentNumberingConfigurationConfiguration : IEntityTypeConfiguration<DocumentNumberingConfiguration>
{
    public void Configure(EntityTypeBuilder<DocumentNumberingConfiguration> builder)
    {
        builder.ToTable("DocumentNumberingConfigurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Prefix)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.NumberFormat)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(c => c.ModifiedByUser)
            .WithMany()
            .HasForeignKey(c => c.ModifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
