using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
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

    // Builds the Item from the fields a client may set. Id, IsActive and
    // Specifications are never taken from the request - see ItemSaveRequest.
    public async Task<Item> CreateAsync(ItemSaveRequest request)
    {
        EnsureAllowedCategory(request.Category);

        if (await _db.Items.AnyAsync(i => i.Code == request.Code))
            throw new InvalidOperationException($"An item with code '{request.Code}' already exists.");

        var item = new Item
        {
            Name = request.Name,
            Code = request.Code,
            Category = request.Category,
            SopNumber = request.SopNumber ?? string.Empty,
            AssignedTests = ToSampleTests(request.AssignedTests)
        };

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
    public async Task UpdateAsync(int id, ItemSaveRequest update)
    {
        EnsureAllowedCategory(update.Category);

        var item = await _db.Items.Include(i => i.AssignedTests).FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new InvalidOperationException($"Item {id} not found.");

        if (await _db.Items.AnyAsync(i => i.Code == update.Code && i.Id != id))
            throw new InvalidOperationException($"An item with code '{update.Code}' already exists.");

        item.Name = update.Name;
        item.Code = update.Code;
        item.Category = update.Category;
        item.SopNumber = update.SopNumber ?? string.Empty;

        _db.RemoveRange(item.AssignedTests);
        item.AssignedTests = ToSampleTests(update.AssignedTests);

        await _db.SaveChangesAsync();
    }

    private static void EnsureAllowedCategory(SampleCategory category)
    {
        if (!AllowedItemCategories.Contains(category))
        {
            throw new InvalidOperationException(
                "Items can only be configured for Product, Raw Material, or " +
                "Packaging Material categories. Water, Environmental Monitoring, " +
                "After Cleaning, and GPT are managed via their dedicated " +
                "configuration pages.");
        }
    }

    private static List<SampleTest> ToSampleTests(List<ItemTestRequest>? tests) =>
        (tests ?? new List<ItemTestRequest>())
            .Select(t => new SampleTest { TestCode = t.TestCode, DisplayName = t.DisplayName })
            .ToList();

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
