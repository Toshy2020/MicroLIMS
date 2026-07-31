using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class SpecificationService
{
    private readonly MicroLimsDbContext _db;

    public SpecificationService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<Specification>> GetForItemAsync(int itemId) =>
        await _db.Specifications.Where(s => s.ItemId == itemId).ToListAsync();

    public async Task<Specification> CreateAsync(Specification spec)
    {
        _db.Specifications.Add(spec);
        await _db.SaveChangesAsync();
        return spec;
    }

    // Backend performs alert/action/spec comparison - frontend only displays results.
    public string CompareAgainstLimits(decimal value, Specification spec)
    {
        if (decimal.TryParse(spec.SpecLimit, out var specLimit) && value > specLimit) return "OutOfSpecification";
        if (decimal.TryParse(spec.ActionLimit, out var actionLimit) && value > actionLimit) return "ActionLimitExceeded";
        if (decimal.TryParse(spec.AlertLimit, out var alertLimit) && value > alertLimit) return "AlertLimitExceeded";
        return "WithinLimits";
    }
}
