using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AutoclaveProgramConfiguration : IEntityTypeConfiguration<AutoclaveProgram>
{
    public void Configure(EntityTypeBuilder<AutoclaveProgram> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.ProgramCode).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ProgramName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.LoadType).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Temperature).HasPrecision(5, 2);

        builder.HasOne(p => p.Equipment)
            .WithMany()
            .HasForeignKey(p => p.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.EquipmentId, p.ProgramCode }).IsUnique();
    }
}
