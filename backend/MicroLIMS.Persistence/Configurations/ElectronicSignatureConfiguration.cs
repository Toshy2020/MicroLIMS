using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ElectronicSignatureConfiguration : IEntityTypeConfiguration<ElectronicSignature>
{
    public void Configure(EntityTypeBuilder<ElectronicSignature> builder)
    {
        builder.HasKey(s => s.Id);

        // Never let deleting a User cascade into deleting signature
        // history - the signature record must survive the signer's
        // account being removed.
        builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);

        // So a record's full signature trail can be retrieved for
        // display and reports without a table scan.
        builder.HasIndex(s => new { s.EntityType, s.EntityId });
    }
}
