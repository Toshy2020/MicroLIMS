using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class MandatoryNotificationTypeConfiguration : IEntityTypeConfiguration<MandatoryNotificationType>
{
    public void Configure(EntityTypeBuilder<MandatoryNotificationType> builder)
    {
        builder.ToTable("MandatoryNotificationTypes");

        builder.HasKey(m => new { m.SectionId, m.Type });
        builder.Property(m => m.Type).IsRequired().HasMaxLength(100);

        builder.HasOne(m => m.Section)
            .WithMany()
            .HasForeignKey(m => m.SectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.SetByUser)
            .WithMany()
            .HasForeignKey(m => m.SetByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.SetByUserId);
    }
}
