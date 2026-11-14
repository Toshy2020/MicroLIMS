using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class TableViewConfiguration : IEntityTypeConfiguration<TableView>
{
    public void Configure(EntityTypeBuilder<TableView> builder)
    {
        builder.ToTable("TableViews", t =>
            t.HasCheckConstraint("CK_TableViews_ScopeSection", "(\"Scope\" = 1) = (\"SectionId\" IS NOT NULL)"));

        builder.HasKey(v => v.Id);
        builder.Property(v => v.TableKey).IsRequired().HasMaxLength(100);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(60);
        builder.Property(v => v.StateJson).IsRequired().HasColumnType("jsonb");

        builder.HasOne(v => v.Owner).WithMany().HasForeignKey(v => v.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.Section).WithMany().HasForeignKey(v => v.SectionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.OwnerUserId, v.TableKey, v.Name })
            .IsUnique()
            .HasFilter("\"Scope\" = 0")
            .HasDatabaseName("IX_TableViews_Personal_Name");
        builder.HasIndex(v => new { v.SectionId, v.TableKey, v.Name })
            .IsUnique()
            .HasFilter("\"Scope\" = 1")
            .HasDatabaseName("IX_TableViews_Section_Name");
        builder.HasIndex(v => new { v.TableKey, v.OwnerUserId });
    }
}

public class UserTableDefaultConfiguration : IEntityTypeConfiguration<UserTableDefault>
{
    public void Configure(EntityTypeBuilder<UserTableDefault> builder)
    {
        builder.ToTable("UserTableDefaults");

        builder.HasKey(d => new { d.UserId, d.TableKey });
        builder.Property(d => d.TableKey).IsRequired().HasMaxLength(100);

        builder.HasOne(d => d.User).WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.TableView).WithMany().HasForeignKey(d => d.TableViewId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => d.TableViewId);
    }
}
