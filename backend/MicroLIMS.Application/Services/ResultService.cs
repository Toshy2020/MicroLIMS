using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class ResultService : IResultService
{
    private readonly MicroLimsDbContext _db;

    public ResultService(MicroLimsDbContext db)
    {
        _db = db;
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
