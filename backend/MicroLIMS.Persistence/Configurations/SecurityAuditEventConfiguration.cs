using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.TargetUsername).HasMaxLength(256);
        builder.Property(e => e.CorrelationId).HasMaxLength(100);
        builder.Property(e => e.IpAddress).HasMaxLength(100);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.RequestPath).HasMaxLength(500);

        builder.Property(e => e.Source)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Reason).HasMaxLength(1000);

        builder.Property(e => e.Metadata).HasColumnType("jsonb");

        // Security evidence must outlive the accounts it describes:
        // deleting a user must never silently erase the record of what
        // was done to that user, or by them.
        builder.HasOne(e => e.Actor)
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Target)
            .WithMany()
            .HasForeignKey(e => e.TargetUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // The three questions a security review actually asks: what
        // happened recently, what happened to this account, and what
        // else happened during this request.
        builder.HasIndex(e => e.OccurredAtUtc);
        builder.HasIndex(e => new { e.EventCode, e.OccurredAtUtc });
        builder.HasIndex(e => new { e.TargetUserId, e.OccurredAtUtc });
        builder.HasIndex(e => e.CorrelationId);
    }
}
