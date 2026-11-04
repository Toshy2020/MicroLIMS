using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class BackgroundJobRunConfiguration : IEntityTypeConfiguration<BackgroundJobRun>
{
    public void Configure(EntityTypeBuilder<BackgroundJobRun> builder)
    {
        builder.ToTable("BackgroundJobRuns");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.JobName).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Error).HasMaxLength(BackgroundJobRun.MaxErrorLength);
        builder.Property(r => r.Reason).HasMaxLength(BackgroundJobRun.MaxReasonLength);
        builder.Property(r => r.Outcome).HasConversion<int>();
        builder.Property(r => r.TriggeredBy).HasConversion<int>();

        // The user id is informational and the row must outlive the user, so no FK.
        builder.HasIndex(r => new { r.JobName, r.StartedAt });
    }
}
