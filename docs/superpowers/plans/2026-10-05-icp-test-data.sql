-- I4 ICP test data seed for the LOCAL dev database (LIMSV2). Spec: docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md §4.
-- Needs the HPLC chain seed (FP section, admin) and the Perkinelmer ICP-OES (Equipment code ICP-F-IL-F-11-069).
-- Idempotent: every insert is guarded by WHERE NOT EXISTS on its natural key.
-- Scenario (user's Syngistix report): Ca/Cu/Fe/Mg/Mn/Zn, levels 0.1/0.5/1/3/6 mg/L, min r 0.999 -> Zn fails (r 0.996383).
-- Osteocare Tablet needs Ca/Mg/Zn -> blocked on that run; Feroglobin Capsule needs Fe only -> can be sent for review.
-- TEST DATA: wavelengths/claims are typical values; the lab must use its own validated method.
-- Enum ints: IcpMethodMode MineralAssay0; AnalyteView Axial0/Radial1; MaterialMasterCategory ReferenceStandard2; MaterialType ReferenceStandard11;
-- ResultBasis PercentLabelClaim2; LimitType Range0; WorkflowType IcpMethodAssay14; EquationType IcpMethodAssay15.

BEGIN;

CREATE TEMP TABLE _ctx ON COMMIT DROP AS
SELECT (SELECT "Id" FROM "DocumentSections" WHERE "Code" = 'FP') AS sec,
       (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' AND "IsActive") AS uid,
       now() AS ts;

DO $$ BEGIN
  IF (SELECT sec FROM _ctx) IS NULL OR (SELECT uid FROM _ctx) IS NULL
     OR NOT EXISTS (SELECT 1 FROM "Equipment" WHERE "Code" = 'ICP-F-IL-F-11-069' AND "Type" = 9) THEN
    RAISE EXCEPTION 'FP section, admin user or ICP-OES instrument not found';
  END IF;
END $$;

-- 1. Calibration standard (multi-element) + one lot
INSERT INTO "MaterialMasterEntries"
  ("SectionId","Code","Name","Category","Grade","Source","BaseUnit","IsActive","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, 'RS-024', 'ICP multi-element calibration standard (Ca, Cu, Fe, Mg, Mn, Zn) 100 mg/L', 2, 'Certified reference material', 'Merck Certipur', 2, true, c.uid, c.ts, c.uid, c.ts
FROM _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = 'RS-024');

INSERT INTO "Materials"
  ("MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location",
   "QuantityReceived","QuantityRemaining","Unit","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt",
   "SectionId","MaterialMasterEntryId","Purity","MoisturePercent")
SELECT 11, e."Name", 'Merck', 'HC12345678', timestamptz '2026-09-01 00:00:00+00', timestamptz '2027-08-31 00:00:00+00',
       e."Code", 'ICP standards cabinet', 100, 100, 2, c.uid, c.ts, c.uid, c.ts, c.sec, e."Id", 100, 0
FROM _ctx c JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = 'RS-024'
WHERE NOT EXISTS (SELECT 1 FROM "Materials" m WHERE m."MaterialMasterEntryId" = e."Id" AND m."BatchNumber" = 'HC12345678');

-- 2. Method: 6 elements, report levels, min r 0.999, CCV 1 mg/L 90-110 %, blank/ICV off (D4 default)
INSERT INTO "IcpMethods"
  ("SectionId","Name","Abbreviation","EffectiveDate","IsActive","Mode","StandardLevelsMgPerL","CalibrationStandardEntryId","MinCorrelation",
   "RequireBlank","RequireIcv","RequireCcv","CcvNominalMgPerL","CcvRecoveryLowPercent","CcvRecoveryHighPercent","MaxCalibrationAgeHours",
   "SampleVolumeMl","DilutionFactor","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, 'Minerals (Ca, Cu, Fe, Mg, Mn, Zn) by ICP-OES', 'MIN6', timestamptz '2026-10-01 00:00:00+00', true, 0, '0.1, 0.5, 1, 3, 6',
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = 'RS-024'), 0.999,
       false, false, true, 1, 90, 110, 24, 50, 10, c.uid, c.ts, c.uid, c.ts
FROM _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "IcpMethods" m WHERE m."SectionId" = c.sec AND m."Abbreviation" = 'MIN6');

INSERT INTO "IcpMethodElements" ("IcpMethodId","DisplayOrder","Symbol","WavelengthNm","View","ConversionFactor")
SELECT m."Id", v.ord, v.sym, v.wl, v.vw, 1
FROM (VALUES (1,'Ca',317.933,1),(2,'Cu',327.393,0),(3,'Fe',238.204,0),(4,'Mg',285.213,1),(5,'Mn',257.610,0),(6,'Zn',206.200,0)) AS v(ord,sym,wl,vw)
CROSS JOIN _ctx c
JOIN "IcpMethods" m ON m."SectionId" = c.sec AND m."Abbreviation" = 'MIN6'
WHERE NOT EXISTS (SELECT 1 FROM "IcpMethodElements" x WHERE x."IcpMethodId" = m."Id" AND x."Symbol" = v.sym);

-- 3. Test master + stage replicates (Finished 2)
INSERT INTO "TestDefinitions"
  ("Code","DisplayName","SectionId","WorkflowType","EquationType","RequiresSystemSuitability","MethodAbbreviation","IsActive","IcpMethodId")
SELECT 'ICP-MIN6', 'Minerals assay (ICP-OES)', c.sec, 14, 15, false, 'MIN6', true, m."Id"
FROM _ctx c JOIN "IcpMethods" m ON m."SectionId" = c.sec AND m."Abbreviation" = 'MIN6'
WHERE NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = 'ICP-MIN6');

INSERT INTO "TestDefinitionStageReplicates" ("TestDefinitionId","Role","SampleReplicates")
SELECT t."Id", 3, 2 FROM "TestDefinitions" t
WHERE t."Code" = 'ICP-MIN6'
  AND NOT EXISTS (SELECT 1 FROM "TestDefinitionStageReplicates" x WHERE x."TestDefinitionId" = t."Id" AND x."Role" = 3);

-- 4. Items: Osteocare Tablet (Ca/Mg/Zn) and Feroglobin Capsule B12 (Fe)
INSERT INTO "SampleTests" ("ItemId","TestCode","DisplayName")
SELECT i."Id", 'ICP-MIN6', 'Minerals assay (ICP-OES)'
FROM "Items" i WHERE i."Code" IN ('ost.tab','Fero.caps')
  AND NOT EXISTS (SELECT 1 FROM "SampleTests" st WHERE st."ItemId" = i."Id" AND st."TestCode" = 'ICP-MIN6');

-- 5. Specs: % of label claim 90-110 with the label claim on the row (I1 ruling)
WITH src(icode, sym, claim) AS (VALUES
 ('ost.tab','Ca',400.0), ('ost.tab','Mg',150.0), ('ost.tab','Zn',5.0),
 ('Fero.caps','Fe',17.0)
)
INSERT INTO "Specifications"
  ("ItemId","TestCode","AlertLimit","ActionLimit","SpecLimit","Unit","ParameterName","DisplayOrder","LimitType",
   "LowerLimit","UpperLimit","LowerInclusive","UpperInclusive","ResultBasis","LabelClaim","LabelClaimUnit","ConversionFactor","IcpMethodElementId")
SELECT i."Id", 'ICP-MIN6', '', '', '90-110', '%', s.sym, (e."DisplayOrder" - 1), 0, 90, 110, true, true, 2, s.claim, 'mg', 1.0, e."Id"
FROM src s
JOIN "Items" i ON i."Code" = s.icode
JOIN "IcpMethods" m ON m."Abbreviation" = 'MIN6'
JOIN "IcpMethodElements" e ON e."IcpMethodId" = m."Id" AND e."Symbol" = s.sym
WHERE NOT EXISTS (SELECT 1 FROM "Specifications" x WHERE x."ItemId" = i."Id" AND x."TestCode" = 'ICP-MIN6' AND x."IcpMethodElementId" = e."Id" AND x."ResultBasis" = 2);

-- 6. Remove the I1 browser-check leftovers on Osteocare Tablet (test ICP-MIN assignment + its Zn spec), so a sample
--    of that item doesn't also get the I1 test.
DELETE FROM "Specifications" WHERE "TestCode" = 'ICP-MIN' AND "ItemId" = (SELECT "Id" FROM "Items" WHERE "Code" = 'ost.tab');
DELETE FROM "SampleTests" WHERE "TestCode" = 'ICP-MIN' AND "ItemId" = (SELECT "Id" FROM "Items" WHERE "Code" = 'ost.tab');

COMMIT;
