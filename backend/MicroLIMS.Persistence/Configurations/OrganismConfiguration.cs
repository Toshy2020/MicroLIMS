using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class OrganismConfiguration : IEntityTypeConfiguration<Organism>
{
    // The real uniqueness guard on ScientificName is a case-insensitive
    // expression index (CREATE UNIQUE INDEX ... ON "Organisms"
    // (LOWER("ScientificName"))) added via raw SQL in the
    // AddOrganismMasterAndSwapOrganismName migration - EF's fluent API
    // can't express a Postgres expression index, so no .IsUnique() here
    // for ScientificName itself (it would only add a case-sensitive one).
    public void Configure(EntityTypeBuilder<Organism> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.ScientificName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.AtccNumber).HasMaxLength(50);
        builder.Property(o => o.CommonName).HasMaxLength(200);
        builder.HasIndex(o => o.AtccNumber).IsUnique().HasFilter("\"AtccNumber\" IS NOT NULL");
    }
}
