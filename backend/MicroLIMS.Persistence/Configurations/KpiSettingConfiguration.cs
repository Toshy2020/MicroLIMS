using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class KpiSettingConfiguration : IEntityTypeConfiguration<KpiSetting>
{
    public void Configure(EntityTypeBuilder<KpiSetting> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.HoursPerAnalystDay).HasPrecision(4, 2);

        builder.HasOne(s => s.Section)
            .WithMany()
            .HasForeignKey(s => s.SectionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.SectionId).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_KpiSettings_Hours", "\"HoursPerAnalystDay\" > 0 AND \"HoursPerAnalystDay\" <= 24");
            t.HasCheckConstraint("CK_KpiSettings_Weekdays", "\"WorkingWeekdays\" BETWEEN 1 AND 127");
            t.HasCheckConstraint("CK_KpiSettings_Horizon", "\"LoadHorizonDays\" BETWEEN 1 AND 30");
        });
    }
}
