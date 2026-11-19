using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;

namespace MicroLIMS.Application.Services;

public sealed class UserUiStateService : IUserUiStateService
{
    public const int MaxValueLength = 2048;
    public const int MaxKeysPerUser = 100;

    private static readonly Regex KeyPattern = new("^[a-z][a-zA-Z0-9.]{1,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IMicroLimsDbContext _db;
    private readonly TimeProvider _time;

    public UserUiStateService(IMicroLimsDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _time = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(int userId, CancellationToken ct = default)
    {
        var rows = await _db.UserUiStates.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Key, s.Value })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => r.Value);
    }

    public async Task SetAsync(int userId, string key, string value, CancellationToken ct = default)
    {
        ValidateKey(key);
        if (value is null) throw new BusinessRuleException(MessageCodes.UserUiStateValueRequired, "A value is required.");
        if (value.Length > MaxValueLength)
            throw new BusinessRuleException(
                MessageCodes.UserUiStateValueTooLong,
                $"A value can be at most {MaxValueLength} characters.",
                new Dictionary<string, string> { ["maxLength"] = MaxValueLength.ToString(CultureInfo.InvariantCulture) });

        var existing = await _db.UserUiStates.SingleOrDefaultAsync(s => s.UserId == userId && s.Key == key, ct);
        var now = _time.GetUtcNow().UtcDateTime;
        if (existing is not null)
        {
            existing.Value = value;
            existing.UpdatedAt = now;
        }
        else
        {
            var count = await _db.UserUiStates.CountAsync(s => s.UserId == userId, ct);
            if (count >= MaxKeysPerUser)
                throw new BusinessRuleException(
                    MessageCodes.UserUiStateMaxKeysReached,
                    $"At most {MaxKeysPerUser} interface settings can be stored per user.",
                    new Dictionary<string, string> { ["maxKeys"] = MaxKeysPerUser.ToString(CultureInfo.InvariantCulture) });
            _db.UserUiStates.Add(new UserUiState { UserId = userId, Key = key, Value = value, UpdatedAt = now });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(int userId, string key, CancellationToken ct = default)
    {
        ValidateKey(key);
        var existing = await _db.UserUiStates.SingleOrDefaultAsync(s => s.UserId == userId && s.Key == key, ct);
        if (existing is null) return;
        _db.UserUiStates.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }

    private static void ValidateKey(string key)
    {
        if (key is null || !KeyPattern.IsMatch(key))
            throw new BusinessRuleException(
                MessageCodes.UserUiStateKeyInvalid,
                "The key must start with a lowercase letter and contain only letters, digits and dots (2 to 64 characters).");
    }
}
