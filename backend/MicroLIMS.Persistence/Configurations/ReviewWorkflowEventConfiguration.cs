using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ReviewWorkflowEventConfiguration : IEntityTypeConfiguration<ReviewWorkflowEvent>
{
    public void Configure(EntityTypeBuilder<ReviewWorkflowEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.PerformedByNameSnapshot).IsRequired().HasMaxLength(200);

        // So a record's full lifecycle timeline can be retrieved without
        // a table scan - same index shape as ElectronicSignature's.
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
    }
}
