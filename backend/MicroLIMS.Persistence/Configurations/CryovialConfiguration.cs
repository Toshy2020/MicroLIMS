using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class CryovialConfiguration : IEntityTypeConfiguration<Cryovial>
{
    // EF's default name for the unique Code index below - how
    // CryovialService recognises a code clash on save.
    public const string CodeIndexName = "IX_Cryovials_Code";

    public void Configure(EntityTypeBuilder<Cryovial> builder)
    {
        builder.HasKey(c => c.Id);

        // A cryovial code is the batch's GMP identifier, cited by every
        // media evaluation that challenges organisms from it - two batches
        // must never share one.
        builder.HasIndex(c => c.Code).IsUnique();

        // Never let deleting a Material cascade into deleting Cryovial
        // history - same reasoning as the Media/Incubation FKs.
        builder.HasOne(c => c.Material).WithMany().HasForeignKey(c => c.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Organism).WithMany().HasForeignKey(c => c.OrganismId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ThawEventConfiguration : IEntityTypeConfiguration<ThawEvent>
{
    public void Configure(EntityTypeBuilder<ThawEvent> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasOne(t => t.Cryovial).WithMany(c => c.ThawHistory).HasForeignKey(t => t.CryovialId).OnDelete(DeleteBehavior.Restrict);
    }
}
