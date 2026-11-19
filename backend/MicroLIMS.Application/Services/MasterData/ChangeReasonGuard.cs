using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services.MasterData;

public enum ChangeKind
{
    Edit,
    Delete,
    Retire,
    Freeze,
    Unfreeze
}

// Enforces the change-reason rule on master-data edits, deletes, retirements,
// freezes and unfreezes. Edit requires a reason only when open work exists
// (computed on the server in the same request); the other actions always require
// one. Sets db.CurrentReason so MicroLimsDbContext records it on AuditLog.
public sealed class ChangeReasonGuard
{
    public const int MinLength = 10;
    public const int MaxLength = 500;
    public const string FieldName = "reason";

    private readonly IMicroLimsDbContext _db;
    private readonly MasterDataUsageService _usage;

    public ChangeReasonGuard(IMicroLimsDbContext db, MasterDataUsageService usage)
    {
        _db = db;
        _usage = usage;
    }

    public async Task EnsureAsync(
        MasterRecordType type,
        int id,
        ChangeKind kind,
        string? reason,
        int userId,
        CancellationToken ct = default)
    {
        bool required = kind != ChangeKind.Edit;
        int openWorkCount = 0;

        if (!required)
        {
            // Every section counts here: the rule must not depend on which sections the editor can read.
            openWorkCount = await _usage.GetUnscopedOpenWorkCountAsync(type, id, ct);
            required = openWorkCount > 0;
        }

        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (!required)
                return;

            var msg = kind == ChangeKind.Edit
                ? $"A reason of at least {MinLength} characters is required because {openWorkCount} open work item(s) exist."
                : $"A reason of at least {MinLength} characters is required for this change.";
            throw new FieldValidationException(msg, new[] { new FieldError(FieldName, msg) });
        }

        if (trimmed.Length < MinLength)
        {
            var msg = $"Reason must be at least {MinLength} characters (entered {trimmed.Length}).";
            throw new FieldValidationException(msg, new[] { new FieldError(FieldName, msg) });
        }

        if (trimmed.Length > MaxLength)
        {
            var msg = $"Reason must be at most {MaxLength} characters.";
            throw new FieldValidationException(msg, new[] { new FieldError(FieldName, msg) });
        }

        _db.CurrentReason = trimmed;
    }
}
