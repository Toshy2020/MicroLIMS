-- Phase 6 Legacy Tables Backup & Archive
-- Executed prior to dropping MediaTypes, MediaChallengeSpecs, and TestDefinitionMedias.

CREATE SCHEMA IF NOT EXISTS archive_legacy_media;

CREATE TABLE IF NOT EXISTS archive_legacy_media."MediaTypes_Backup_Phase6" AS 
SELECT * FROM "MediaTypes";

CREATE TABLE IF NOT EXISTS archive_legacy_media."MediaChallengeSpecs_Backup_Phase6" AS 
SELECT * FROM "MediaChallengeSpecs";

CREATE TABLE IF NOT EXISTS archive_legacy_media."TestDefinitionMedias_Backup_Phase6" AS 
SELECT * FROM "TestDefinitionMedias";
