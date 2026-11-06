BEGIN;
SELECT count(*) FROM "TestDefinitions" WHERE "Code" LIKE 'RM-%';
ROLLBACK;
