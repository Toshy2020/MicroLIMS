using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SampleSectionSignoffConfiguration : IEntityTypeConfiguration<SampleSectionSignoff>
{
    public void Configure(EntityTypeBuilder<SampleSectionSignoff> builder)
    {
        builder.ToTable("SampleSectionSignoffs");
        builder.HasKey(s => s.Id);

        // Review/approval history is GMP evidence - nothing it points at may
        // cascade it away.
        builder.HasOne(s => s.Sample).WithMany(x => x.SectionSignoffs).HasForeignKey(s => s.SampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Section).WithMany().HasForeignKey(s => s.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.ReviewSignature).WithMany().HasForeignKey(s => s.ReviewSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.ApprovalSignature).WithMany().HasForeignKey(s => s.ApprovalSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.CloseSignature).WithMany().HasForeignKey(s => s.CloseSignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.SampleId, s.SectionId }).IsUnique();
        builder.HasIndex(s => new { s.SectionId, s.Status });
    }
}
