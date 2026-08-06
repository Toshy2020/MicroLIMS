using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DataExportLogConfiguration : IEntityTypeConfiguration<DataExportLog>
{
    public void Configure(EntityTypeBuilder<DataExportLog> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.UserName).IsRequired();
        builder.Property(d => d.FilterJson).IsRequired();
        builder.Property(d => d.ExportType).IsRequired().HasMaxLength(50);

        builder.HasIndex(d => d.ExportedAt);
        builder.HasIndex(d => d.UserId);
    }
}
