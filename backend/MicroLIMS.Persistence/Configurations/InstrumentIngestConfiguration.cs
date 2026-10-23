using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class InstrumentIngestConfiguration : IEntityTypeConfiguration<InstrumentIngest>
{
    public void Configure(EntityTypeBuilder<InstrumentIngest> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.SequenceName).IsRequired().HasMaxLength(200);

        builder.HasOne(i => i.InstrumentAgent)
            .WithMany()
            .HasForeignKey(i => i.InstrumentAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Equipment)
            .WithMany()
            .HasForeignKey(i => i.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.EquipmentId, i.SequenceName, i.InjectionNumber, i.AcquiredAtUtc }).IsUnique();
        builder.HasIndex(i => i.InstrumentAgentId);
    }
}

public class InstrumentIngestVersionConfiguration : IEntityTypeConfiguration<InstrumentIngestVersion>
{
    public void Configure(EntityTypeBuilder<InstrumentIngestVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.ReportSha256).IsRequired().HasMaxLength(64);
        builder.Property(v => v.ReportStorageKey).IsRequired().HasMaxLength(500);
        builder.Property(v => v.ReportFileName).IsRequired().HasMaxLength(260);
        builder.Property(v => v.StatusReason).HasMaxLength(2000);
        builder.Property(v => v.AcquiredAtLocalText).IsRequired().HasMaxLength(100);
        builder.Property(v => v.TimeZoneIdUsed).IsRequired().HasMaxLength(100);
        builder.Property(v => v.OperatorAsReported).HasMaxLength(200);
        builder.Property(v => v.Status).HasConversion<int>();

        builder.HasOne(v => v.InstrumentIngest)
            .WithMany(i => i.Versions)
            .HasForeignKey(v => v.InstrumentIngestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.InstrumentIngestId, v.ReportSha256 }).IsUnique();
        builder.HasIndex(v => new { v.InstrumentIngestId, v.VersionNo }).IsUnique();
    }
}

public class InstrumentIngestPayloadConfiguration : IEntityTypeConfiguration<InstrumentIngestPayload>
{
    public void Configure(EntityTypeBuilder<InstrumentIngestPayload> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.ParserVersion).IsRequired().HasMaxLength(50);
        builder.Property(p => p.ManifestJson).IsRequired().HasColumnType("jsonb");
        builder.Property(p => p.ParsedJson).IsRequired().HasColumnType("jsonb");

        builder.HasOne(p => p.InstrumentIngestVersion)
            .WithMany(v => v.Payloads)
            .HasForeignKey(p => p.InstrumentIngestVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.InstrumentIngestVersionId);
    }
}
