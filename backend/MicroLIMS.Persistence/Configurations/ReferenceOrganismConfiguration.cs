using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ReferenceOrganismConfiguration : IEntityTypeConfiguration<ReferenceOrganism>
{
    public void Configure(EntityTypeBuilder<ReferenceOrganism> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Code).IsRequired().HasMaxLength(50);
        builder.Property(r => r.StrainNumber).HasMaxLength(50);
        builder.HasIndex(r => r.Code).IsUnique();
        // The same strain can't be listed twice; strains without a number (collection Other) are not constrained.
        builder.HasIndex(r => new { r.CultureCollection, r.StrainNumber }).IsUnique().HasFilter("\"StrainNumber\" IS NOT NULL");
        builder.HasIndex(r => r.OrganismId);
        builder.HasIndex(r => r.OwnerSectionId);
        builder.HasOne(r => r.Organism).WithMany().HasForeignKey(r => r.OrganismId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.OwnerSection).WithMany().HasForeignKey(r => r.OwnerSectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
