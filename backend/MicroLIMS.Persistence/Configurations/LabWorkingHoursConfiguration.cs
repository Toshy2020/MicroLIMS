using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class LabWorkingHoursConfiguration : IEntityTypeConfiguration<LabWorkingHours>
{
    public void Configure(EntityTypeBuilder<LabWorkingHours> builder)
    {
        builder.ToTable("LabWorkingHours", t =>
            t.HasCheckConstraint(
                "CK_LabWorkingHours_StartBeforeEndOnWorkingDay",
                "NOT \"IsWorkingDay\" OR (\"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"StartMinute\" < \"EndMinute\")"));

        builder.HasKey(w => w.DayOfWeek);
        builder.Property(w => w.DayOfWeek).ValueGeneratedNever();
    }
}
