using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

public class UserService
{
    private readonly MicroLimsDbContext _db;

    public UserService(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<List<User>> GetAllAsync() =>
        await _db.Users.Include(u => u.Role).ToListAsync();

    public async Task<User> CreateAsync(User user, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 8)
            throw new InvalidOperationException("Password must be at least 8 characters.");
        if (await _db.Users.AnyAsync(u => u.Username == user.Username))
            throw new InvalidOperationException($"Username '{user.Username}' is already taken.");
        if (!await _db.Roles.AnyAsync(r => r.Id == user.RoleId))
            throw new InvalidOperationException("Selected role does not exist.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task DeactivateAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return;
        user.IsActive = false;
        await _db.SaveChangesAsync();
    }
}
