using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Persistence.Repositories;

public interface IMediaRepository
{
    Task<Media?> GetByIdAsync(int id);
    Task<List<Media>> GetAllAsync();
    Task AddAsync(Media entity);
    Task UpdateAsync(Media entity);
}

public class MediaRepository : IMediaRepository
{
    private readonly MicroLimsDbContext _db;

    public MediaRepository(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<Media?> GetByIdAsync(int id) =>
        await _db.Media.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<List<Media>> GetAllAsync() =>
        await _db.Media.ToListAsync();

    public async Task AddAsync(Media entity)
    {
        _db.Media.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Media entity)
    {
        _db.Media.Update(entity);
        await _db.SaveChangesAsync();
    }
}
