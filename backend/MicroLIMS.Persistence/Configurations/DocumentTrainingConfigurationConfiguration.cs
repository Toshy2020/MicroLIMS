using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class DocumentTrainingConfigurationConfiguration : IEntityTypeConfiguration<DocumentTrainingConfiguration>
{
    public void Configure(EntityTypeBuilder<DocumentTrainingConfiguration> builder)
    {
        builder.ToTable("DocumentTrainingConfigurations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.DefaultAcknowledgementStatement)
            .IsRequired()
            .HasMaxLength(2000);

        builder.HasOne(c => c.DocumentType)
            .WithMany()
            .HasForeignKey(c => c.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.DocumentMaster)
            .WithMany()
            .HasForeignKey(c => c.DocumentMasterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ModifiedByUser)
            .WithMany()
            .HasForeignKey(c => c.ModifiedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.DocumentTypeId)
            .IsUnique()
            .HasFilter("\"DocumentTypeId\" IS NOT NULL");

        builder.HasIndex(c => c.DocumentMasterId)
            .IsUnique()
            .HasFilter("\"DocumentMasterId\" IS NOT NULL");
    }
}
