using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaConfigurationConfiguration : IEntityTypeConfiguration<MediaConfiguration>
{
    public void Configure(EntityTypeBuilder<MediaConfiguration> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name).IsRequired();

        builder.HasOne(m => m.MediaProduct)
            .WithMany(p => p.Configurations)
            .HasForeignKey(m => m.MediaProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.IncubationCondition)
            .WithMany()
            .HasForeignKey(m => m.MediaIncubationConditionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Exactly one evaluation configuration per media product.
        builder.HasIndex(m => m.MediaProductId).IsUnique();
    }
}

public class MediaConfigurationChallengeConfiguration : IEntityTypeConfiguration<MediaConfigurationChallenge>
{
    public void Configure(EntityTypeBuilder<MediaConfigurationChallenge> builder)
    {
        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.MediaConfiguration)
            .WithMany(m => m.Challenges)
            .HasForeignKey(c => c.MediaConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Organism)
            .WithMany()
            .HasForeignKey(c => c.OrganismId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.MediaConfigurationId, c.OrganismId, c.ChallengeRole }).IsUnique();
    }
}
