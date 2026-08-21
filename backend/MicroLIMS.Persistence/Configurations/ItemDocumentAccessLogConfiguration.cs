using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ItemDocumentAccessLogConfiguration : IEntityTypeConfiguration<ItemDocumentAccessLog>
{
    public void Configure(EntityTypeBuilder<ItemDocumentAccessLog> builder)
    {
        builder.HasKey(l => l.Id);

        builder.HasOne(l => l.Document)
            .WithMany()
            .HasForeignKey(l => l.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.DocumentId);
    }
}
