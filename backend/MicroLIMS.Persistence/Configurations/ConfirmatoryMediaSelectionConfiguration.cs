using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ConfirmatoryMediaSelectionConfiguration : IEntityTypeConfiguration<ConfirmatoryMediaSelection>
{
    public void Configure(EntityTypeBuilder<ConfirmatoryMediaSelection> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.WorkflowStepResultId, s.MaterialId }).IsUnique();

        builder.HasOne(s => s.WorkflowStepResult)
            .WithMany(r => r.Selections)
            .HasForeignKey(s => s.WorkflowStepResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Material).WithMany().HasForeignKey(s => s.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Media).WithMany().HasForeignKey(s => s.MediaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Equipment).WithMany().HasForeignKey(s => s.EquipmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
