using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ResultReadingConfiguration : IEntityTypeConfiguration<ResultReading>
{
    public void Configure(EntityTypeBuilder<ResultReading> builder)
    {
        builder.ToTable("ResultReadings");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Kind).IsRequired();
        builder.Property(r => r.Index).IsRequired();
        builder.Property(r => r.TimePointMinutes).HasPrecision(18, 6);
        builder.Property(r => r.Value1).HasPrecision(28, 10);
        builder.Property(r => r.Value2).HasPrecision(28, 10);
        builder.Property(r => r.Value3).HasPrecision(28, 10);
        builder.Property(r => r.Text).HasMaxLength(500);
        builder.Property(r => r.ComputedValue).HasPrecision(28, 10);

        builder.HasOne(r => r.ParameterResult)
            .WithMany(p => p.Readings)
            .HasForeignKey(r => r.ParameterResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.ParameterResultId);
    }
}
