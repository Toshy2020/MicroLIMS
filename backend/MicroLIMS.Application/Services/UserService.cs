using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Validation;

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

        var passwordFailures = PasswordPolicy.Validate(plainPassword);
        if (passwordFailures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", passwordFailures));

        if (await _db.Users.AnyAsync(u => u.Username == user.Username))
            throw new InvalidOperationException($"Username '{user.Username}' is already taken.");
        if (!await _db.Roles.AnyAsync(r => r.Id == user.RoleId))
            throw new InvalidOperationException("Selected role does not exist.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        user.MustChangePassword = true;
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

    // Not required at creation (existing seeded/legacy users may have
    // none), but the password-reset flow needs it to actually deliver a
    // reset link - see AuthenticationService.RequestPasswordResetAsync.
    public async Task UpdateEmailAsync(int userId, string? email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        user.Email = email;
        await _db.SaveChangesAsync();
    }
}
