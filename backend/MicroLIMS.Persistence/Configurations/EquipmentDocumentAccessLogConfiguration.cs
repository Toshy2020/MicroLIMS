using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class EquipmentDocumentAccessLogConfiguration : IEntityTypeConfiguration<EquipmentDocumentAccessLog>
{
    public void Configure(EntityTypeBuilder<EquipmentDocumentAccessLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.HasIndex(l => l.DocumentId);
        builder.HasIndex(l => l.EquipmentInventoryId);
        builder.HasIndex(l => l.UserId);
    }
}
