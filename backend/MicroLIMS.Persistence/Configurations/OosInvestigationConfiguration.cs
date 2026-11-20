using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Persistence.Configurations;

public class OosInvestigationConfiguration : IEntityTypeConfiguration<OosInvestigation>
{
    public void Configure(EntityTypeBuilder<OosInvestigation> builder)
    {
        builder.Property(x => x.Number).HasMaxLength(20);
        builder.Property(x => x.TemplateSnapshotJson).HasColumnType("jsonb");
        builder.Property(x => x.PhaseOneCauseDescription).HasMaxLength(4000);
        builder.Property(x => x.RootCauseDescription).HasMaxLength(4000);
        builder.Property(x => x.ConclusionSummary).HasMaxLength(4000);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => new { x.SampleId, x.SectionId, x.Kind }).IsUnique()
            .HasFilter($"\"Status\" <> {(int)OosInvestigationStatus.Concluded}");

        builder.HasOne<Sample>().WithMany().HasForeignKey(x => x.SampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentSection>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OpenedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OosCauseCategory>().WithMany().HasForeignKey(x => x.PhaseOneCauseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.PhaseOneSignedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(x => x.PhaseOneSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OosCauseCategory>().WithMany().HasForeignKey(x => x.RootCauseCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ConcludedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(x => x.ConclusionSignatureId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosInvestigationResultConfiguration : IEntityTypeConfiguration<OosInvestigationResult>
{
    public void Configure(EntityTypeBuilder<OosInvestigationResult> builder)
    {
        builder.Property(x => x.OutcomeAtOpening).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ResultSummaryAtOpening).HasMaxLength(500);
        builder.HasIndex(x => new { x.InvestigationId, x.TestOrderId }).IsUnique();

        builder.HasOne<OosInvestigation>().WithMany(i => i.Results).HasForeignKey(x => x.InvestigationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TestOrder>().WithMany().HasForeignKey(x => x.TestOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosInvestigationEntryConfiguration : IEntityTypeConfiguration<OosInvestigationEntry>
{
    public void Configure(EntityTypeBuilder<OosInvestigationEntry> builder)
    {
        builder.ToTable("OosInvestigationEntries", table =>
            table.HasCheckConstraint("CK_OosInvestigationEntries_Phase", "\"Phase\" IN (1, 2)"));
        builder.Property(x => x.ChecklistItemKey).HasMaxLength(36);
        builder.Property(x => x.Text).HasMaxLength(4000);
        builder.ToTable(table => table.HasCheckConstraint("CK_OosInvestigationEntries_ChecklistComment",
            $"\"Kind\" <> {(int)OosEntryKind.ChecklistAnswer} OR char_length(\"Text\") <= 1000"));

        builder.HasOne<OosInvestigation>().WithMany().HasForeignKey(x => x.InvestigationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OosInvestigationEntry>().WithMany().HasForeignKey(x => x.ParentEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosRetestPlanConfiguration : IEntityTypeConfiguration<OosRetestPlan>
{
    public void Configure(EntityTypeBuilder<OosRetestPlan> builder)
    {
        builder.Property(x => x.TestOrderIdsJson).HasColumnType("jsonb");
        builder.Property(x => x.AnalystUserIdsJson).HasColumnType("jsonb");
        builder.Property(x => x.JudgingRule).HasMaxLength(4000);
        builder.Property(x => x.AmendmentReason).HasMaxLength(4000);
        builder.HasIndex(x => new { x.InvestigationId, x.Revision }).IsUnique();

        builder.HasOne<OosInvestigation>().WithMany().HasForeignKey(x => x.InvestigationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.SignedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(x => x.SignatureId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosRetestPlanSampleConfiguration : IEntityTypeConfiguration<OosRetestPlanSample>
{
    public void Configure(EntityTypeBuilder<OosRetestPlanSample> builder)
    {
        builder.HasKey(x => new { x.PlanId, x.SampleId });

        builder.HasOne<OosRetestPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sample>().WithMany().HasForeignKey(x => x.SampleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosDeviationRequestConfiguration : IEntityTypeConfiguration<OosDeviationRequest>
{
    public void Configure(EntityTypeBuilder<OosDeviationRequest> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(4000);
        builder.Property(x => x.AddressedComment).HasMaxLength(4000);

        builder.HasOne<OosInvestigation>().WithMany().HasForeignKey(x => x.InvestigationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AddressedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosInvestigationReopeningConfiguration : IEntityTypeConfiguration<OosInvestigationReopening>
{
    public void Configure(EntityTypeBuilder<OosInvestigationReopening> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(4000);

        builder.HasOne<OosInvestigation>().WithMany().HasForeignKey(x => x.InvestigationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(x => x.PreviousConclusionSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReopenedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(x => x.SignatureId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosInvestigationTemplateConfiguration : IEntityTypeConfiguration<OosInvestigationTemplate>
{
    public void Configure(EntityTypeBuilder<OosInvestigationTemplate> builder)
    {
        builder.Property(x => x.AlertOpensExcursionReview).HasDefaultValue(false);
        builder.HasIndex(x => new { x.SectionId, x.Kind }).IsUnique();

        builder.HasOne<DocumentSection>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosChecklistItemConfiguration : IEntityTypeConfiguration<OosChecklistItem>
{
    public void Configure(EntityTypeBuilder<OosChecklistItem> builder)
    {
        builder.ToTable("OosChecklistItems", table =>
            table.HasCheckConstraint("CK_OosChecklistItems_Phase", "\"Phase\" IN (1, 2)"));
        builder.Property(x => x.Key).HasMaxLength(36);
        builder.Property(x => x.Text).HasMaxLength(4000);
        builder.HasIndex(x => new { x.TemplateId, x.Key }).IsUnique();

        builder.HasOne<OosInvestigationTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OosCauseCategoryConfiguration : IEntityTypeConfiguration<OosCauseCategory>
{
    public void Configure(EntityTypeBuilder<OosCauseCategory> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => x.IsNoCauseFound).IsUnique().HasFilter("\"IsNoCauseFound\"");
        builder.ToTable(table => table.HasCheckConstraint("CK_OosCauseCategories_NoCauseFound",
            "NOT \"IsNoCauseFound\" OR (\"IsActive\" AND \"Name\" = 'No cause found')"));
        builder.HasData(
            new OosCauseCategory { Id = 1, Name = "Analyst error", IsActive = true },
            new OosCauseCategory { Id = 2, Name = "Equipment", IsActive = true },
            new OosCauseCategory { Id = 3, Name = "Method", IsActive = true },
            new OosCauseCategory { Id = 4, Name = "Sample", IsActive = true },
            new OosCauseCategory { Id = 5, Name = "Media or reagent", IsActive = true },
            new OosCauseCategory { Id = 6, Name = "Environment", IsActive = true },
            new OosCauseCategory { Id = 7, Name = "No cause found", IsActive = true, IsNoCauseFound = true });
    }
}
