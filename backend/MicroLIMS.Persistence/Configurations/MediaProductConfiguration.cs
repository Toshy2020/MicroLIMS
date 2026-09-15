using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MediaProductConfiguration : IEntityTypeConfiguration<MediaProduct>
{
    public const string NameIndexName = "IX_MediaProducts_Name_Lower";
    public const string CodeIndexName = "IX_MediaProducts_Code_Lower";

    // The real uniqueness guards on Name and Code are case-insensitive
    // expression indexes (CREATE UNIQUE INDEX ... ON "MediaProducts"
    // (lower("Name")) and (lower("Code"))) added via raw SQL in the
    // AddMediaProductMaster migration - EF's fluent API can't express a
    // Postgres expression index, so no .IsUnique() here for Name or Code
    // themselves (it would only add case-sensitive ones).
    public void Configure(EntityTypeBuilder<MediaProduct> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(10);
    }
}
