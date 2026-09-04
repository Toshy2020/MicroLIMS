using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentKeywordConfiguration : IEntityTypeConfiguration<DocumentKeyword>
{
    public void Configure(EntityTypeBuilder<DocumentKeyword> builder)
    {
        builder.ToTable("DocumentKeywords");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Keyword)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasOne(k => k.DocumentMaster)
            .WithMany(m => m.Keywords)
            .HasForeignKey(k => k.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(k => new { k.DocumentMasterId, k.Keyword });
    }
}
