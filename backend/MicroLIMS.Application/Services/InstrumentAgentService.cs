using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services;

// Lifecycle of the on-site agents that push instrument data: register,
// rotate key, revoke. The plain key exists only in the return value of
// register/rotate; the database holds its SHA-256 hex.
public class InstrumentAgentService
{
    private const int KeyPrefixLength = 12;

    private readonly IMicroLimsDbContext _db;
    private readonly IUserSectionScopeService _scope;
    private readonly IAuditEventService _audit;
    private readonly ILabClock _clock;

    public InstrumentAgentService(IMicroLimsDbContext db, IUserSectionScopeService scope, IAuditEventService audit, ILabClock? clock = null)
    {
        _db = db;
        _scope = scope;
        _audit = audit;
        _clock = clock ?? LabClock.Default;
    }

    // Lowercase SHA-256 hex; also what the ingest authenticator hashes an incoming key with.
    public static string HashKey(string plainKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainKey))).ToLowerInvariant();

    public async Task<(InstrumentAgentDto Agent, string PlainKey)> RegisterAsync(
        int equipmentId, string name, string timeZoneId, int adminUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Agent name is required.");
        RequireKnownTimeZone(timeZoneId);

        var equipment = await _db.Equipment.AsNoTracking().FirstOrDefaultAsync(e => e.Id == equipmentId, ct)
            ?? throw new NotFoundException($"Equipment {equipmentId} not found.");
        if (equipment.Type != EquipmentType.Hplc)
            throw new InvalidOperationException($"Equipment {equipment.Code} is not an HPLC; an agent can only be bound to an HPLC.");
        await _scope.EnsureEquipmentAccessAsync(adminUserId, equipmentId, ct);

        var existing = await _db.InstrumentAgents.AsNoTracking()
            .Where(a => a.EquipmentId == equipmentId)
            .Select(a => new { a.Code, a.IsActive })
            .ToListAsync(ct);
        var active = existing.FirstOrDefault(a => a.IsActive);
        if (active is not null)
            throw new InvalidOperationException($"Equipment {equipment.Code} already has an active agent ({active.Code}). Rotate its key or revoke it first.");

        // A revoked agent keeps its code (deactivate, never delete), so a replacement gets the next suffix.
        var code = existing.Count == 0 ? $"AGT-{equipment.Code}" : $"AGT-{equipment.Code}-{existing.Count + 1}";
        var plainKey = NewKey(code);

        var agent = new InstrumentAgent
        {
            Code = code,
            Name = name.Trim(),
            EquipmentId = equipment.Id,
            SectionId = equipment.SectionId,
            TimeZoneId = timeZoneId,
            KeyHash = HashKey(plainKey),
            KeyPrefix = plainKey[..KeyPrefixLength],
            IsActive = true,
            CreatedAt = _clock.UtcNow.UtcDateTime,
            CreatedByUserId = adminUserId
        };
        _db.InstrumentAgents.Add(agent);
        await _db.SaveChangesAsync(ct);

        await AuditAsync("INSTRUMENT_AGENT_REGISTERED", agent, adminUserId, null, ct);
        return (ToDto(agent), plainKey);
    }

    public async Task<(InstrumentAgentDto Agent, string PlainKey)> RotateKeyAsync(int agentId, int adminUserId, CancellationToken ct)
    {
        var agent = await LoadActiveAsync(agentId, adminUserId, ct);
        var plainKey = NewKey(agent.Code);
        agent.KeyHash = HashKey(plainKey);
        agent.KeyPrefix = plainKey[..KeyPrefixLength];
        await _db.SaveChangesAsync(ct);

        await AuditAsync("INSTRUMENT_AGENT_KEY_ROTATED", agent, adminUserId, null, ct);
        return (ToDto(agent), plainKey);
    }

    public async Task RevokeAsync(int agentId, string reason, int adminUserId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required to revoke an agent.");
        var agent = await LoadActiveAsync(agentId, adminUserId, ct);
        agent.IsActive = false;
        agent.RevokedAt = _clock.UtcNow.UtcDateTime;
        agent.RevokedByUserId = adminUserId;
        agent.RevokeReason = reason.Trim();
        await _db.SaveChangesAsync(ct);

        await AuditAsync("INSTRUMENT_AGENT_REVOKED", agent, adminUserId, agent.RevokeReason, ct);
    }

    private async Task<InstrumentAgent> LoadActiveAsync(int agentId, int adminUserId, CancellationToken ct)
    {
        var agent = await _db.InstrumentAgents.FirstOrDefaultAsync(a => a.Id == agentId, ct)
            ?? throw new NotFoundException($"Instrument agent {agentId} not found.");
        await _scope.EnsureEquipmentAccessAsync(adminUserId, agent.EquipmentId, ct);
        if (!agent.IsActive) throw new InvalidOperationException($"Agent {agent.Code} is revoked.");
        return agent;
    }

    private async Task AuditAsync(string actionCode, InstrumentAgent agent, int adminUserId, string? reason, CancellationToken ct)
    {
        _db.CurrentUserId = adminUserId;
        await _audit.RecordUserEventAsync(actionCode, AuditActionCategory.Security, nameof(InstrumentAgent),
            reason: reason, entityId: agent.Id.ToString(), sourceContext: $"{agent.Code} / equipment {agent.EquipmentId}",
            cancellationToken: ct);
    }

    private static void RequireKnownTimeZone(string timeZoneId)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? string.Empty); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new InvalidOperationException($"Unknown time zone '{timeZoneId}'. Use an IANA id such as Africa/Cairo.");
        }
    }

    // lims_agent_{Code}_{32 random bytes, base64url}
    private static string NewKey(string code) =>
        $"lims_agent_{code}_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";

    private static InstrumentAgentDto ToDto(InstrumentAgent a) => new(
        a.Id, a.Code, a.Name, a.EquipmentId, a.SectionId, a.TimeZoneId, a.KeyPrefix,
        a.IsActive, a.CreatedAt, a.RevokedAt, a.RevokeReason, a.LastSeenAt);
}
