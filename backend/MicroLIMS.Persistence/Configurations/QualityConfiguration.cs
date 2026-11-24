using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Entities.Quality;

namespace MicroLIMS.Persistence.Configurations;

public class QualityRecordConfiguration : IEntityTypeConfiguration<QualityRecord>
{
    public void Configure(EntityTypeBuilder<QualityRecord> builder)
    {
        builder.Property(x => x.Number).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RaisedBySystemProcess).HasMaxLength(200);
        builder.Property(x => x.CancelReason).HasMaxLength(4000);

        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => new { x.Type, x.RaisedAtUtc });
        builder.HasIndex(x => x.SectionId);
        builder.HasIndex(x => x.OwnerUserId);

        builder.HasOne<DocumentSection>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QualityClassification>().WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DeviationDetail).WithOne().HasForeignKey<DeviationDetail>(d => d.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CapaDetail).WithOne().HasForeignKey<CapaDetail>(d => d.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChangeControlDetail).WithOne().HasForeignKey<ChangeControlDetail>(d => d.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ComplaintDetail).WithOne().HasForeignKey<ComplaintDetail>(d => d.RecordId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Steps).WithOne().HasForeignKey(s => s.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Actions).WithOne().HasForeignKey(a => a.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Links).WithOne().HasForeignKey(l => l.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Attachments).WithOne().HasForeignKey(a => a.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Comments).WithOne().HasForeignKey(c => c.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.ExtensionRequests).WithOne().HasForeignKey(e => e.RecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.ChecklistAnswers).WithOne().HasForeignKey(a => a.RecordId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DeviationDetailConfiguration : IEntityTypeConfiguration<DeviationDetail>
{
    public void Configure(EntityTypeBuilder<DeviationDetail> builder)
    {
        builder.HasKey(x => x.RecordId);
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.Property(x => x.ImmediateAction).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ImpactAssessment).HasMaxLength(4000);
        builder.Property(x => x.RootCauseDescription).HasMaxLength(4000);
        builder.Property(x => x.ProposedActions).HasMaxLength(4000);
        builder.Property(x => x.RejectionReason).HasMaxLength(4000);

        builder.HasOne<OosCauseCategory>().WithMany().HasForeignKey(x => x.RootCauseCategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CapaDetailConfiguration : IEntityTypeConfiguration<CapaDetail>
{
    public void Configure(EntityTypeBuilder<CapaDetail> builder)
    {
        builder.HasKey(x => x.RecordId);
        builder.Property(x => x.EffectivenessCriteria).HasMaxLength(4000);
        builder.Property(x => x.EffectivenessMethod).HasMaxLength(4000);
        builder.Property(x => x.EffectivenessFindings).HasMaxLength(4000);

        builder.HasOne<QualityRecord>().WithMany().HasForeignKey(x => x.SourceRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QualityRecord>().WithMany().HasForeignKey(x => x.SuccessorCapaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChangeControlDetailConfiguration : IEntityTypeConfiguration<ChangeControlDetail>
{
    public void Configure(EntityTypeBuilder<ChangeControlDetail> builder)
    {
        builder.HasKey(x => x.RecordId);
        builder.Property(x => x.Reason).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ImpactSummary).HasMaxLength(4000);
        builder.Property(x => x.RejectionReason).HasMaxLength(4000);
        builder.Property(x => x.PostChangeReview).HasMaxLength(4000);
        builder.Property(x => x.UnchangedJustification).HasMaxLength(4000);
    }
}

public class ComplaintDetailConfiguration : IEntityTypeConfiguration<ComplaintDetail>
{
    public void Configure(EntityTypeBuilder<ComplaintDetail> builder)
    {
        builder.HasKey(x => x.RecordId);
        builder.Property(x => x.Source).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ExternalReference).HasMaxLength(200);
        builder.Property(x => x.LotText).HasMaxLength(200);
        builder.Property(x => x.ComplaintText).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.WhatToTest).HasMaxLength(4000);
        builder.Property(x => x.Findings).HasMaxLength(4000);
        builder.Property(x => x.ConclusionSummary).HasMaxLength(4000);

        builder.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityRecordStepConfiguration : IEntityTypeConfiguration<QualityRecordStep>
{
    public void Configure(EntityTypeBuilder<QualityRecordStep> builder)
    {
        builder.Property(x => x.Comment).HasMaxLength(4000);
        builder.Property(x => x.SnapshotJson).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(x => new { x.RecordId, x.SignedAtUtc });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.SignedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(x => x.SignatureId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityActionConfiguration : IEntityTypeConfiguration<QualityAction>
{
    public void Configure(EntityTypeBuilder<QualityAction> builder)
    {
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Evidence).HasMaxLength(4000);

        builder.HasIndex(x => new { x.RecordId, x.DueDate });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.DoneByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QualityRecordLink>().WithMany().HasForeignKey(x => x.EvidenceLinkId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityRecordLinkConfiguration : IEntityTypeConfiguration<QualityRecordLink>
{
    public void Configure(EntityTypeBuilder<QualityRecordLink> builder)
    {
        builder.Property(x => x.NewRecordCode).HasMaxLength(100);
        builder.HasIndex(x => new { x.RecordId, x.TargetType, x.TargetId, x.NewRecordCode })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(x => new { x.TargetType, x.TargetId });

        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityAttachmentConfiguration : IEntityTypeConfiguration<QualityAttachment>
{
    public void Configure(EntityTypeBuilder<QualityAttachment> builder)
    {
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FileExtension).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ContentSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.VoidReason).HasMaxLength(4000);

        builder.HasIndex(x => x.RecordId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityCommentConfiguration : IEntityTypeConfiguration<QualityComment>
{
    public void Configure(EntityTypeBuilder<QualityComment> builder)
    {
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => x.RecordId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityExtensionRequestConfiguration : IEntityTypeConfiguration<QualityExtensionRequest>
{
    public void Configure(EntityTypeBuilder<QualityExtensionRequest> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.DecisionComment).HasMaxLength(4000);

        builder.HasIndex(x => x.RecordId);
        builder.HasOne<QualityAction>().WithMany().HasForeignKey(x => x.ActionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(x => x.SignatureId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityChecklistAnswerConfiguration : IEntityTypeConfiguration<QualityChecklistAnswer>
{
    public void Configure(EntityTypeBuilder<QualityChecklistAnswer> builder)
    {
        builder.Property(x => x.ItemKey).HasMaxLength(36).IsRequired();
        builder.Property(x => x.ItemText).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(2000);

        builder.HasIndex(x => new { x.RecordId, x.ItemKey, x.AnsweredAtUtc });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.AnsweredByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityChecklistTemplateConfiguration : IEntityTypeConfiguration<QualityChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<QualityChecklistTemplate> builder)
    {
        builder.HasIndex(x => new { x.Type, x.SectionId }).IsUnique().AreNullsDistinct(false);
        builder.HasOne<DocumentSection>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualityChecklistItemConfiguration : IEntityTypeConfiguration<QualityChecklistItem>
{
    public void Configure(EntityTypeBuilder<QualityChecklistItem> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(36).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.TemplateId, x.Key }).IsUnique();
    }
}

public class QualityClassificationConfiguration : IEntityTypeConfiguration<QualityClassification>
{
    public void Configure(EntityTypeBuilder<QualityClassification> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasData(
            new QualityClassification { Id = 1, Name = "Minor", Order = 1, IsActive = true },
            new QualityClassification { Id = 2, Name = "Major", Order = 2, IsActive = true },
            new QualityClassification { Id = 3, Name = "Critical", Order = 3, IsActive = true }
        );
    }
}

public class QualityDueRuleConfiguration : IEntityTypeConfiguration<QualityDueRule>
{
    public void Configure(EntityTypeBuilder<QualityDueRule> builder)
    {
        builder.HasIndex(x => new { x.Type, x.ClassificationId }).IsUnique().AreNullsDistinct(false);
        builder.HasOne<QualityClassification>().WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QualitySectionSettingConfiguration : IEntityTypeConfiguration<QualitySectionSetting>
{
    public void Configure(EntityTypeBuilder<QualitySectionSetting> builder)
    {
        builder.Property(x => x.ChangeControlRequired).HasDefaultValue(false);
        builder.HasIndex(x => x.SectionId).IsUnique();
        builder.HasOne<DocumentSection>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
