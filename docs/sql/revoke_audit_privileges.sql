-- ============================================================================
-- MicroLIMS Document Control (Release 1a) - WP1
-- Database-Level Audit Immutability: Privilege Revocation Script
-- File: ./docs/sql/revoke_audit_privileges.sql
--
-- PURPOSE:
-- Enforces database-level append-only immutability for regulatory compliance
-- (21 CFR Part 11, EU Annex 11, and URS DC-URS-127, DC-URS-128, DC-URS-152).
-- Revokes UPDATE and DELETE privileges on the audit and electronic signature
-- tables for the application runtime database role.
--
-- WHEN TO RUN:
-- This script is executed by the Database Administrator during deployment
-- Installation Qualification (IQ) after running EF Core database migrations.
-- It must NOT be applied automatically by the application migration runner.
--
-- INSTRUCTIONS:
-- Replace 'microlims_app_user' with the actual application database role/user.
-- ============================================================================

DO $$
BEGIN
    -- Revoke UPDATE and DELETE permissions from the application user
    REVOKE UPDATE, DELETE ON TABLE "AuditLogs" FROM microlims_app_user;
    REVOKE UPDATE, DELETE ON TABLE "AuditEventChanges" FROM microlims_app_user;
    REVOKE UPDATE, DELETE ON TABLE "ElectronicSignatures" FROM microlims_app_user;

    -- Ensure SELECT and INSERT remain granted
    GRANT SELECT, INSERT ON TABLE "AuditLogs" TO microlims_app_user;
    GRANT SELECT, INSERT ON TABLE "AuditEventChanges" TO microlims_app_user;
    GRANT SELECT, INSERT ON TABLE "ElectronicSignatures" TO microlims_app_user;
END $$;
