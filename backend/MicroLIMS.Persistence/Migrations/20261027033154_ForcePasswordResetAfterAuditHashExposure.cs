using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ForcePasswordResetAfterAuditHashExposure : Migration
    {
        // Deviation DEV-2026-AUD-001 (docs/deviations/DEV-2026-AUD-001-credential-hashes-in-audit-trail.md).
        // Until [AuditRedacted] the automatic audit capture wrote User.PasswordHash, PasswordHistory.PasswordHash,
        // PasswordResetToken.TokenHash, AdminPasswordRecovery.CodeHash and InstrumentAgent.KeyHash into
        // AuditLogs / AuditEventChanges. Those rows are append-only and are left untouched (21 CFR Part 11
        // 11.10(e), EU GMP Annex 11 s9), so the exposed credentials are retired instead:
        //   * every user must choose a new password (MustChangePassword; enforced server-side by
        //     MustChangePasswordFilter, so live access tokens can do nothing but change the password,
        //     read /me and log out - no separate access-token invalidation is needed);
        //   * every refresh token is revoked (RevokedAt, rows kept) so no session can be silently renewed;
        //   * ONE System-actor audit row records the action.
        // Instrument agent keys cannot be re-keyed by SQL (the new key is shown to an administrator once);
        // an administrator rotates each key - see the deviation record, "Immediate actions".
        private const string ProcessName = "Migration 20261027033154_ForcePasswordResetAfterAuditHashExposure";

        private const string Reason =
            "Forced password reset: credential hashes were captured in the audit trail before redaction (deviation DEV-2026-AUD-001)";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Inactive users are included so a later reactivation does not bring back an exposed password.
            // Every user is affected, so one aggregate row rather than one per user.
            migrationBuilder.Sql($$"""
                DO $$
                DECLARE
                    affected_users integer;
                    revoked_tokens integer;
                BEGIN
                    SELECT COUNT(*) INTO affected_users FROM "Users";
                    UPDATE "Users" SET "MustChangePassword" = TRUE;

                    UPDATE "RefreshTokens" SET "RevokedAt" = now() WHERE "RevokedAt" IS NULL;
                    GET DIAGNOSTICS revoked_tokens = ROW_COUNT;

                    INSERT INTO "AuditLogs" (
                        "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                        "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                    VALUES (
                        'User', 'ALL', 'FORCED_PASSWORD_RESET',
                        NULL,
                        jsonb_build_object('MustChangePassword', TRUE, 'UsersAffected', affected_users, 'RefreshTokensRevoked', revoked_tokens)::text,
                        0, now(), 2, '{{ProcessName}}', 'ForcedPasswordReset', 7, '{{Reason}}');
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally a no-op: the forced reset and token revocation cannot be meaningfully undone
            // (users will already have chosen new passwords, and restoring the exposed credentials
            // would be the opposite of the corrective action).
        }
    }
}
