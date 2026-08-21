using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class AutoclaveProgramHistoryConfiguration : IEntityTypeConfiguration<AutoclaveProgramHistory>
{
    public void Configure(EntityTypeBuilder<AutoclaveProgramHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Action).HasMaxLength(50).IsRequired();
        builder.Property(h => h.ProgramCode).HasMaxLength(50).IsRequired();
        builder.Property(h => h.PreviousTemperature).HasPrecision(5, 2);
        builder.Property(h => h.NewTemperature).HasPrecision(5, 2);
        builder.Property(h => h.Comment).HasMaxLength(500);

        builder.HasOne(h => h.AutoclaveProgram)
            .WithMany(p => p.History)
            .HasForeignKey(h => h.AutoclaveProgramId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
