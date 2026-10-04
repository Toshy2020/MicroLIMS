-- G3 GC test data seed for the LOCAL dev database (LIMSV2). Spec: docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md
-- Pattern copied from 2026-09-30-hplc-chain-test-data.sql (needs that seed: FP section, admin, RG-002/RG-003, IBU-400).
-- Idempotent: every insert is guarded by WHERE NOT EXISTS on its natural key.
-- Parameters follow USP <467> (Class 2, headspace, G43) in outline only. TEST DATA: the lab must check them
-- against its current official USP-NF before GMP use.
-- Residual solvents: sample W = 250 mg in V = 5 mL water, standard at the limit -> C_std = limit_ppm * 0.25 / 5.
-- Enum ints: Technique Hplc0/Gc1; ResultMode Assay0/ResidualSolvents1; CarrierGas Helium0/Nitrogen1/Hydrogen2;
-- DetectorType Fid6; LimitType NotMoreThan1 / Range0; ResultBasis PercentLabelClaim2 / Ppm6;
-- WorkflowType HplcMethodAssay12; EquationType HplcMethodAssay13.

BEGIN;

CREATE TEMP TABLE _ctx ON COMMIT DROP AS
SELECT (SELECT "Id" FROM "DocumentSections" WHERE "Code" = 'FP') AS sec,
       (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' AND "IsActive") AS uid,
       (SELECT "Id" FROM "Equipment" WHERE "Code" = 'GCS-F-IL-F-11-068' AND "Type" = 22) AS gc,
       now() AS ts;

DO $$ BEGIN
  IF (SELECT sec FROM _ctx) IS NULL OR (SELECT uid FROM _ctx) IS NULL OR (SELECT gc FROM _ctx) IS NULL THEN
    RAISE EXCEPTION 'FP section, admin user or Shimadzu GC not found';
  END IF;
END $$;

-- 1. Reference standards (master + one lot each)
INSERT INTO "MaterialMasterEntries"
  ("SectionId","Code","Name","Category","Grade","Source","BaseUnit","IsActive",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.code, v.name, 2, 'Primary reference standard', 'USP', 0, true, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('RS-019','Methanol RS'),
 ('RS-020','Acetonitrile RS'),
 ('RS-021','Methylene Chloride RS'),
 ('RS-022','Toluene RS'),
 ('RS-023','Menthol RS')
) AS v(code,name)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v.code);

INSERT INTO "Materials"
  ("MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location",
   "QuantityReceived","QuantityRemaining","Unit","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt",
   "SectionId","MaterialMasterEntryId","Purity","MoisturePercent")
SELECT 11, e."Name", 'USP', v.batch, timestamptz '2026-09-01 00:00:00+00', timestamptz '2027-12-31 00:00:00+00',
       e."Code", 'RS fridge 2-8 C', 1, 1, 0, c.uid, c.ts, c.uid, c.ts, c.sec, e."Id", v.purity, v.mc
FROM (VALUES
 ('RS-019','R22010',99.9,0.0),
 ('RS-020','R22020',99.9,0.0),
 ('RS-021','R22030',99.8,0.0),
 ('RS-022','R22040',99.9,0.0),
 ('RS-023','R22050',99.5,0.1)
) AS v(code,batch,purity,mc)
CROSS JOIN _ctx c
JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = v.code
WHERE NOT EXISTS (SELECT 1 FROM "Materials" m WHERE m."MaterialMasterEntryId" = e."Id" AND m."BatchNumber" = v.batch);

-- 2. GC columns (G-designation) on the Shimadzu GC
INSERT INTO "ChromatographyColumns"
  ("Code","Name","SerialNumber","SectionId","IsActive","UspDesignation","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT v.code, v.name, v.sn, c.sec, true, v.des, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('COL-G43-01','DB-624 30 m x 0.32 mm, 1.8 um (USP G43)','SN-G43-0001','G43'),
 ('COL-G16-01','DB-WAX 30 m x 0.32 mm, 0.25 um (USP G16)','SN-G16-0001','G16')
) AS v(code,name,sn,des)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "ChromatographyColumns" x WHERE x."Code" = v.code);

INSERT INTO "ChromatographyColumnEquipment" ("CompatibleColumnsId","CompatibleEquipmentId")
SELECT x."Id", c.gc
FROM "ChromatographyColumns" x CROSS JOIN _ctx c
WHERE x."Code" IN ('COL-G43-01','COL-G16-01')
  AND NOT EXISTS (SELECT 1 FROM "ChromatographyColumnEquipment" l WHERE l."CompatibleColumnsId" = x."Id" AND l."CompatibleEquipmentId" = c.gc);

-- 3. Diluents
INSERT INTO "SolutionMasters"
  ("SectionId","Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","Instructions","IsActive","BlankRequired",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.name, 1, 7, 1, 'Room temperature, tightly closed', 1000, v.instr, true, false, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('Residual solvents diluent (water)','HPLC water, used as is for headspace vials.'),
 ('Menthol diluent (methanol)','Methanol, used as is.')
) AS v(name,instr)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v.name);

INSERT INTO "SolutionComponents" ("SolutionMasterId","Order","MaterialMasterEntryId","Quantity","Unit")
SELECT s."Id", 1, e."Id", 1000, 2
FROM (VALUES ('Residual solvents diluent (water)','RG-003'), ('Menthol diluent (methanol)','RG-002')) AS v(sname,rg)
CROSS JOIN _ctx c
JOIN "SolutionMasters" s ON s."SectionId" = c.sec AND s."Name" = v.sname
JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = v.rg
WHERE NOT EXISTS (SELECT 1 FROM "SolutionComponents" x WHERE x."SolutionMasterId" = s."Id" AND x."Order" = 1);

-- 4. GC methods
INSERT INTO "HplcMethods"
  ("SectionId","Name","Abbreviation","EffectiveDate","IsActive","Technique","ResultMode",
   "ColumnDesignation","ColumnLengthMm","ColumnInternalDiameterMm","FilmThicknessUm","ColumnBrand",
   "ElutionMode","FlowRateMlPerMin","DetectorType","InjectionVolumeUl","RunTimeMin","DiluentSolutionId",
   "CarrierGas","SplitRatio","InletTemperatureC","DetectorTemperatureC",
   "HeadspaceEnabled","HeadspaceEquilibrationTemperatureC","HeadspaceEquilibrationMin","HeadspaceTransferLineTemperatureC",
   "SampleSolutionVolumeMl","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.name, v.abbr, timestamptz '2026-10-01 00:00:00+00', true, 1, v.mode,
       v.des, 30000, 0.32, v.film, v.brand, 0, v.flow, 6, v.inj, v.run,
       (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v.dil AND s."Type" = 1),
       0, v.split, v.inlet, v.det, v.hs, v.hst, v.hsmin, v.hstl, v.vol, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('Residual Solvents Class 2 (headspace GC-FID)','RS-C2',1,'G43',1.8,'DB-624',2.0,1000.0,60,5,140,250,
  true,80,60,85,5,'Residual solvents diluent (water)'),
 ('Menthol Lozenges Assay (GC-FID)','MENTHOL',0,'G16',0.25,'DB-WAX',1.5,1.0,20,50,250,280,
  false,NULL,NULL,NULL,NULL,'Menthol diluent (methanol)')
) AS v(name,abbr,mode,des,film,brand,flow,inj,run,split,inlet,det,hs,hst,hsmin,hstl,vol,dil)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethods" m WHERE m."SectionId" = c.sec AND m."Abbreviation" = v.abbr);

INSERT INTO "HplcMethodOvenSteps" ("HplcMethodId","StepNo","RateCPerMin","TemperatureC","HoldMin")
SELECT m."Id", v.step, v.rate, v.temp, v.hold
FROM (VALUES
 ('RS-C2',1,NULL,40,20), ('RS-C2',2,10,240,20),
 ('MENTHOL',1,NULL,100,2), ('MENTHOL',2,10,200,8)
) AS v(abbr,step,rate,temp,hold)
CROSS JOIN _ctx c
JOIN "HplcMethods" m ON m."SectionId" = c.sec AND m."Abbreviation" = v.abbr
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethodOvenSteps" o WHERE o."HplcMethodId" = m."Id" AND o."StepNo" = v.step);

-- Analytes: residual solvents carry C_std (no Th.Wt); the assay method carries Th.Wt.
INSERT INTO "HplcMethodAnalytes"
  ("HplcMethodId","DisplayOrder","Name","WavelengthNm","StandardEntryId","TheoreticalWeightStdMg","TheoreticalWeightTestMg",
   "StandardInjections","SstMaxRsdPercent","SstMinResolution","StandardConcentrationUgPerMl")
SELECT m."Id", v.ord, v.aname, NULL, e."Id", v.stdwt, v.testwt, v.inj, v.rsd, v.res::numeric, v.cstd::numeric
FROM (VALUES
 ('RS-C2',1,'Methanol','RS-019',0,0,3,15.0,NULL,150.0),
 ('RS-C2',2,'Acetonitrile','RS-020',0,0,3,15.0,1.0,20.5),
 ('RS-C2',3,'Methylene chloride','RS-021',0,0,3,15.0,NULL,30.0),
 ('RS-C2',4,'Toluene','RS-022',0,0,3,15.0,NULL,44.5),
 ('MENTHOL',1,'Menthol','RS-023',50,500,5,2.0,NULL,NULL)
) AS v(abbr,ord,aname,std,stdwt,testwt,inj,rsd,res,cstd)
CROSS JOIN _ctx c
JOIN "HplcMethods" m ON m."SectionId" = c.sec AND m."Abbreviation" = v.abbr
JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = v.std
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethodAnalytes" a WHERE a."HplcMethodId" = m."Id" AND a."Name" = v.aname);

-- 5. Test master (one per method) + stage replicates (Finished 2, Bulk 3)
INSERT INTO "TestDefinitions"
  ("Code","DisplayName","SectionId","WorkflowType","EquationType","RequiresSystemSuitability","MethodAbbreviation",
   "IsActive","CalMaxRunAgeHours","HplcMethodId")
SELECT 'GC-' || m."Abbreviation", m."Name", c.sec, 12, 13, true, m."Abbreviation", true, 24, m."Id"
FROM "HplcMethods" m CROSS JOIN _ctx c
WHERE m."SectionId" = c.sec AND m."Abbreviation" IN ('RS-C2','MENTHOL')
  AND NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = 'GC-' || m."Abbreviation");

INSERT INTO "TestDefinitionStageReplicates" ("TestDefinitionId","Role","SampleReplicates")
SELECT t."Id", r.role, r.smp
FROM "TestDefinitions" t CROSS JOIN (VALUES (3,2),(1,3)) AS r(role,smp)
WHERE t."Code" IN ('GC-RS-C2','GC-MENTHOL')
  AND NOT EXISTS (SELECT 1 FROM "TestDefinitionStageReplicates" x WHERE x."TestDefinitionId" = t."Id" AND x."Role" = r.role);

-- 6. Items: residual solvents on the existing Ibuprofen tablets; new menthol lozenges.
INSERT INTO "Items" ("Name","Code","Category","SopNumber","IsActive")
SELECT 'Menthol Lozenges 10 mg', 'MENTHOL-10', 0, 'USP-NF', true
WHERE NOT EXISTS (SELECT 1 FROM "Items" i WHERE i."Code" = 'MENTHOL-10');

INSERT INTO "SampleTests" ("ItemId","TestCode","DisplayName")
SELECT i."Id", t."Code", t."DisplayName"
FROM (VALUES ('IBU-400','GC-RS-C2'), ('MENTHOL-10','GC-MENTHOL')) AS v(icode,tcode)
JOIN "Items" i ON i."Code" = v.icode
JOIN "TestDefinitions" t ON t."Code" = v.tcode
WHERE NOT EXISTS (SELECT 1 FROM "SampleTests" st WHERE st."ItemId" = i."Id" AND st."TestCode" = t."Code");

-- 7. Specifications: ppm NMT per solvent (USP <467> Class 2 limits); menthol assay % 90-110.
WITH src(icode, tcode, aname, lt, lo, hi, unit, basis, speclimit) AS (VALUES
 ('IBU-400','GC-RS-C2','Methanol',1,NULL,3000.0,'ppm',6,'NMT 3000'),
 ('IBU-400','GC-RS-C2','Acetonitrile',1,NULL,410.0,'ppm',6,'NMT 410'),
 ('IBU-400','GC-RS-C2','Methylene chloride',1,NULL,600.0,'ppm',6,'NMT 600'),
 ('IBU-400','GC-RS-C2','Toluene',1,NULL,890.0,'ppm',6,'NMT 890'),
 ('MENTHOL-10','GC-MENTHOL','Menthol',0,90.0,110.0,'%',2,'90-110')
)
INSERT INTO "Specifications"
  ("ItemId","TestCode","AlertLimit","ActionLimit","SpecLimit","Unit","ParameterName","DisplayOrder","LimitType",
   "LowerLimit","UpperLimit","LowerInclusive","UpperInclusive","ResultBasis","ConversionFactor","HplcMethodAnalyteId")
SELECT i."Id", t."Code", '', '', s.speclimit, s.unit, s.aname, (a."DisplayOrder" - 1), s.lt,
       s.lo::numeric, s.hi, true, true, s.basis, 1.0, a."Id"
FROM src s
JOIN "Items" i ON i."Code" = s.icode
JOIN "TestDefinitions" t ON t."Code" = s.tcode
JOIN "HplcMethodAnalytes" a ON a."HplcMethodId" = t."HplcMethodId" AND a."Name" = s.aname
WHERE NOT EXISTS (SELECT 1 FROM "Specifications" x
                  WHERE x."ItemId" = i."Id" AND x."TestCode" = t."Code" AND x."HplcMethodAnalyteId" = a."Id" AND x."ResultBasis" = s.basis);

COMMIT;
