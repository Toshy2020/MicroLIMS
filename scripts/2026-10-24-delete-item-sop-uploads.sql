-- One-off: hard delete the STM/SOP files uploaded on Item Master (ItemDocuments.DocumentType = 0).
-- STMs now come from the Document Library. Run step 1, show the rows, then step 2.
-- Stored files are deleted separately by their StorageKey (local disk / B2).
-- AuditLogs rows for these uploads stay (immutable by trigger).

-- Step 1: review
SELECT "Id","ItemId","OriginalFileName","StorageKey","Status" FROM "ItemDocuments" WHERE "DocumentType" = 0 ORDER BY "Id";

-- Step 2: delete
BEGIN;
DELETE FROM "ItemDocumentAccessLogs" WHERE "DocumentId" IN (SELECT "Id" FROM "ItemDocuments" WHERE "DocumentType" = 0);
UPDATE "ItemDocuments" SET "SupersededByDocumentId" = NULL
 WHERE "SupersededByDocumentId" IN (SELECT "Id" FROM "ItemDocuments" WHERE "DocumentType" = 0);
DELETE FROM "ItemDocuments" WHERE "DocumentType" = 0;
COMMIT;
