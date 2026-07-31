using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Persistence.Repositories;

public interface ISampleRepository
{
    Task<Sample?> GetByIdAsync(int id);
    Task<List<Sample>> GetAllAsync();
    Task AddAsync(Sample entity);
    Task UpdateAsync(Sample entity);
}

public class SampleRepository : ISampleRepository
{
    private readonly MicroLimsDbContext _db;

    public SampleRepository(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<Sample?> GetByIdAsync(int id) =>
        await _db.Samples.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<List<Sample>> GetAllAsync() =>
        await _db.Samples.ToListAsync();

    public async Task AddAsync(Sample entity)
    {
        _db.Samples.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Sample entity)
    {
        _db.Samples.Update(entity);
        await _db.SaveChangesAsync();
    }
}
