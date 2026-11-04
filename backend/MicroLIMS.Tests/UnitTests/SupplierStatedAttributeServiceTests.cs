using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SupplierStatedAttributeServiceTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        // Seed an item with specifications for "BP" and "USP"
        var item = new Item { Id = 1, Name = "Paracetamol", Code = "RM001" };
        db.Items.Add(item);

        db.Specifications.Add(new Specification
        {
            Id = 1,
            ItemId = 1,
            TestCode = "ID",
            ParameterName = "Identification",
            Grade = "BP",
            LimitType = LimitType.Qualitative,
            ExpectedResultText = "Conforms"
        });

        db.Specifications.Add(new Specification
        {
            Id = 2,
            ItemId = 1,
            TestCode = "ASSAY",
            ParameterName = "Assay",
            Grade = "USP",
            LimitType = LimitType.Range,
            LowerLimit = 98.0m,
            UpperLimit = 102.0m,
            Unit = "%"
        });

        db.SaveChanges();
        return db;
    }

    private static SupplierStatedAttributeService NewService(MicroLimsDbContext db) =>
        new(db, new AuditEventService(db, new DatabaseSequenceHelper(db)));

    [Fact]
    public async Task ForSample_ReturnsUngradedAndMatchingGradeOnly()
    {
        using var db = NewDb();
        var service = NewService(db);

        // Attributes for Item 1
        db.SupplierStatedAttributes.AddRange(
            new SupplierStatedAttribute { ItemId = 1, Grade = null, Name = "Appearance", StatedValue = "White powder", SortOrder = 1 },
            new SupplierStatedAttribute { ItemId = 1, Grade = "BP", Name = "Refractive Index", StatedValue = "1.332", SortOrder = 2 },
            new SupplierStatedAttribute { ItemId = 1, Grade = "USP", Name = "Loss on Drying", StatedValue = "< 0.5%", SortOrder = 2 },
            new SupplierStatedAttribute { ItemId = 1, Grade = "BP", Name = "Clarity of Solution", StatedValue = "Clear", SortOrder = 3 }
        );

        // Attribute for another item
        db.SupplierStatedAttributes.Add(
            new SupplierStatedAttribute { ItemId = 2, Grade = "BP", Name = "Melting Point", StatedValue = "150 C", SortOrder = 1 }
        );
        await db.SaveChangesAsync();

        // Querying for BP should return ungraded (null) + BP rows, sorted by SortOrder then Name
        var bpResults = await service.ForSampleAsync(1, "BP");
        Assert.Equal(3, bpResults.Count);
        Assert.Equal("Appearance", bpResults[0].Name);
        Assert.Equal("Refractive Index", bpResults[1].Name);
        Assert.Equal("Clarity of Solution", bpResults[2].Name);

        // Case-insensitive grade matching e.g. "bp"
        var bpLower = await service.ForSampleAsync(1, "bp");
        Assert.Equal(3, bpLower.Count);

        // Ungraded query (null grade) returns only ungraded rows
        var ungradedResults = await service.ForSampleAsync(1, null);
        var single = Assert.Single(ungradedResults);
        Assert.Equal("Appearance", single.Name);
    }

    [Fact]
    public async Task Create_RefusesUnknownGrade()
    {
        using var db = NewDb();
        var service = NewService(db);

        // Item 1 only has specs for BP and USP. EP is unknown.
        var request = new SupplierStatedAttributeRequest(
            ItemId: 1,
            Grade: "EP",
            Name: "Heavy Metals",
            StatedValue: "< 10 ppm",
            SortOrder: 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(userId: 1, request));
        Assert.Contains("Grade EP has no specification for this item.", ex.Message);
    }

    [Fact]
    public async Task Update_AuditsOldAndNewValue()
    {
        using var db = NewDb();
        var service = NewService(db);

        var attr = new SupplierStatedAttribute
        {
            ItemId = 1,
            Grade = "BP",
            Name = "Refractive Index",
            StatedValue = "1.332",
            SortOrder = 1
        };
        db.SupplierStatedAttributes.Add(attr);
        await db.SaveChangesAsync();

        var updateRequest = new SupplierStatedAttributeRequest(
            ItemId: 1,
            Grade: "BP",
            Name: "Refractive Index",
            StatedValue: "1.335",
            SortOrder: 1);

        var updated = await service.UpdateAsync(userId: 42, id: attr.Id, updateRequest);
        Assert.Equal("1.335", updated.StatedValue);

        var audit = Assert.Single(await db.AuditLogs
            .Include(a => a.Changes)
            .Where(a => a.ActionCode == SupplierStatedAttributeService.UpdatedActionCode && a.EntityId == attr.Id.ToString())
            .ToListAsync());

        Assert.Equal(AuditActionCategory.Configuration, audit.ActionCategory);
        var change = Assert.Single(audit.Changes);
        Assert.Equal("StatedValue", change.FieldName);
        Assert.Equal("1.332", change.PreviousValue);
        Assert.Equal("1.335", change.NewValue);
    }

    [Fact]
    public async Task Delete_IsAudited()
    {
        using var db = NewDb();
        var service = NewService(db);

        var attr = new SupplierStatedAttribute
        {
            ItemId = 1,
            Grade = "BP",
            Name = "Refractive Index",
            StatedValue = "1.332",
            SortOrder = 1
        };
        db.SupplierStatedAttributes.Add(attr);
        await db.SaveChangesAsync();

        await service.DeleteAsync(userId: 42, id: attr.Id);

        Assert.Empty(await db.SupplierStatedAttributes.Where(a => a.Id == attr.Id).ToListAsync());

        var audit = Assert.Single(await db.AuditLogs
            .Include(a => a.Changes)
            .Where(a => a.ActionCode == SupplierStatedAttributeService.DeletedActionCode && a.EntityId == attr.Id.ToString())
            .ToListAsync());

        Assert.Equal(AuditActionCategory.Configuration, audit.ActionCategory);
        Assert.Contains(audit.Changes, c => c.FieldName == "Name" && c.PreviousValue == "Refractive Index" && c.NewValue == null);
        Assert.Contains(audit.Changes, c => c.FieldName == "StatedValue" && c.PreviousValue == "1.332" && c.NewValue == null);
        Assert.Contains(audit.Changes, c => c.FieldName == "Grade" && c.PreviousValue == "BP" && c.NewValue == null);
        Assert.Contains(audit.Changes, c => c.FieldName == "SortOrder" && c.PreviousValue == "1" && c.NewValue == null);
    }

    [Fact]
    public async Task ListForItem_ReturnsAllAttributesSorted()
    {
        using var db = NewDb();
        var service = NewService(db);

        db.SupplierStatedAttributes.AddRange(
            new SupplierStatedAttribute { ItemId = 1, Grade = "BP", Name = "Zeta", StatedValue = "1", SortOrder = 2 },
            new SupplierStatedAttribute { ItemId = 1, Grade = null, Name = "Alpha", StatedValue = "2", SortOrder = 1 },
            new SupplierStatedAttribute { ItemId = 1, Grade = "USP", Name = "Beta", StatedValue = "3", SortOrder = 1 },
            new SupplierStatedAttribute { ItemId = 2, Grade = null, Name = "Gamma", StatedValue = "4", SortOrder = 1 }
        );
        await db.SaveChangesAsync();

        var list = await service.ListForItemAsync(1);
        Assert.Equal(3, list.Count);
        Assert.Equal("Alpha", list[0].Name);
        Assert.Equal("Beta", list[1].Name);
        Assert.Equal("Zeta", list[2].Name);
    }

    [Fact]
    public async Task Create_SucceedsAndAudits_WithNormalizedGrade()
    {
        using var db = NewDb();
        var service = NewService(db);

        var request = new SupplierStatedAttributeRequest(
            ItemId: 1,
            Grade: " bp ",
            Name: " Refractive Index ",
            StatedValue: " 1.332 ",
            SortOrder: 1);

        var created = await service.CreateAsync(userId: 10, request);

        Assert.Equal("BP", created.Grade);
        Assert.Equal("Refractive Index", created.Name);
        Assert.Equal("1.332", created.StatedValue);

        var audit = Assert.Single(await db.AuditLogs
            .Include(a => a.Changes)
            .Where(a => a.ActionCode == SupplierStatedAttributeService.CreatedActionCode && a.EntityId == created.Id.ToString())
            .ToListAsync());

        Assert.Equal(AuditActionCategory.Configuration, audit.ActionCategory);
        Assert.Equal(4, audit.Changes.Count);
    }

    [Fact]
    public async Task Create_FailsWhenItemNotFound()
    {
        using var db = NewDb();
        var service = NewService(db);

        var request = new SupplierStatedAttributeRequest(
            ItemId: 999,
            Grade: null,
            Name: "Test",
            StatedValue: "Value",
            SortOrder: 0);

        await Assert.ThrowsAsync<MicroLIMS.Shared.Exceptions.NotFoundException>(() => service.CreateAsync(userId: 1, request));
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("   ", "value")]
    [InlineData("name", "")]
    [InlineData("name", "   ")]
    public async Task Create_FailsWhenNameOrValueMissing(string name, string value)
    {
        using var db = NewDb();
        var service = NewService(db);

        var request = new SupplierStatedAttributeRequest(
            ItemId: 1,
            Grade: null,
            Name: name,
            StatedValue: value,
            SortOrder: 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(userId: 1, request));
    }

    [Theory]
    [InlineData(201, 10, "Name must be 200 characters or fewer.")]
    [InlineData(10, 501, "Stated value must be 500 characters or fewer.")]
    public async Task Create_RefusesOverlongNameAndValue(int nameLength, int valueLength, string expectedMessage)
    {
        using var db = NewDb();
        var service = NewService(db);

        var request = new SupplierStatedAttributeRequest(
            ItemId: 1,
            Grade: null,
            Name: new string('a', nameLength),
            StatedValue: new string('b', valueLength),
            SortOrder: 0);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(userId: 1, request));
        Assert.Equal(expectedMessage, ex.Message);
    }
}

[Collection("PostgresDatabaseCollection")]
public class SupplierStatedAttributePostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public SupplierStatedAttributePostgresTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(Item Item, int UserId)> SetupItemAsync(MicroLimsDbContext db)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var item = new Item { Name = $"Item {uid}", Code = $"RM-{uid}", SopNumber = "SOP-01" };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.Specifications.Add(new Specification
        {
            ItemId = item.Id,
            TestCode = "ID",
            ParameterName = "Identification",
            Grade = "BP",
            LimitType = LimitType.Qualitative,
            ExpectedResultText = "Conforms"
        });
        await db.SaveChangesAsync();

        return (item, _fixture.SeededUserId);
    }

    [PostgresFact]
    public async Task Create_WhenAuditWriteThrows_RollsBackAndNoRowRemains()
    {
        await using var db = _fixture.CreateDbContext();
        var (item, userId) = await SetupItemAsync(db);

        var service = new SupplierStatedAttributeService(db, new ThrowingAudit());
        var request = new SupplierStatedAttributeRequest(
            ItemId: item.Id,
            Grade: "BP",
            Name: "Refractive Index",
            StatedValue: "1.332",
            SortOrder: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(userId, request));

        await using var verifyDb = _fixture.CreateDbContext();
        Assert.False(await verifyDb.SupplierStatedAttributes.AnyAsync(a => a.ItemId == item.Id));
    }

    [PostgresFact]
    public async Task Update_WhenAuditWriteThrows_RollsBackAndKeepsOldValues()
    {
        await using var db = _fixture.CreateDbContext();
        var (item, userId) = await SetupItemAsync(db);

        var attr = new SupplierStatedAttribute
        {
            ItemId = item.Id,
            Grade = "BP",
            Name = "Refractive Index",
            StatedValue = "1.332",
            SortOrder = 1
        };
        db.SupplierStatedAttributes.Add(attr);
        await db.SaveChangesAsync();

        var service = new SupplierStatedAttributeService(db, new ThrowingAudit());
        var request = new SupplierStatedAttributeRequest(
            ItemId: item.Id,
            Grade: "BP",
            Name: "Updated Name",
            StatedValue: "9.999",
            SortOrder: 2);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(userId, attr.Id, request));

        await using var verifyDb = _fixture.CreateDbContext();
        var reloaded = await verifyDb.SupplierStatedAttributes.SingleAsync(a => a.Id == attr.Id);
        Assert.Equal("Refractive Index", reloaded.Name);
        Assert.Equal("1.332", reloaded.StatedValue);
        Assert.Equal(1, reloaded.SortOrder);
    }

    [PostgresFact]
    public async Task Delete_WhenAuditWriteThrows_RollsBackAndRowStillExists()
    {
        await using var db = _fixture.CreateDbContext();
        var (item, userId) = await SetupItemAsync(db);

        var attr = new SupplierStatedAttribute
        {
            ItemId = item.Id,
            Grade = "BP",
            Name = "Refractive Index",
            StatedValue = "1.332",
            SortOrder = 1
        };
        db.SupplierStatedAttributes.Add(attr);
        await db.SaveChangesAsync();

        var service = new SupplierStatedAttributeService(db, new ThrowingAudit());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(userId, attr.Id));

        await using var verifyDb = _fixture.CreateDbContext();
        Assert.True(await verifyDb.SupplierStatedAttributes.AnyAsync(a => a.Id == attr.Id));
    }

    private sealed class ThrowingAudit : IAuditEventService
    {
        public Task<AuditLog> RecordUserEventAsync(
            string actionCode,
            AuditActionCategory actionCategory,
            string recordType,
            int? documentMasterId = null,
            int? documentRevisionId = null,
            string? reason = null,
            Guid? correlationId = null,
            string? sourceContext = null,
            IEnumerable<AuditFieldChange>? changes = null,
            string? entityId = null,
            CancellationToken cancellationToken = default,
            int? sampleId = null) =>
            throw new InvalidOperationException("Simulated audit write failure.");

        public Task<AuditLog> RecordSystemEventAsync(
            string systemProcessName,
            string actionCode,
            AuditActionCategory actionCategory,
            string recordType,
            int? documentMasterId = null,
            int? documentRevisionId = null,
            string? reason = null,
            Guid? correlationId = null,
            string? sourceContext = null,
            IEnumerable<AuditFieldChange>? changes = null,
            string? entityId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
