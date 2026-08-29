using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestReturnEventConfiguration : IEntityTypeConfiguration<TestReturnEvent>
{
    public void Configure(EntityTypeBuilder<TestReturnEvent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.TestOrder)
            .WithMany()
            .HasForeignKey(e => e.TestOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ReviewerUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AssignedAnalyst)
            .WithMany()
            .HasForeignKey(e => e.AssignedAnalystId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Reason)
            .HasMaxLength(1000);

        builder.HasIndex(e => e.TestOrderId);
        builder.HasIndex(e => new { e.AssignedAnalystId, e.ReturnedAt });
    }
}
