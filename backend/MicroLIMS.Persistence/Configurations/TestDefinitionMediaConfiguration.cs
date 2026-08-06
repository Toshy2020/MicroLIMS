using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestDefinitionMediaConfiguration : IEntityTypeConfiguration<TestDefinitionMedia>
{
    public void Configure(EntityTypeBuilder<TestDefinitionMedia> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => new { t.TestDefinitionId, t.MediaTypeId, t.StepName }).IsUnique();

        builder.HasOne(t => t.TestDefinition).WithMany().HasForeignKey(t => t.TestDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.MediaType).WithMany().HasForeignKey(t => t.MediaTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
