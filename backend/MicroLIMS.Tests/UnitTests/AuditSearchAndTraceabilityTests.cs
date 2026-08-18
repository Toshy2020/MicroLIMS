using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class AuditSearchAndTraceabilityTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        // Seed Role & User
        var role = new Role { Id = 1, Name = "Microbiology Analyst", Type = RoleType.Analyst };
        db.Roles.Add(role);

        var user = new User
        {
            Id = 5,
            FullName = "Ahmed Hassan",
            Username = "ahmed.hassan",
            PasswordHash = "hash",
            RoleId = 1,
            Role = role,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();

        return db;
    }

    [Fact]
    public async Task AuditSearch_EnrichesUserIdentity_Correctly()
    {
        await using var db = NewDb();
        var searchService = new AuditSearchService(db);

        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "Sample",
            EntityId = "10",
            Action = "Create",
            NewValue = "{\"ReferenceNumber\":\"FP0107026\"}",
            UserId = 5,
            SampleReferenceNumber = "FP0107026",
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var results = await searchService.SearchAsync(new AuditSearchRequest(
            null, null, null, null, null, "FP0107026", null, null, null, null, null, null, null));

        Assert.Single(results);
        var entry = results[0];
        Assert.Equal("Ahmed Hassan", entry.UserName);
        Assert.Equal("Microbiology Analyst", entry.UserRole);
        Assert.Equal("ahmed.hassan", entry.UserUsername);
        Assert.Equal(5, entry.UserId);
        Assert.Equal("FP0107026", entry.SampleReferenceNumber);
    }

    [Fact]
    public async Task AuditService_GetForEntity_EnrichesUserIdentity()
    {
        await using var db = NewDb();
        var auditService = new AuditService(db);

        db.AuditLogs.Add(new AuditLog
        {
            EntityName = "EquipmentInventory",
            EntityId = "3",
            Action = "Update",
            PreviousValue = "{\"Status\":0}",
            NewValue = "{\"Status\":1}",
            UserId = 5,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var history = await auditService.GetForEntityAsync("EquipmentInventory", "3");

        Assert.Single(history);
        Assert.Equal("Ahmed Hassan", history[0].UserName);
        Assert.Equal("Microbiology Analyst", history[0].UserRole);
        Assert.Equal(5, history[0].UserId);
    }

    [Fact]
    public async Task Traceability_SampleTestingChain_ConstructsRealGraph()
    {
        await using var db = NewDb();
        var traceService = new AuditTraceabilityService(db);

        var item = new Item { Id = 1, Code = "ITM-001", Name = "Paracetamol 500mg Tablet", Category = SampleCategory.FinishedProduct };
        db.Items.Add(item);

        var sample = new Sample
        {
            Id = 101,
            ReferenceNumber = "FP-2026-00871",
            Category = SampleCategory.FinishedProduct,
            ItemId = 1,
            Item = item,
            ControlNumber = "CTRL-904",
            BatchNumber = "B-2026-088",
            Status = SampleStatus.InTesting,
            ReceivedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.Samples.Add(sample);

        var testOrder = new TestOrder
        {
            Id = 451,
            SampleId = 101,
            TestCode = "TAMC",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Incubating
        };
        db.TestOrders.Add(testOrder);

        var result = new Result
        {
            Id = 789,
            TestOrderId = 451,
            RawValue = "150",
            InterpretedValue = "150 CFU/g",
            Type = ResultType.Numeric,
            EnteredByUserId = 5,
            EnteredAt = DateTime.UtcNow.AddHours(-5)
        };
        db.Results.Add(result);

        var auditLog = new AuditLog
        {
            EntityName = "Result",
            EntityId = "789",
            Action = "Update",
            UserId = 5,
            SampleId = 101,
            SampleReferenceNumber = "FP-2026-00871"
        };
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync();

        var trace = await traceService.GetTraceabilityAsync(auditLog.Id);

        Assert.NotNull(trace);
        Assert.Equal("SampleTesting", trace.PrimaryCategory);
        Assert.Equal("FP-2026-00871", trace.RootIdentifier);

        // Nodes in sequence: Item -> Sample -> TestOrder -> Result
        Assert.Contains(trace.Nodes, n => n.NodeType == "Item" && n.Identifier == "ITM-001");
        Assert.Contains(trace.Nodes, n => n.NodeType == "Sample" && n.Identifier == "FP-2026-00871");
        Assert.Contains(trace.Nodes, n => n.NodeType == "TestOrder" && n.Identifier == "TO-0451");
        Assert.Contains(trace.Nodes, n => n.NodeType == "Result" && n.Identifier == "RES-789");
    }

    [Fact]
    public async Task Traceability_EquipmentChain_ConstructsRealGraph()
    {
        await using var db = NewDb();
        var traceService = new AuditTraceabilityService(db);

        var eq = new EquipmentInventory
        {
            Id = 15,
            Code = "ATC-003",
            InstrumentType = "Autoclave",
            ManufacturerName = "Hirayama",
            SerialNumber = "SN-99812",
            Location = "Sterilization Room",
            Status = EquipmentOperationalStatus.InService,
            CalibrationDueDate = DateTime.UtcNow.AddMonths(8),
            CreatedByUserId = 5
        };
        db.EquipmentInventories.Add(eq);

        var auditLog = new AuditLog
        {
            EntityName = "EquipmentInventory",
            EntityId = "15",
            Action = "Update",
            UserId = 5
        };
        db.AuditLogs.Add(auditLog);
        await db.SaveChangesAsync();

        var trace = await traceService.GetTraceabilityAsync(auditLog.Id);

        Assert.NotNull(trace);
        Assert.Equal("EquipmentRegister", trace.PrimaryCategory);
        Assert.Equal("ATC-003", trace.RootIdentifier);
        Assert.Contains(trace.Nodes, n => n.NodeType == "Equipment" && n.Identifier == "ATC-003");
    }
}
