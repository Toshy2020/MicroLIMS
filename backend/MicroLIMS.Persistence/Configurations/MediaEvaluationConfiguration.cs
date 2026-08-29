using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaEvaluationConfiguration : IEntityTypeConfiguration<MediaEvaluation>
{
    public void Configure(EntityTypeBuilder<MediaEvaluation> builder)
    {
        builder.HasKey(e => e.Id);

        // Deleting a Media lot can never cascade-delete its evaluation
        // history - it's part of the release audit trail.
        builder.HasOne(e => e.Media).WithMany().HasForeignKey(e => e.MediaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MediaEvaluationChallengeConfiguration : IEntityTypeConfiguration<MediaEvaluationChallenge>
{
    public void Configure(EntityTypeBuilder<MediaEvaluationChallenge> builder)
    {
        builder.HasKey(c => c.Id);

        // Challenges belong to their evaluation - deleting the evaluation
        // deletes its challenges.
        builder.HasOne(c => c.MediaEvaluation).WithMany(e => e.Challenges).HasForeignKey(c => c.MediaEvaluationId).OnDelete(DeleteBehavior.Cascade);

        // Cryovial/Incubation/Organism are shared reference data
        // referenced by many things - never cascade-delete them from here.
        builder.HasOne(c => c.Cryovial).WithMany().HasForeignKey(c => c.CryovialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Incubation).WithMany().HasForeignKey(c => c.IncubationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Organism).WithMany().HasForeignKey(c => c.OrganismId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ReferenceMedia).WithMany().HasForeignKey(c => c.ReferenceMediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.LyophilizedDisk).WithMany().HasForeignKey(c => c.LyophilizedDiskId).OnDelete(DeleteBehavior.Restrict);
    }
}
