using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("ErrorLogs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExceptionType)
            .IsRequired()
            .HasMaxLength(300);

        // Unbounded: frontend errors in particular arrive with long
        // messages, and truncating loses the only diagnostic we have.
        builder.Property(e => e.Message)
            .IsRequired();

        builder.Property(e => e.RequestPath)
            .HasMaxLength(500);

        builder.Property(e => e.HttpMethod)
            .HasMaxLength(10);

        builder.Property(e => e.CorrelationId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.RawContext)
            .HasColumnType("jsonb");

        // Child rows have no meaning without their parent incident.
        builder.HasOne(e => e.Incident)
            .WithMany(i => i.ErrorLogs)
            .HasForeignKey(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        // The acting user is informational - keep the error entry when
        // the user is deleted rather than cascading the history away.
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.OccurredAtUtc);
    }
}
