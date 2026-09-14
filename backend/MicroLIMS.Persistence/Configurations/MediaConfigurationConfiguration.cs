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

        // Multiple configurations can belong to the same MediaProduct (e.g. two
        // Tryptic Soy Agar rows with different incubation windows). There's no
        // separate disambiguating field - MediaProductId + the full profile
        // together is what must be unique, which also blocks a genuine accidental
        // duplicate (same product, same profile, entered twice).
        builder.HasIndex(m => new { m.MediaProductId, m.IncubationMinHours, m.IncubationMaxHours, m.TemperatureMin, m.TemperatureMax }).IsUnique();
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
