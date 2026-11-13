using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.JobTitle).HasMaxLength(100);
        builder.Property(u => u.SignInMethod).HasConversion<int>().HasDefaultValue(SignInMethod.Local);
        builder.Property(u => u.DirectoryUsername).HasMaxLength(256);
        builder.Property(u => u.DirectorySubjectId).HasMaxLength(512);
        builder.Property(u => u.IsEmergencyAccount).HasDefaultValue(false);

        builder.HasIndex(u => u.DirectoryUsername)
            .IsUnique()
            .HasFilter("\"DirectoryUsername\" IS NOT NULL");

        builder.HasIndex(u => u.DirectorySubjectId)
            .IsUnique()
            .HasFilter("\"DirectorySubjectId\" IS NOT NULL");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Users_EmergencyAccount_MustBeLocal",
            "(NOT \"IsEmergencyAccount\") OR (\"SignInMethod\" = 0)"));
    }
}
