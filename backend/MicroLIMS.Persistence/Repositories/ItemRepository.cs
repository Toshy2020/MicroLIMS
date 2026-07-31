using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Persistence.Repositories;

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(int id);
    Task<List<Item>> GetAllAsync();
    Task AddAsync(Item entity);
    Task UpdateAsync(Item entity);
}

public class ItemRepository : IItemRepository
{
    private readonly MicroLimsDbContext _db;

    public ItemRepository(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<Item?> GetByIdAsync(int id) =>
        await _db.Items.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<List<Item>> GetAllAsync() =>
        await _db.Items.ToListAsync();

    public async Task AddAsync(Item entity)
    {
        _db.Items.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Item entity)
    {
        _db.Items.Update(entity);
        await _db.SaveChangesAsync();
    }
}
