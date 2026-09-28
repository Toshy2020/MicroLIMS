using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;

namespace MicroLIMS.Application.Services;

// The laboratories a sample can be received for: which ones an item's
// tests belong to (the Receiving page's lab picker), and which of the
// requested ones this user may target (ReceiptLabGuard).
public class ReceiptLabService
{
    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;

    public ReceiptLabService(IMicroLimsDbContext db, IUserSectionScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public async Task<List<ReceiptLabOptionDto>> GetForItemAsync(int itemId)
    {
        var codes = await _db.Items.Where(i => i.Id == itemId).SelectMany(i => i.AssignedTests.Select(t => t.TestCode)).ToListAsync();
        return await _db.TestDefinitions.Where(t => codes.Contains(t.Code))
            .GroupBy(t => new { t.SectionId, t.Section!.Code, t.Section.Name })
            .Select(g => new ReceiptLabOptionDto(g.Key.SectionId, g.Key.Code, g.Key.Name, g.Count()))
            .ToListAsync();
    }

    public Task<IReadOnlyCollection<int>> ResolveTargetsAsync(int userId, bool canReceiveForAnyLab, IReadOnlyCollection<int>? requested) =>
        ReceiptLabGuard.ResolveTargetsAsync(_db, _scope, userId, canReceiveForAnyLab, requested);
}
