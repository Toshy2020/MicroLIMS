using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaChallengeSpecConfiguration : IEntityTypeConfiguration<MediaChallengeSpec>
{
    public void Configure(EntityTypeBuilder<MediaChallengeSpec> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.MaterialName, s.EvaluationType, s.OrganismId, s.ChallengeRole }).IsUnique();
        builder.HasOne(s => s.Organism).WithMany().HasForeignKey(s => s.OrganismId).OnDelete(DeleteBehavior.Restrict);
    }
}
