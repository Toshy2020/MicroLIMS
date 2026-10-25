using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SpecificationGradeTests
{
    [Theory]
    [InlineData(" bp ", "BP")]
    [InlineData("usp", "USP")]
    [InlineData("In-house", "In-house")]
    [InlineData("  In   house ", "In house")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void NormalizeGrade_TrimsAndUpperCasesKnownGrades(string? input, string? expected) =>
        Assert.Equal(expected, SpecificationService.NormalizeGrade(input));

    [Fact]
    public void NormalizeGrade_RejectsMoreThan20Characters()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SpecificationService.NormalizeGrade(new string('x', 21)));
        Assert.Equal("Grade must be 20 characters or fewer.", ex.Message);
    }

    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.SampleTests.Add(new SampleTest { ItemId = 1, TestCode = "ID" });
        db.SaveChanges();
        return db;
    }

    private static Specification Spec(string? grade) => new()
    {
        ItemId = 1, TestCode = "ID", ParameterName = "Identification",
        LimitType = LimitType.Qualitative, ExpectedResultText = "Conforms", Grade = grade,
    };

    [Fact]
    public async Task ValidateAsync_RejectsSameParameterAndGradeIgnoringCase()
    {
        using var db = NewDb();
        var service = new SpecificationService(db);
        db.Specifications.Add(Spec("BP"));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(Spec("bp")));
        Assert.Equal("A specification for this parameter and grade already exists.", ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_AcceptsSameParameterWithAnotherGrade()
    {
        using var db = NewDb();
        var service = new SpecificationService(db);
        db.Specifications.Add(Spec("BP"));
        await db.SaveChangesAsync();

        await service.ValidateAsync(Spec("USP"));
        await service.ValidateAsync(Spec(null));
    }

    [Fact]
    public async Task ValidateAsync_UngradedDuplicateKeepsTheOriginalMessage()
    {
        using var db = NewDb();
        var service = new SpecificationService(db);
        db.Specifications.Add(Spec(null));
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(Spec(null)));
        Assert.Contains("already exists for test 'ID'", ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_ReusesTheItemsExistingSpellingOfAGrade()
    {
        using var db = NewDb();
        db.SampleTests.Add(new SampleTest { ItemId = 1, TestCode = "LOD" });
        var assay = Spec("In-house");
        assay.ParameterName = "Assay";
        db.Specifications.Add(assay);
        await db.SaveChangesAsync();

        var lod = Spec("in-house");
        lod.TestCode = "LOD";
        await new SpecificationService(db).ValidateAsync(lod);

        Assert.Equal("In-house", lod.Grade);
    }

    [Fact]
    public async Task ValidateAsync_LetsTheOnlyRowOfAGradeChangeItsCase()
    {
        using var db = NewDb();
        var row = Spec("In-house");
        db.Specifications.Add(row);
        await db.SaveChangesAsync();

        row.Grade = "IN-HOUSE";
        await new SpecificationService(db).ValidateAsync(row);

        Assert.Equal("IN-HOUSE", row.Grade);
    }
}
