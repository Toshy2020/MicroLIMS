using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class ResultService : IResultService
{
    private readonly MicroLimsDbContext _db;

    public ResultService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<ResultDto> SaveResultAsync(int testOrderId, string rawValue, int enteredByUserId)
    {
        var order = await _db.TestOrders.FirstOrDefaultAsync(t => t.Id == testOrderId)
            ?? throw new InvalidOperationException($"Test order {testOrderId} not found.");

        var sample = await _db.Samples.FirstOrDefaultAsync(s => s.Id == order.SampleId);
        if (sample is not null && sample.Category is SampleCategory.FinishedProduct or SampleCategory.RawMaterial or SampleCategory.PackagingMaterial or SampleCategory.Water)
        {
            var prepared = await _db.SamplePreparations.AnyAsync(p => p.SampleId == sample.Id);
            if (!prepared)
                throw new InvalidOperationException("Test Preparation must be completed for this sample before results can be entered.");
        }

        var result = new Result
        {
            TestOrderId = testOrderId,
            RawValue = rawValue,
            EnteredByUserId = enteredByUserId,
            Type = ResultType.Numeric
        };

        _db.Results.Add(result);
        order.Status = ApprovalStatus.ResultEntered;
        await _db.SaveChangesAsync();

        return new ResultDto { ResultId = result.Id, TestOrderId = testOrderId, RawValue = rawValue, EnteredAt = result.EnteredAt };
    }

    public async Task<List<ResultDto>> GetResultsForTestOrderAsync(int testOrderId)
    {
        var results = await _db.Results.Where(r => r.TestOrderId == testOrderId).ToListAsync();
        return results.Select(r => new ResultDto
        {
            ResultId = r.Id,
            TestOrderId = r.TestOrderId,
            RawValue = r.RawValue,
            InterpretedValue = r.InterpretedValue,
            EnteredAt = r.EnteredAt
        }).ToList();
    }
}
