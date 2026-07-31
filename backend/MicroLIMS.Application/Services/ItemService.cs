using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Section Head owns this - the Master Configuration that the Workflow
// Engine reads on every sample receipt (Frozen Principle #1).
public class ItemService
{
    private readonly MicroLimsDbContext _db;

    public ItemService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<Item>> GetAllAsync() =>
        await _db.Items.Include(i => i.AssignedTests).Include(i => i.Specifications).ToListAsync();

    public async Task<Item> CreateAsync(Item item)
    {
        _db.Items.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(Item item)
    {
        _db.Items.Update(item);
        await _db.SaveChangesAsync();
    }
}
