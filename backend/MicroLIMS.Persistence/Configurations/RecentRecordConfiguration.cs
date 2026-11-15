using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class RecentRecordConfiguration : IEntityTypeConfiguration<RecentRecord>
{
    public void Configure(EntityTypeBuilder<RecentRecord> builder)
    {
        builder.ToTable("RecentRecords");

        builder.HasKey(r => new { r.UserId, r.RecordType, r.RecordId });
        builder.Property(r => r.RecordType).IsRequired().HasMaxLength(RecentRecord.MaxRecordTypeLength);

        builder.HasIndex(r => new { r.UserId, r.OpenedAt })
            .IsDescending(false, true);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
