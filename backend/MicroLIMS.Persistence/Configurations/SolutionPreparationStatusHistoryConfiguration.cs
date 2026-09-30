using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SolutionPreparationStatusHistoryConfiguration : IEntityTypeConfiguration<SolutionPreparationStatusHistory>
{
    public void Configure(EntityTypeBuilder<SolutionPreparationStatusHistory> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reason).HasMaxLength(500);
    }
}
