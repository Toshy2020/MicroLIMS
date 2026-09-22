using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SystemSuitabilityStandardResponseConfiguration : IEntityTypeConfiguration<SystemSuitabilityStandardResponse>
{
    public void Configure(EntityTypeBuilder<SystemSuitabilityStandardResponse> builder)
    {
        builder.ToTable("SystemSuitabilityStandardResponses");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Response).HasPrecision(28, 10);

        builder.HasOne(r => r.SystemSuitabilityRunAnalyte)
            .WithMany(a => a.Responses)
            .HasForeignKey(r => r.SystemSuitabilityRunAnalyteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.SystemSuitabilityRunAnalyteId);
        builder.HasIndex(r => new { r.SystemSuitabilityRunAnalyteId, r.Index }).IsUnique();
    }
}
