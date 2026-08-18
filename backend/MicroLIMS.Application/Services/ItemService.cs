using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Section Head owns this - the Master Configuration that the Workflow
// Engine reads on every sample receipt (Frozen Principle #1).
public class ItemService
{
    private readonly MicroLimsDbContext _db;

    private static readonly SampleCategory[] AllowedItemCategories =
    {
        SampleCategory.FinishedProduct,
        SampleCategory.RawMaterial,
        SampleCategory.PackagingMaterial,
    };

    public ItemService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<Item>> GetAllAsync() =>
        await _db.Items.Include(i => i.AssignedTests).Include(i => i.Specifications).ToListAsync();

    public async Task<Item> CreateAsync(Item item)
    {
        if (!AllowedItemCategories.Contains(item.Category))
        {
            throw new InvalidOperationException(
                "Items can only be configured for Product, Raw Material, or " +
                "Packaging Material categories. Water, Environmental Monitoring, " +
                "After Cleaning, and GPT are managed via their dedicated " +
                "configuration pages.");
        }

        if (await _db.Items.AnyAsync(i => i.Code == item.Code))
            throw new InvalidOperationException($"An item with code '{item.Code}' already exists.");

        _db.Items.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // Loads the tracked entity and mutates it in place rather than
    // Update()-ing a detached graph from the client - a naive Update()
    // on the whole Item would try to re-insert AssignedTests as new rows
    // without removing the old ones, and would wipe Specifications
    // (managed on its own page, never sent by this form) since the
    // incoming graph never populates that collection.
    public async Task UpdateAsync(int id, Item update)
    {
        if (!AllowedItemCategories.Contains(update.Category))
        {
            throw new InvalidOperationException(
                "Items can only be configured for Product, Raw Material, or " +
                "Packaging Material categories. Water, Environmental Monitoring, " +
                "After Cleaning, and GPT are managed via their dedicated " +
                "configuration pages.");
        }

        var item = await _db.Items.Include(i => i.AssignedTests).FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new InvalidOperationException($"Item {id} not found.");

        if (await _db.Items.AnyAsync(i => i.Code == update.Code && i.Id != id))
            throw new InvalidOperationException($"An item with code '{update.Code}' already exists.");

        item.Name = update.Name;
        item.Code = update.Code;
        item.Category = update.Category;
        item.SopNumber = update.SopNumber;

        _db.RemoveRange(item.AssignedTests);
        item.AssignedTests = update.AssignedTests
            .Select(t => new SampleTest { TestCode = t.TestCode, DisplayName = t.DisplayName })
            .ToList();

        await _db.SaveChangesAsync();
    }

    // Frozen (not deleted) items stay visible for historical traceability
    // but ProductWorkflowEngine.ReceiveAsync refuses to use them for new
    // samples. This is the safe way to retire an Item that has already
    // been used - see DeleteAsync for why a hard delete can't always happen.
    public async Task SetActiveAsync(int id, bool isActive)
    {
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new InvalidOperationException($"Item {id} not found.");
        item.IsActive = isActive;
        await _db.SaveChangesAsync();
    }

    // Sample.ItemId is a Restrict FK (see SampleConfiguration) - deleting
    // an Item that has ever received a sample would otherwise surface as
    // a raw database constraint error. Guard it here with a clear message
    // and point at Freeze as the alternative.
    public async Task DeleteAsync(int id)
    {
        var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new InvalidOperationException($"Item {id} not found.");

        var sampleCount = await _db.Samples.CountAsync(s => s.ItemId == id);
        if (sampleCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete '{item.Name}' - it has been used to receive {sampleCount} sample(s). Freeze it instead to stop new samples without losing history.");

        _db.Items.Remove(item);
        await _db.SaveChangesAsync();
    }
}
