using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MaterialDocumentAccessLogConfiguration : IEntityTypeConfiguration<MaterialDocumentAccessLog>
{
    public void Configure(EntityTypeBuilder<MaterialDocumentAccessLog> builder)
    {
        builder.HasKey(l => l.Id);

        // No navigation properties — this is an append-only log table.
        // DocumentId and MaterialId are stored as plain integers for
        // historical retention even if the referenced rows are ever
        // subject to future data management operations.
        builder.HasIndex(l => l.DocumentId);
        builder.HasIndex(l => l.MaterialId);
        builder.HasIndex(l => l.UserId);
    }
}
