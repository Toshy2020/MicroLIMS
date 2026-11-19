using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class LabelFormatConfiguration : IEntityTypeConfiguration<LabelFormat>
{
    public void Configure(EntityTypeBuilder<LabelFormat> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.WidthMm).HasPrecision(5, 2);
        builder.Property(f => f.HeightMm).HasPrecision(5, 2);

        builder.HasIndex(f => f.EntityType).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_LabelFormats_WidthMm", "\"WidthMm\" BETWEEN 10 AND 150");
            t.HasCheckConstraint("CK_LabelFormats_HeightMm", "\"HeightMm\" BETWEEN 10 AND 150");
            t.HasCheckConstraint("CK_LabelFormats_DefaultCopies", "\"DefaultCopies\" BETWEEN 1 AND 50");
        });
    }
}
