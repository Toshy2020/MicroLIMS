using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class DocumentControlRelease1cDomainUnitTests
{
    [Fact]
    public void DocumentTrainingAssignment_ConstructsWithValidDefaults()
    {
        var assignment = new DocumentTrainingAssignment
        {
            DocumentMasterId = 10,
            DocumentRevisionId = 20,
            AssignedUserId = 5,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = 1
        };

        Assert.Equal(AssignmentType.Reading, assignment.AssignmentType);
        Assert.Equal(TrainingAssignmentStatus.Assigned, assignment.Status);
        Assert.True(assignment.AssignedDateUtc <= DateTime.UtcNow);
        Assert.True(assignment.CreatedAtUtc <= DateTime.UtcNow);
        Assert.Null(assignment.AcknowledgedAtUtc);
        Assert.Null(assignment.StatementText);
        Assert.Null(assignment.CompletedAtUtc);
        Assert.Null(assignment.SupersededAtUtc);
        Assert.Null(assignment.SourceAssignmentId);
    }

    [Fact]
    public void DocumentTrainingAssignment_SupportsRetrainingLinkage()
    {
        var sourceAssignment = new DocumentTrainingAssignment
        {
            Id = 101,
            DocumentMasterId = 10,
            DocumentRevisionId = 20, // Rev 1
            AssignedUserId = 5,
            Status = TrainingAssignmentStatus.CompletedPassed,
            CompletedAtUtc = DateTime.UtcNow.AddMonths(-1),
            CreatedByUserId = 1
        };

        var retrainedAssignment = new DocumentTrainingAssignment
        {
            Id = 102,
            DocumentMasterId = 10,
            DocumentRevisionId = 21, // Rev 2
            AssignedUserId = 5,
            Status = TrainingAssignmentStatus.Assigned,
            SourceAssignmentId = sourceAssignment.Id,
            SourceAssignment = sourceAssignment,
            AssignmentReason = "Retraining cascade on Rev 2 effective",
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = 1
        };

        Assert.Equal(101, retrainedAssignment.SourceAssignmentId);
        Assert.Equal(sourceAssignment, retrainedAssignment.SourceAssignment);
        Assert.Equal(21, retrainedAssignment.DocumentRevisionId);
        Assert.Equal(TrainingAssignmentStatus.CompletedPassed, retrainedAssignment.SourceAssignment.Status);
    }

    [Fact]
    public void DocumentTrainingAssignment_TerminalSupersededIncomplete_PreservesEvidence()
    {
        var assignment = new DocumentTrainingAssignment
        {
            Id = 201,
            DocumentMasterId = 10,
            DocumentRevisionId = 20,
            AssignedUserId = 5,
            Status = TrainingAssignmentStatus.Assigned,
            DueDateUtc = DateTime.UtcNow.AddDays(7),
            CreatedByUserId = 1
        };

        // Transition to SupersededIncomplete upon new revision becoming effective
        assignment.Status = TrainingAssignmentStatus.SupersededIncomplete;
        assignment.ClosedReason = "Superseded by Revision 02";
        assignment.SupersededAtUtc = DateTime.UtcNow;

        Assert.Equal(TrainingAssignmentStatus.SupersededIncomplete, assignment.Status);
        Assert.NotNull(assignment.SupersededAtUtc);
        Assert.Equal("Superseded by Revision 02", assignment.ClosedReason);
    }

    [Fact]
    public void DocumentTrainingAssignment_Acknowledgement_PreservesStatementAndUser()
    {
        var assignment = new DocumentTrainingAssignment
        {
            Id = 301,
            DocumentMasterId = 10,
            DocumentRevisionId = 20,
            AssignedUserId = 5,
            Status = TrainingAssignmentStatus.Assigned,
            DueDateUtc = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = 1
        };

        const string legalStatement = "I confirm that I have read, understood, and agree to adhere to the contents of this controlled document revision.";
        var ackTime = DateTime.UtcNow;

        assignment.Status = TrainingAssignmentStatus.Acknowledged;
        assignment.AcknowledgedAtUtc = ackTime;
        assignment.AcknowledgedByUserId = 5;
        assignment.StatementText = legalStatement;
        assignment.CompletedAtUtc = ackTime;

        Assert.Equal(TrainingAssignmentStatus.Acknowledged, assignment.Status);
        Assert.Equal(5, assignment.AcknowledgedByUserId);
        Assert.Equal(legalStatement, assignment.StatementText);
        Assert.Equal(ackTime, assignment.AcknowledgedAtUtc);
        Assert.Equal(ackTime, assignment.CompletedAtUtc);
    }

    [Fact]
    public void DocumentTrainingConfiguration_ConstructsWithRegulatoryDefaults()
    {
        var config = new DocumentTrainingConfiguration
        {
            DocumentTypeId = 2,
            ModifiedByUserId = 1
        };

        Assert.True(config.RequiresReading);
        Assert.True(config.RequiresRetrainingOnRevision);
        Assert.Equal(14, config.DefaultGracePeriodDays);
        Assert.Equal(3, config.EscalationDaysBeforeDue);
        Assert.Equal(1, config.EscalationDaysAfterDue);
        Assert.Contains("I confirm that I have read, understood, and agree", config.DefaultAcknowledgementStatement);
    }

    [Fact]
    public void DocumentRoleCurriculum_ConstructsWithItems()
    {
        var curriculum = new DocumentRoleCurriculum
        {
            Id = 1,
            RoleId = 4, // Analyst
            Name = "Microbiology Analyst Core Curriculum",
            Description = "Mandatory reading for all QC microbiology laboratory analysts",
            IsActive = true,
            CreatedByUserId = 1
        };

        var item1 = new DocumentRoleCurriculumItem
        {
            DocumentRoleCurriculumId = 1,
            DocumentMasterId = 10,
            IsMandatory = true,
            CustomGracePeriodDays = 7
        };

        var item2 = new DocumentRoleCurriculumItem
        {
            DocumentRoleCurriculumId = 1,
            DocumentMasterId = 11,
            IsMandatory = true,
            CustomGracePeriodDays = null // inherits default
        };

        curriculum.Items.Add(item1);
        curriculum.Items.Add(item2);

        Assert.Equal(2, curriculum.Items.Count);
        Assert.All(curriculum.Items, i => Assert.True(i.IsMandatory));
        Assert.Equal(7, curriculum.Items.First().CustomGracePeriodDays);
        Assert.Null(curriculum.Items.Last().CustomGracePeriodDays);
    }

    [Theory]
    [InlineData(TrainingAssignmentStatus.NotAssigned)]
    [InlineData(TrainingAssignmentStatus.Assigned)]
    [InlineData(TrainingAssignmentStatus.Reading)]
    [InlineData(TrainingAssignmentStatus.Acknowledged)]
    [InlineData(TrainingAssignmentStatus.KafPending)]
    [InlineData(TrainingAssignmentStatus.CompletedPassed)]
    [InlineData(TrainingAssignmentStatus.Failed)]
    [InlineData(TrainingAssignmentStatus.Overdue)]
    [InlineData(TrainingAssignmentStatus.SupersededIncomplete)]
    [InlineData(TrainingAssignmentStatus.TrainedOnSupersededOnly)]
    [InlineData(TrainingAssignmentStatus.Cancelled)]
    public void TrainingAssignmentStatus_SupportsAllPlannedStates(TrainingAssignmentStatus status)
    {
        Assert.True(Enum.IsDefined(typeof(TrainingAssignmentStatus), status));
    }

    [Theory]
    [InlineData(AssignmentType.Reading)]
    [InlineData(AssignmentType.Training)]
    public void AssignmentType_SupportsPlannedTypes(AssignmentType type)
    {
        Assert.True(Enum.IsDefined(typeof(AssignmentType), type));
    }
}
