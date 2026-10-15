using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AnalystAvailabilityConfiguration : IEntityTypeConfiguration<AnalystAvailability>
{
    public void Configure(EntityTypeBuilder<AnalystAvailability> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FromDate).HasColumnType("date");
        builder.Property(a => a.ToDate).HasColumnType("date");
        builder.Property(a => a.FractionAvailable).HasPrecision(3, 2);
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(500);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.UserId, a.FromDate });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_AnalystAvailabilities_Range", "\"ToDate\" >= \"FromDate\"");
            t.HasCheckConstraint("CK_AnalystAvailabilities_Fraction", "\"FractionAvailable\" BETWEEN 0 AND 1");
        });
    }
}
