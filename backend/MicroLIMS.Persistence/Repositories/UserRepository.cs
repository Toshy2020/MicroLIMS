using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Persistence.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<List<User>> GetAllAsync();
    Task AddAsync(User entity);
    Task UpdateAsync(User entity);
}

public class UserRepository : IUserRepository
{
    private readonly MicroLimsDbContext _db;

    public UserRepository(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(int id) =>
        await _db.Users.FirstOrDefaultAsync(e => e.Id == id);

    public async Task<List<User>> GetAllAsync() =>
        await _db.Users.ToListAsync();

    public async Task AddAsync(User entity)
    {
        _db.Users.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(User entity)
    {
        _db.Users.Update(entity);
        await _db.SaveChangesAsync();
    }
}
