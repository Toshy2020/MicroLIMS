using MicroLIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroLIMS.Persistence.Configurations;

public sealed class CompetencyOverrideConfiguration : IEntityTypeConfiguration<CompetencyOverride>
{
    public void Configure(EntityTypeBuilder<CompetencyOverride> builder)
    {
        builder.Property(row => row.Reason).HasMaxLength(2000);
        builder.HasOne(row => row.TestOrder).WithMany().HasForeignKey(row => row.TestOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sample>().WithMany().HasForeignKey(row => row.SampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.AnalystUser).WithMany().HasForeignKey(row => row.AnalystUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(row => row.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.Signature).WithOne().HasForeignKey<CompetencyOverride>(row => row.SignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(row => row.TestOrderId, "IX_CompetencyOverrides_ActiveOrder")
            .HasFilter(""" "EndedAtUtc" IS NULL """).IsUnique();
        builder.HasIndex(row => new { row.SampleId, row.GrantedByUserId });
    }
}
