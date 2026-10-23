using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;

namespace MicroLIMS.API.Authorization;

public static class AgentKeyDefaults
{
    public const string Scheme = "AgentKey";
    public const string HeaderName = "X-Agent-Key";
    public const string Policy = "InstrumentAgent";

    public const string AgentIdClaim = "agent_id";
    public const string AgentCodeClaim = "agent_code";
    public const string EquipmentIdClaim = "equipment_id";
    public const string SectionIdClaim = "section_id";

    // Keys are "lims_agent_<code>_<random>" (InstrumentAgentService.NewKey).
    internal const string KeyPrefix = "lims_agent_";
    internal const int MaxKeyLength = 256;
}

// Authenticates an on-site edge agent by its per-agent key (header
// X-Agent-Key). Only runs for endpoints that name the "AgentKey" scheme, so
// the key opens nothing that is guarded by the default JWT scheme.
//
// The principal deliberately has NO NameIdentifier claim: AuditMiddleware and
// MustChangePasswordFilter read it as a user id, and an agent is not a user.
public sealed class AgentKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private static readonly TimeSpan LastSeenInterval = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _time;

    public AgentKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, TimeProvider time)
        : base(options, logger, encoder)
    {
        _time = time;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AgentKeyDefaults.HeaderName, out var values)
            || string.IsNullOrWhiteSpace(values.ToString()))
            return AuthenticateResult.NoResult();

        var ct = Context.RequestAborted;
        var key = values.ToString();
        if (values.Count != 1 || key.Length > AgentKeyDefaults.MaxKeyLength
            || !key.StartsWith(AgentKeyDefaults.KeyPrefix, StringComparison.Ordinal))
            return await FailAsync("Malformed agent key.", null, ct);

        var db = Context.RequestServices.GetRequiredService<IMicroLimsDbContext>();
        var hash = InstrumentAgentService.HashKey(key);
        var agent = await db.InstrumentAgents.AsNoTracking()
            .Where(a => a.KeyHash == hash)
            .Select(a => new
            {
                a.Id, a.Code, a.EquipmentId, a.SectionId, a.IsActive, a.LastSeenAt,
                EquipmentExists = db.Equipment.Any(e => e.Id == a.EquipmentId)
            })
            .FirstOrDefaultAsync(ct);

        if (agent is null) return await FailAsync("Unknown agent key.", null, ct);
        // A revoked agent keeps its hash (deactivate, never delete).
        if (!agent.IsActive) return await FailAsync("Agent has been revoked.", agent.Code, ct);
        // Equipment has no IsActive column; a removed row is the deactivated state.
        if (!agent.EquipmentExists) return await FailAsync("The agent's equipment no longer exists.", agent.Code, ct);

        await TouchLastSeenAsync(db, agent.Id, agent.LastSeenAt, ct);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(AgentKeyDefaults.AgentIdClaim, agent.Id.ToString()),
            new Claim(AgentKeyDefaults.AgentCodeClaim, agent.Code),
            new Claim(AgentKeyDefaults.EquipmentIdClaim, agent.EquipmentId.ToString()),
            new Claim(AgentKeyDefaults.SectionIdClaim, agent.SectionId.ToString())
        }, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    // At most once a minute per agent. Set-based so it neither loads the row
    // nor writes a GxP AuditLog entry per heartbeat, and the WHERE makes
    // concurrent requests race safely. Never fails authentication.
    private async Task TouchLastSeenAsync(IMicroLimsDbContext db, int agentId, DateTime? lastSeen, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        if (lastSeen is { } seen && now - seen < LastSeenInterval) return;
        var cutoff = now - LastSeenInterval;
        try
        {
            await db.InstrumentAgents
                .Where(a => a.Id == agentId && (a.LastSeenAt == null || a.LastSeenAt <= cutoff))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSeenAt, now), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "Could not update LastSeenAt for agent {AgentId}.", agentId);
        }
    }

    // Records the failure (agent code if known, reason, IP; never the key or
    // its hash) and fails the request. Losing the audit row must not turn a
    // refusal into a 500.
    private async Task<AuthenticateResult> FailAsync(string reason, string? agentCode, CancellationToken ct)
    {
        try
        {
            var audit = Context.RequestServices.GetRequiredService<ISecurityAuditService>();
            audit.Record(new SecurityEventRequest(
                SecurityEventCodes.AgentAuthFailed, SecurityEventOutcome.Failure,
                Reason: reason,
                Metadata: agentCode is null ? null : System.Text.Json.JsonSerializer.Serialize(new { agentCode })));
            await Context.RequestServices.GetRequiredService<IMicroLimsDbContext>().SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "Could not record the failed agent authentication.");
        }
        return AuthenticateResult.Fail(reason);
    }
}
