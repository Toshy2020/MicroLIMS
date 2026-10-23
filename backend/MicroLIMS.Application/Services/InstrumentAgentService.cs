using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
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
    private const int MaxCodeLength = 50; // InstrumentAgents.Code column width
    private const int KeyPrefixLength = 8; // first chars of the random part (the "lims_agent_" label is identical for every agent)

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

    public Task<(InstrumentAgentDto Agent, string PlainKey)> RegisterAsync(
        int equipmentId, string name, string timeZoneId, int adminUserId, CancellationToken ct) =>
        UnitOfWork.RunAsync(_db, () => RegisterCoreAsync(equipmentId, name, timeZoneId, adminUserId, ct));

    private async Task<(InstrumentAgentDto Agent, string PlainKey)> RegisterCoreAsync(
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
        if (code.Length > MaxCodeLength)
            throw new InvalidOperationException($"Equipment code {equipment.Code} is too long for an agent code ({code.Length} characters, the limit is {MaxCodeLength}). Shorten the equipment code first.");
        var (plainKey, keyPrefix) = NewKey(code);

        var agent = new InstrumentAgent
        {
            Code = code,
            Name = name.Trim(),
            EquipmentId = equipment.Id,
            SectionId = equipment.SectionId,
            TimeZoneId = timeZoneId,
            KeyHash = HashKey(plainKey),
            KeyPrefix = keyPrefix,
            IsActive = true,
            CreatedAt = _clock.UtcNow.UtcDateTime,
            CreatedByUserId = adminUserId
        };
        _db.InstrumentAgents.Add(agent);
        await _db.SaveChangesAsync(ct);

        await AuditAsync("INSTRUMENT_AGENT_REGISTERED", agent, adminUserId, null, ct);
        return (ToDto(agent), plainKey);
    }

    public Task<(InstrumentAgentDto Agent, string PlainKey)> RotateKeyAsync(int agentId, int adminUserId, CancellationToken ct) =>
        UnitOfWork.RunAsync(_db, () => RotateKeyCoreAsync(agentId, adminUserId, ct));

    private async Task<(InstrumentAgentDto Agent, string PlainKey)> RotateKeyCoreAsync(int agentId, int adminUserId, CancellationToken ct)
    {
        var agent = await LoadActiveAsync(agentId, adminUserId, ct);
        var (plainKey, keyPrefix) = NewKey(agent.Code);
        agent.KeyHash = HashKey(plainKey);
        agent.KeyPrefix = keyPrefix;
        await _db.SaveChangesAsync(ct);

        await AuditAsync("INSTRUMENT_AGENT_KEY_ROTATED", agent, adminUserId, null, ct);
        return (ToDto(agent), plainKey);
    }

    public Task RevokeAsync(int agentId, string reason, int adminUserId, CancellationToken ct) =>
        UnitOfWork.RunAsync(_db, () => RevokeCoreAsync(agentId, reason, adminUserId, ct));

    private async Task RevokeCoreAsync(int agentId, string reason, int adminUserId, CancellationToken ct)
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
        _db.CurrentUserId = adminUserId; // the audit row takes its actor from here; AuditMiddleware only sets it in HTTP requests
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

    // lims_agent_{Code}_{32 random bytes, base64url}; the prefix is the first
    // characters of the random part, which is what tells agents apart.
    private static (string Key, string Prefix) NewKey(string code)
    {
        var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return ($"lims_agent_{code}_{random}", random[..KeyPrefixLength]);
    }

    private static InstrumentAgentDto ToDto(InstrumentAgent a) => new(
        a.Id, a.Code, a.Name, a.EquipmentId, a.SectionId, a.TimeZoneId, a.KeyPrefix,
        a.IsActive, a.CreatedAt, a.RevokedAt, a.RevokeReason, a.LastSeenAt);
}
