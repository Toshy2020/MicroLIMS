using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MasterDataImportConfiguration : IEntityTypeConfiguration<MasterDataImport>
{
    public void Configure(EntityTypeBuilder<MasterDataImport> builder)
    {
        builder.Property(i => i.FileName).HasMaxLength(260).IsRequired();
        builder.Property(i => i.FileSha256).HasMaxLength(64).IsRequired();
        builder.Property(i => i.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(i => i.Comment).HasMaxLength(2000);
        builder.Property(i => i.VerificationComment).HasMaxLength(2000);
        builder.HasOne(i => i.ImportedByUser).WithMany().HasForeignKey(i => i.ImportedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.VerifiedByUser).WithMany().HasForeignKey(i => i.VerifiedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(i => i.Records).WithOne(r => r.MasterDataImport!).HasForeignKey(r => r.MasterDataImportId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MasterDataImportRecordConfiguration : IEntityTypeConfiguration<MasterDataImportRecord>
{
    public void Configure(EntityTypeBuilder<MasterDataImportRecord> builder)
    {
        builder.Property(r => r.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Sheet).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => new { r.EntityType, r.EntityId });
    }
}
