-- S4.1 RM & PM titration test data seed for the LOCAL dev database (LIMSV2).
-- Brief: .superpowers/sdd/2026-10-05-physchem-areas/lane-S4-brief.md (Task S4.1).
-- Run AFTER all migrations of feat/physchem-areas (needs TestDefinitions."PhyschemArea" and MaterialMasterCategory.PrimaryStandard).
-- Needs the HPLC chain seed (FP section, admin user, RG-022 KHP, RG-011 NaOH, IN-001/IN-005 indicators, RS-009 Ascorbic Acid RS,
-- 0.1 N Iodine VS). Idempotent: every insert is guarded by WHERE NOT EXISTS on its natural key.
-- TEST DATA: equivalency factors / limits are typical values; the lab must use its own validated method.
-- Enum ints (checked against backend/MicroLIMS.Domain/Enums):
--   MaterialMasterCategory Reagent0/Indicator1/ReferenceStandard2/PrimaryStandard3; MaterialType ReferenceStandard11/PrimaryStandard13;
--   MaterialUnit Gram0; SolutionType Titrant2; StandardizationMode PrimaryStandard0/AgainstVolumetricSolution1;
--   TitrantStrengthUnit Normal0; ShelfLifeUnit Days1; PhyschemArea RawPackaging1; SampleCategory RawMaterial1;
--   TitrationType AcidBase0/Redox1; TitrationMode Direct0/Residual1; TitrationCalculation UspFactor0/Relative1;
--   TitrationEndpoint Visual0; ResultBasis PercentAsIs3; LimitType Range0; WorkflowType Titration13; EquationType Titration14.
BEGIN;

CREATE TEMP TABLE _ctx ON COMMIT DROP AS
SELECT (SELECT "Id" FROM "DocumentSections" WHERE "Code" = 'FP') AS sec,
       (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' AND "IsActive") AS uid,
       now() AS ts;

DO $$ BEGIN
  IF (SELECT sec FROM _ctx) IS NULL OR (SELECT uid FROM _ctx) IS NULL THEN
    RAISE EXCEPTION 'FP section or admin user not found';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e, _ctx c WHERE e."SectionId" = c.sec AND e."Code" = 'RG-022') THEN
    RAISE EXCEPTION 'KHP entry RG-022 not found (run the HPLC chain seed first)';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "SolutionMasters" s, _ctx c WHERE s."SectionId" = c.sec AND s."Name" = '0.1 N Iodine VS' AND s."Type" = 2) THEN
    RAISE EXCEPTION '0.1 N Iodine VS not found (run the HPLC chain seed first)';
  END IF;
END $$;

------------------------------------------------------------------------------
-- 1. KHP primary standard: reuse RG-022 (migrated to Primary Standard by the physchem-areas migrations; the UPDATE is a
--    no-op once migrated) and give it a PrimaryStandard lot, purity 99.95, expiry today + 2 years.
------------------------------------------------------------------------------
UPDATE "MaterialMasterEntries" e SET "Category" = 3
FROM _ctx c WHERE e."SectionId" = c.sec AND e."Code" = 'RG-022' AND e."Category" <> 3;

INSERT INTO "Materials"
  ("MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location",
   "QuantityReceived","QuantityRemaining","Unit","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt",
   "SectionId","MaterialMasterEntryId","Purity")
SELECT 13, e."Name", 'Sigma-Aldrich', 'KHP-PS-2610', c.ts, c.ts + interval '2 years', e."Code", 'FP chemical store',
       100, 100, 0, c.uid, c.ts, c.uid, c.ts, c.sec, e."Id", 99.95
FROM _ctx c JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = 'RG-022'
WHERE NOT EXISTS (SELECT 1 FROM "Materials" m WHERE m."MaterialMasterEntryId" = e."Id" AND m."BatchNumber" = 'KHP-PS-2610');

------------------------------------------------------------------------------
-- 2. Indicators (only when no indicator of that name exists in FP)
------------------------------------------------------------------------------
INSERT INTO "MaterialMasterEntries"
  ("SectionId","Code","Name","Category","Grade","Source","BaseUnit","IsActive",
   "WorkingConcentration","Solvent","TransitionRangeFrom","TransitionRangeTo","ColourChange","IndicatorUse",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.code, v.name, 1, 'Indicator grade', NULL, 8, true, v.conc, v.solvent, v.tfrom::numeric, v.tto::numeric, v.colour, v.usage,
       c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('IN-001','Phenolphthalein TS','1 g in 100 mL','Alcohol',8.2,10.0,'Colourless to pink','Acid-base (NaOH VS)'),
 ('IN-005','Starch TS','1 g in 100 mL','Water',NULL,NULL,'Blue to colourless','Iodometry')
) AS v(code,name,conc,solvent,tfrom,tto,colour,usage)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e
                  WHERE e."SectionId" = c.sec AND e."Category" = 1 AND (e."Code" = v.code OR e."Name" = v.name));

------------------------------------------------------------------------------
-- 3. Reference standard for the relative test: RS-009 Ascorbic Acid RS (HPLC seed, lot purity 99.8). Only when that
--    entry is absent, create RS-ASCORBIC with a lot of purity 99.8.
------------------------------------------------------------------------------
INSERT INTO "MaterialMasterEntries"
  ("SectionId","Code","Name","Category","Grade","Source","BaseUnit","IsActive","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, 'RS-ASCORBIC', 'Ascorbic Acid RS', 2, 'Primary reference standard', 'USP', 0, true, c.uid, c.ts, c.uid, c.ts
FROM _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" IN ('RS-009','RS-ASCORBIC'));

INSERT INTO "Materials"
  ("MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location",
   "QuantityReceived","QuantityRemaining","Unit","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt",
   "SectionId","MaterialMasterEntryId","Purity","MoisturePercent")
SELECT 11, e."Name", 'USP', 'RSASC-2610', c.ts, c.ts + interval '2 years', e."Code", 'RS fridge 2-8 C',
       0.2, 0.2, 0, c.uid, c.ts, c.uid, c.ts, c.sec, e."Id", 99.8, 0.1
FROM _ctx c JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = 'RS-ASCORBIC'
WHERE NOT EXISTS (SELECT 1 FROM "Materials" m WHERE m."MaterialMasterEntryId" = e."Id" AND m."BatchNumber" = 'RSASC-2610');

------------------------------------------------------------------------------
-- 4. Titrants (Type 2). SolutionMasterService rules: strength + unit, mode, replicates, factor range, max RSD, validity.
--    Columns copied from the existing 0.1 N Sodium hydroxide VS row (30 days, 1000 mL, storage text, BlankRequired).
--    Primary-standard mode (KHP = RG-022) first; the H2SO4 master references 1 N NaOH VS so it goes last.
------------------------------------------------------------------------------
INSERT INTO "SolutionMasters"
  ("SectionId","Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","Instructions","IsActive",
   "NominalStrength","StrengthUnit","StandardizationMode","StandardEntryId","EquivalenceMgPerMl","ReferenceSolutionId",
   "BlankRequired","ReplicateCount","FactorMin","FactorMax","MaxRsdPercent","ValidityDays",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.name, 2, 30, 1, 'Tightly closed, room temperature, protected from CO2', 1000, v.instr, true,
       v.strength, 0, 0,
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = 'RG-022'),
       v.equiv, NULL, true, 3, 0.980, 1.020, 0.2, 30, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('1 N Sodium hydroxide VS', 1.0, 204.22,
  'Dissolve 42 g sodium hydroxide in CO2-free water to 1000 mL. Standardize against potassium biphthalate dried at 120 C for 2 h (USP <Volumetric Solutions>).'),
 ('0.5 N Sodium hydroxide VS', 0.5, 102.11,
  'Dissolve 21 g sodium hydroxide in CO2-free water to 1000 mL. Standardize against potassium biphthalate dried at 120 C for 2 h (USP <Volumetric Solutions>).')
) AS v(name,strength,equiv,instr)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v.name);

INSERT INTO "SolutionMasters"
  ("SectionId","Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","Instructions","IsActive",
   "NominalStrength","StrengthUnit","StandardizationMode","StandardEntryId","EquivalenceMgPerMl","ReferenceSolutionId",
   "BlankRequired","ReplicateCount","FactorMin","FactorMax","MaxRsdPercent","ValidityDays",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, '0.5 N Sulfuric acid VS', 2, 30, 1, 'Tightly closed, room temperature', 1000,
       'Add slowly, with stirring, 30 mL sulfuric acid to 1000 mL water, cool. Standardize against 1 N sodium hydroxide VS (methyl orange or phenolphthalein TS).',
       true, 0.5, 0, 1, NULL, NULL,
       (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '1 N Sodium hydroxide VS' AND s."Type" = 2 AND s."IsActive"),
       false, 3, 0.980, 1.020, 0.2, 30, c.uid, c.ts, c.uid, c.ts
FROM _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '0.5 N Sulfuric acid VS');

------------------------------------------------------------------------------
-- 5. RM items (SampleCategory RawMaterial = 1, FP section tests; Items are not section-scoped)
------------------------------------------------------------------------------
INSERT INTO "Items" ("Name","Code","Category","SopNumber","IsActive")
SELECT v.name, v.code, 1, 'USP-NF', true
FROM (VALUES
 ('Citric acid anhydrous (raw material)','RM-CITRIC'),
 ('Acetylsalicylic acid / aspirin (raw material)','RM-ASPIRIN'),
 ('Ascorbic acid (raw material)','RM-ASCORBIC')
) AS v(name,code)
WHERE NOT EXISTS (SELECT 1 FROM "Items" i WHERE i."Code" = v.code);

------------------------------------------------------------------------------
-- 6. Titration tests (TestDefinitionMasterDataService + TitrationDefinitionRules): WorkflowType 13 + EquationType 14,
--    no system suitability, PhyschemArea RawPackaging (1). Required titration fields set, inapplicable ones NULL
--    (non-aqueous only for acid-base, indicator only for visual, standard entry only for relative, excess only for residual,
--    F only for USP factor, temp correction false / coefficient NULL).
------------------------------------------------------------------------------
-- TIT-CITRIC: acid-base, direct, USP factor, 1 N NaOH, F 64.03, blank off, 1 replicate, visual phenolphthalein
INSERT INTO "TestDefinitions"
  ("Code","DisplayName","SectionId","IsActive","WorkflowType","EquationType","RequiresSystemSuitability","PhyschemArea","ReplicateCount",
   "TitrationType","TitrationNonAqueous","TitrationMode","TitrationCalculation","TitrantSolutionMasterId","TitrationEquivalencyFactor",
   "TitrationBlankRequired","TitrationEndpoint","TitrationIndicatorEntryId","TitrationIndicator","TitrationTempCorrection")
SELECT 'TIT-CITRIC', 'Assay of citric acid (titration)', c.sec, true, 13, 14, false, 1, 1,
       0, false, 0, 0, (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '1 N Sodium hydroxide VS'), 64.03,
       false, 0, ind."Id", ind."Name", false
FROM _ctx c
JOIN LATERAL (SELECT e."Id", e."Name" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Category" = 1 AND e."Name" LIKE 'Phenolphthalein%' ORDER BY e."Code" LIMIT 1) ind ON true
WHERE NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = 'TIT-CITRIC');

-- TIT-ASPIRIN: acid-base, residual, USP factor, blank on, back titrant 0.5 N H2SO4, excess 0.5 N NaOH 50 mL, F 90.08, visual phenolphthalein
INSERT INTO "TestDefinitions"
  ("Code","DisplayName","SectionId","IsActive","WorkflowType","EquationType","RequiresSystemSuitability","PhyschemArea","ReplicateCount",
   "TitrationType","TitrationNonAqueous","TitrationMode","TitrationCalculation","TitrantSolutionMasterId","TitrationEquivalencyFactor",
   "TitrationBlankRequired","TitrationExcessSolutionMasterId","TitrationExcessVolumeMl","TitrationMaxRsdPercent",
   "TitrationEndpoint","TitrationIndicatorEntryId","TitrationIndicator","TitrationTempCorrection")
SELECT 'TIT-ASPIRIN', 'Assay of aspirin (residual titration)', c.sec, true, 13, 14, false, 1, 3,
       0, false, 1, 0, (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '0.5 N Sulfuric acid VS'), 90.08,
       true, (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '0.5 N Sodium hydroxide VS'), 50, 1.0,
       0, ind."Id", ind."Name", false
FROM _ctx c
JOIN LATERAL (SELECT e."Id", e."Name" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Category" = 1 AND e."Name" LIKE 'Phenolphthalein%' ORDER BY e."Code" LIMIT 1) ind ON true
WHERE NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = 'TIT-ASPIRIN');

-- TIT-ASCORBIC: redox, direct, USP factor, 0.1 N iodine, F 88.06, blank off, visual starch
INSERT INTO "TestDefinitions"
  ("Code","DisplayName","SectionId","IsActive","WorkflowType","EquationType","RequiresSystemSuitability","PhyschemArea","ReplicateCount",
   "TitrationType","TitrationNonAqueous","TitrationMode","TitrationCalculation","TitrantSolutionMasterId","TitrationEquivalencyFactor",
   "TitrationBlankRequired","TitrationMaxRsdPercent","TitrationEndpoint","TitrationIndicatorEntryId","TitrationIndicator","TitrationTempCorrection")
SELECT 'TIT-ASCORBIC', 'Assay of ascorbic acid (iodimetric titration)', c.sec, true, 13, 14, false, 1, 3,
       1, NULL, 0, 0, (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '0.1 N Iodine VS'), 88.06,
       false, 1.0, 0, ind."Id", ind."Name", false
FROM _ctx c
JOIN LATERAL (SELECT e."Id", e."Name" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Category" = 1 AND e."Name" LIKE 'Starch%' ORDER BY e."Code" LIMIT 1) ind ON true
WHERE NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = 'TIT-ASCORBIC');

-- TIT-REL: redox, direct, relative to the ascorbic acid RS (no F), 0.1 N iodine, blank off, visual starch
INSERT INTO "TestDefinitions"
  ("Code","DisplayName","SectionId","IsActive","WorkflowType","EquationType","RequiresSystemSuitability","PhyschemArea","ReplicateCount",
   "TitrationType","TitrationNonAqueous","TitrationMode","TitrationCalculation","TitrantSolutionMasterId","TitrationEquivalencyFactor",
   "TitrationBlankRequired","TitrationMaxRsdPercent","TitrationEndpoint","TitrationIndicatorEntryId","TitrationIndicator","TitrationTempCorrection",
   "TitrationStandardEntryId")
SELECT 'TIT-REL', 'Assay of ascorbic acid vs RS (relative titration)', c.sec, true, 13, 14, false, 1, 3,
       1, NULL, 0, 1, (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '0.1 N Iodine VS'), NULL,
       false, 1.0, 0, ind."Id", ind."Name", false,
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" IN ('RS-009','RS-ASCORBIC') ORDER BY e."Code" LIMIT 1)
FROM _ctx c
JOIN LATERAL (SELECT e."Id", e."Name" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Category" = 1 AND e."Name" LIKE 'Starch%' ORDER BY e."Code" LIMIT 1) ind ON true
WHERE NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = 'TIT-REL');

------------------------------------------------------------------------------
-- 7. Assign tests to items and add the specifications (% as is, Range, inclusive; basis 3).
--    TIT-REL is also assigned to RM-ASCORBIC with its own spec (coordinator ruling: E2E due-titrant override).
------------------------------------------------------------------------------
INSERT INTO "SampleTests" ("ItemId","TestCode","DisplayName")
SELECT i."Id", t."Code", t."DisplayName"
FROM (VALUES ('RM-CITRIC','TIT-CITRIC'),('RM-ASPIRIN','TIT-ASPIRIN'),('RM-ASCORBIC','TIT-ASCORBIC'),('RM-ASCORBIC','TIT-REL')) AS v(icode,tcode)
JOIN "Items" i ON i."Code" = v.icode
JOIN "TestDefinitions" t ON t."Code" = v.tcode
WHERE NOT EXISTS (SELECT 1 FROM "SampleTests" st WHERE st."ItemId" = i."Id" AND st."TestCode" = t."Code");

INSERT INTO "Specifications"
  ("ItemId","TestCode","AlertLimit","ActionLimit","SpecLimit","Unit","ParameterName","DisplayOrder","LimitType",
   "LowerLimit","UpperLimit","LowerInclusive","UpperInclusive","ResultBasis")
SELECT i."Id", v.tcode, '', '', trim_scale(v.lo)::text || '-' || trim_scale(v.hi)::text, '%', 'Assay', 0, 0, v.lo, v.hi, true, true, 3
FROM (VALUES
 ('RM-CITRIC','TIT-CITRIC',99.5,100.5),
 ('RM-ASPIRIN','TIT-ASPIRIN',99.5,100.5),
 ('RM-ASCORBIC','TIT-ASCORBIC',99.0,100.5),
 ('RM-ASCORBIC','TIT-REL',99.0,100.5)
) AS v(icode,tcode,lo,hi)
JOIN "Items" i ON i."Code" = v.icode
WHERE NOT EXISTS (SELECT 1 FROM "Specifications" x WHERE x."ItemId" = i."Id" AND x."TestCode" = v.tcode AND x."ResultBasis" = 3);

------------------------------------------------------------------------------
-- Checks
------------------------------------------------------------------------------
SELECT m."Code", m."BatchNumber", m."MaterialType", m."Purity", m."ExpiryDate"::date AS expiry
FROM "Materials" m WHERE m."BatchNumber" IN ('KHP-PS-2610','RSASC-2610');

SELECT s."Name", s."NominalStrength", s."StandardizationMode", s."EquivalenceMgPerMl",
       (SELECT e."Code" FROM "MaterialMasterEntries" e WHERE e."Id" = s."StandardEntryId") AS std,
       (SELECT r."Name" FROM "SolutionMasters" r WHERE r."Id" = s."ReferenceSolutionId") AS ref
FROM "SolutionMasters" s WHERE s."Name" IN ('1 N Sodium hydroxide VS','0.5 N Sodium hydroxide VS','0.5 N Sulfuric acid VS') ORDER BY s."Id";

SELECT t."Code", t."WorkflowType", t."EquationType", t."PhyschemArea", t."TitrationType", t."TitrationMode", t."TitrationCalculation",
       (SELECT s."Name" FROM "SolutionMasters" s WHERE s."Id" = t."TitrantSolutionMasterId") AS titrant,
       t."TitrationEquivalencyFactor" AS f, t."TitrationBlankRequired" AS blank, t."ReplicateCount" AS reps, t."TitrationIndicator" AS indicator
FROM "TestDefinitions" t WHERE t."Code" LIKE 'TIT-%' ORDER BY t."Code";

SELECT i."Code" AS item, i."Category", st."TestCode", sp."SpecLimit", sp."ResultBasis", sp."LimitType"
FROM "Items" i
LEFT JOIN "SampleTests" st ON st."ItemId" = i."Id"
LEFT JOIN "Specifications" sp ON sp."ItemId" = i."Id" AND sp."TestCode" = st."TestCode"
WHERE i."Code" LIKE 'RM-%' ORDER BY i."Code";

COMMIT;
