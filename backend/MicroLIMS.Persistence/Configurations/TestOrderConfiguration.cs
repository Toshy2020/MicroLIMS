using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TestOrderConfiguration : IEntityTypeConfiguration<TestOrder>
{
    public void Configure(EntityTypeBuilder<TestOrder> builder)
    {
        builder.HasKey(t => t.Id);

        // Never let deleting/reconfiguring a Room cascade into deleting
        // TestOrder history - same reasoning as the Media/Material FKs.
        builder.HasOne(t => t.Room).WithMany().HasForeignKey(t => t.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}
