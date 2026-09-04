using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentMasterAssignmentConfiguration : IEntityTypeConfiguration<DocumentMasterAssignment>
{
    public void Configure(EntityTypeBuilder<DocumentMasterAssignment> builder)
    {
        builder.ToTable("DocumentMasterAssignments");

        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.DocumentMaster)
            .WithMany(m => m.Assignments)
            .HasForeignKey(a => a.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AssignedByUser)
            .WithMany()
            .HasForeignKey(a => a.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.DocumentMasterId, a.UserId, a.AssignmentRole });
    }
}
