using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Thrown specifically when a hard delete is blocked because the user has
// history somewhere in UserReferenceRegistry - kept distinct from a plain
// InvalidOperationException so the controller can map it to 409 Conflict
// instead of the generic 400 used for validation failures.
public class UserHasHistoryException : InvalidOperationException
{
    public UserHasHistoryException(string message) : base(message) { }
}

public class UserDeletionService
{
    private readonly MicroLimsDbContext _db;

    public UserDeletionService(MicroLimsDbContext db)
    {
        _db = db;
    }

    // Walks every UserReferenceRegistry entry marked Blocks, short-circuiting
    // on the first hit. Each entity type is queried generically via reflection
    // so this doesn't require 50+ hand-written near-identical LINQ checks -
    // the reviewed (entityType, propertyName) list in UserReferenceRegistry is
    // the thing that has to stay correct, not this loop.
    public async Task<bool> UserHasAnyHistoryAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (user.LastLoginAt is not null)
            return true;

        foreach (var entry in UserReferenceRegistry.All)
        {
            if (entry.Disposition != UserReferenceDisposition.Blocks)
                continue;

            if (await AnyReferenceAsync(entry.EntityType, entry.PropertyName, userId))
                return true;
        }

        return false;
    }

    public async Task HardDeleteAsync(int targetUserId, int actingUserId)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new InvalidOperationException($"User {targetUserId} not found.");

        if (actingUserId == targetUserId)
            throw new InvalidOperationException("You cannot permanently delete your own account.");

        if (user.IsActive && user.Role?.Type == RoleType.SystemAdministrator)
        {
            var otherActiveAdmins = await _db.Users.CountAsync(u =>
                u.Id != targetUserId && u.IsActive && u.Role != null && u.Role.Type == RoleType.SystemAdministrator);

            if (otherActiveAdmins == 0)
                throw new InvalidOperationException("Cannot delete the last active System Administrator account. Promote another user to System Administrator first.");
        }

        if (await UserHasAnyHistoryAsync(targetUserId))
            throw new UserHasHistoryException("This user has activity history and cannot be permanently deleted. Deactivate instead.");

        var deletedUserSnapshot = new
        {
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            RoleName = user.Role?.Name,
            RoleId = user.RoleId
        };

        _db.Users.Remove(user);

        // Removing the User row here and adding the audit entry in the same
        // SaveChangesAsync call keeps both in one transaction - EntityId is
        // set from the already-known real targetUserId (not a DB-generated
        // key read pre-save), so this isn't subject to the Added-entity
        // temp-key issue that affects Create audit rows elsewhere.
        _db.AuditLogs.Add(new AuditLog
        {
            EntityName = nameof(User),
            EntityId = targetUserId.ToString(),
            Action = "USER_HARD_DELETED",
            PreviousValue = JsonSerializer.Serialize(deletedUserSnapshot),
            NewValue = null,
            UserId = actingUserId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    private async Task<bool> AnyReferenceAsync(Type entityType, string propertyName, int userId)
    {
        var method = typeof(UserDeletionService)
            .GetMethod(nameof(AnyReferenceGenericAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType);

        return await (Task<bool>)method.Invoke(this, new object[] { propertyName, userId })!;
    }

    private async Task<bool> AnyReferenceGenericAsync<TEntity>(string propertyName, int userId) where TEntity : class
    {
        var property = typeof(TEntity).GetProperty(propertyName)
            ?? throw new InvalidOperationException($"UserReferenceRegistry entry for {typeof(TEntity).Name}.{propertyName} does not match a real property.");

        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var propertyAccess = Expression.Property(parameter, property);

        Expression comparison = property.PropertyType == typeof(int?)
            ? Expression.Equal(propertyAccess, Expression.Constant((int?)userId, typeof(int?)))
            : Expression.Equal(propertyAccess, Expression.Constant(userId));

        var lambda = Expression.Lambda<Func<TEntity, bool>>(comparison, parameter);

        return await _db.Set<TEntity>().AnyAsync(lambda);
    }
}
