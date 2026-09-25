using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestDefinitionStageReplicateConfiguration : IEntityTypeConfiguration<TestDefinitionStageReplicate>
{
    public void Configure(EntityTypeBuilder<TestDefinitionStageReplicate> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.TestDefinitionId, r.Role }).IsUnique();

        builder.HasOne(r => r.TestDefinition)
            .WithMany(t => t.StageReplicates)
            .HasForeignKey(r => r.TestDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
