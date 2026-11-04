using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class BackupRunConfiguration : IEntityTypeConfiguration<BackupRun>
{
    public void Configure(EntityTypeBuilder<BackupRun> builder)
    {
        builder.ToTable("BackupRuns");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.StartedAt).IsRequired();
        builder.Property(b => b.FinishedAt).IsRequired();
        builder.Property(b => b.Succeeded).IsRequired();

        builder.Property(b => b.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(b => b.Error)
            .HasMaxLength(1000);

        builder.Property(b => b.Host)
            .IsRequired()
            .HasMaxLength(255);

        // Newest-first is the only read pattern (the overview's latest run).
        builder.HasIndex(b => b.StartedAt);
    }
}
