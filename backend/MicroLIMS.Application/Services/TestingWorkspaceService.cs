using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class TestingWorkspaceService : ITestWorkspaceService
{
    private readonly MicroLimsDbContext _db;

    public TestingWorkspaceService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<SampleDto>> GetActiveSamplesAsync()
    {
        var samples = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .OrderByDescending(s => s.ReceivedAt)
            .ToListAsync();

        return samples.Select(ToDto).ToList();
    }

    public async Task<SampleDto?> GetSampleAsync(int sampleId)
    {
        var sample = await _db.Samples
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .FirstOrDefaultAsync(s => s.Id == sampleId);
        return sample is null ? null : ToDto(sample);
    }

    public static SampleDto ToDto(Sample s) => new()
    {
        SampleId = s.Id,
        ReferenceNumber = s.ReferenceNumber,
        Category = s.Category.ToString(),
        DisplayName = s.Item?.Name ?? s.WaterSamplingPoint?.Code ?? s.Department?.Name ?? s.Machine?.Name ?? string.Empty,
        DepartmentId = s.DepartmentId,
        MachineId = s.MachineId,
        ProductionStage = s.ProductionStage,
        CauseOfTesting = s.CauseOfTesting?.Name ?? string.Empty,
        BatchNumber = s.BatchNumber,
        ControlNumber = s.ControlNumber,
        Status = s.Status.ToString(),
        PreparationStatus = s.PreparationStatus.ToString(),
        ReceivedAt = s.ReceivedAt,
        AssignedTests = s.TestOrders.Select(t => new TestOrderSummaryDto { TestOrderId = t.Id, TestCode = t.TestCode, Status = t.Status.ToString() }).ToList()
    };
}
