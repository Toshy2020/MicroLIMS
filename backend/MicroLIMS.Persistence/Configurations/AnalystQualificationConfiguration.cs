using MicroLIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroLIMS.Persistence.Configurations;

public class AnalystQualificationConfiguration : IEntityTypeConfiguration<AnalystQualification>
{
    public void Configure(EntityTypeBuilder<AnalystQualification> builder)
    {
        builder.Property<string>("SigningTransaction").HasDefaultValueSql("pg_current_xact_id()::text").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(q => q.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(q => q.SignedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(q => q.WithdrawnByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestDefinition>().WithMany().HasForeignKey(q => q.TestDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sample>().WithMany().HasForeignKey(q => q.EvidenceSampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(q => q.Signature).WithOne().HasForeignKey<AnalystQualification>(q => q.SignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(q => q.WithdrawnSignature).WithOne().HasForeignKey<AnalystQualification>(q => q.WithdrawnSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(q => q.Attachments).WithOne().HasForeignKey(a => a.AnalystQualificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(q => new { q.UserId, q.TestDefinitionId, q.QualifiedOn });
    }
}

public class QualificationAttachmentConfiguration : IEntityTypeConfiguration<QualificationAttachment>
{
    public void Configure(EntityTypeBuilder<QualificationAttachment> builder)
    {
        builder.Property(a => a.OriginalFileName).HasMaxLength(255);
        builder.Property(a => a.StorageKey).HasMaxLength(500);
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.FileExtension).HasMaxLength(20);
        builder.Property(a => a.ContentSha256).HasMaxLength(64);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
