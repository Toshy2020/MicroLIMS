using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class UserOrgMembershipConfiguration : IEntityTypeConfiguration<UserOrgMembership>
{
    public void Configure(EntityTypeBuilder<UserOrgMembership> builder)
    {
        builder.ToTable("UserOrgMemberships");

        builder.HasKey(m => m.Id);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Department)
            .WithMany()
            .HasForeignKey(m => m.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Section)
            .WithMany()
            .HasForeignKey(m => m.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.UserId, m.DepartmentId, m.SectionId })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
