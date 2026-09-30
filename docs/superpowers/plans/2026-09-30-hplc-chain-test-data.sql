-- HPLC chain USP-based test data seed for the LOCAL dev database (LIMSV2).
-- Spec: docs/superpowers/plans/2026-09-30-hplc-chain-test-data.md
-- Idempotent: every insert is guarded by WHERE NOT EXISTS on its natural key; re-running changes nothing.
-- Rules copied from MaterialMasterService, SolutionMasterService, HplcMethodService, ChromatographyColumnService,
-- MaterialService, TestDefinitionMasterDataService, TestStageReplicateMasterDataService, SpecificationService, ItemService.
-- Enum ints: SolutionType MobilePhase0/Diluent1/Titrant2; StandardizationMode Primary0/AgainstVS1; ShelfLifeUnit Hours0/Days1;
-- MaterialMasterCategory Reagent0/Indicator1/ReferenceStandard2; MaterialUnit Gram0 Milliliter2 Liter3 Bottle8;
-- MaterialType Chemical6/Indicator7/ReferenceStandard11; SolutionComponentUnit Gram0 Milliliter2; LimitType Range0;
-- ResultBasis MgPerUnit1/PercentLabelClaim2; WorkflowType HplcMethodAssay12; EquationType HplcMethodAssay13;
-- ProductionStageRole Bulk1/Finished3; SampleCategory FinishedProduct0.
BEGIN;

CREATE TEMP TABLE _ctx ON COMMIT DROP AS
SELECT (SELECT "Id" FROM "DocumentSections" WHERE "Code" = 'FP') AS sec,
       (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' AND "IsActive") AS uid,
       now() AS ts;

-- sanity: refuse to run without the FP section and the admin user
DO $$ BEGIN
  IF (SELECT sec FROM _ctx) IS NULL OR (SELECT uid FROM _ctx) IS NULL THEN
    RAISE EXCEPTION 'FP section or admin user not found';
  END IF;
END $$;

------------------------------------------------------------------------------
-- 1. Material master: reagents
------------------------------------------------------------------------------
INSERT INTO "MaterialMasterEntries"
  ("SectionId","Code","Name","Category","Grade","Source","BaseUnit","IsActive",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.code, v.name, 0, v.grade, NULL, v.unit, true, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('RG-001','Acetonitrile','HPLC grade',3),
 ('RG-002','Methanol','HPLC grade',3),
 ('RG-003','Water, HPLC','HPLC grade (Milli-Q)',3),
 ('RG-004','Monobasic potassium phosphate (KH2PO4)','ACS reagent',0),
 ('RG-005','Phosphoric acid 85%','ACS reagent',3),
 ('RG-006','Glacial acetic acid','ACS reagent',3),
 ('RG-007','Potassium hydroxide (45% w/v solution)','ACS reagent',3),
 ('RG-008','Chloroacetic acid','ACS reagent',0),
 ('RG-009','Ammonium hydroxide 28%','ACS reagent',3),
 ('RG-010','Monobasic sodium phosphate (NaH2PO4.H2O)','ACS reagent',0),
 ('RG-011','Sodium hydroxide pellets','ACS reagent',0),
 ('RG-012','Hydrochloric acid 37%','ACS reagent',3),
 ('RG-013','Perchloric acid 70%','ACS reagent',3),
 ('RG-014','Acetic anhydride','ACS reagent',3),
 ('RG-015','Edetate disodium (EDTA-Na2.2H2O)','ACS reagent',0),
 ('RG-016','Sodium thiosulfate pentahydrate','ACS reagent',0),
 ('RG-017','Silver nitrate','ACS reagent',0),
 ('RG-018','Iodine','ACS reagent',0),
 ('RG-019','Potassium iodide','ACS reagent',0),
 ('RG-020','Potassium permanganate','ACS reagent',0),
 ('RG-021','Sodium carbonate, anhydrous','ACS reagent',0),
 ('RG-022','Potassium biphthalate (KHP)','Primary standard',0),
 ('RG-023','Tromethamine','Primary standard',0),
 ('RG-024','Calcium carbonate (chelometric standard)','Primary standard',0),
 ('RG-025','Potassium dichromate','Primary standard',0),
 ('RG-026','Sodium chloride','Primary standard',0),
 ('RG-027','Sodium oxalate','Primary standard',0),
 ('RG-028','Sodium 1-heptanesulfonate','HPLC grade',0),
 ('RG-029','Sodium hydroxide 10 N (pH adjustment)','ACS reagent',3),
 ('RG-030','Sodium perchlorate','ACS reagent',0),
 ('RG-031','Sodium 1-hexanesulfonate','HPLC grade',0),
 ('RG-032','n-Hexane (HPLC)','HPLC grade',3),
 ('RG-033','1-Pentanol (n-amyl alcohol)','ACS reagent',3),
 ('RG-034','Toluene (HPLC)','HPLC grade',3),
 ('RG-035','Benzoic acid','ACS reagent',0),
 ('RG-036','Dibasic sodium phosphate (Na2HPO4)','ACS reagent',0),
 ('RG-037','Diethyl phthalate','ACS reagent',3)
) AS v(code,name,grade,unit)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v.code);

------------------------------------------------------------------------------
-- 1b. Material master: indicators
------------------------------------------------------------------------------
INSERT INTO "MaterialMasterEntries"
  ("SectionId","Code","Name","Category","Grade","Source","BaseUnit","IsActive",
   "WorkingConcentration","Solvent","TransitionRangeFrom","TransitionRangeTo","ColourChange","IndicatorUse",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.code, v.name, 1, 'Indicator grade', NULL, v.unit, true,
       v.conc, v.solvent, v.tfrom::numeric, v.tto::numeric, v.colour, v.usage,
       c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('IN-001','Phenolphthalein TS','1 g in 100 mL','Alcohol',8.2,10.0,'Colourless to pink','Acid-base (NaOH VS)',8),
 ('IN-002','Methyl orange TS','0.1 g in 100 mL','Water',3.1,4.4,'Red to yellow','Acid-base',8),
 ('IN-003','Bromocresol green TS','0.05 g in 100 mL','Alcohol',3.8,5.4,'Yellow to blue','HCl VS vs tromethamine',8),
 ('IN-004','Crystal violet TS','0.5 g in 100 mL','Glacial acetic acid',NULL,NULL,'Violet to blue-green','Non-aqueous (HClO4 VS)',8),
 ('IN-005','Starch TS','1 g in 100 mL','Water',NULL,NULL,'Blue to colourless','Iodometry',8),
 ('IN-006','Hydroxy naphthol blue','Triturate',NULL,12.0,13.0,'Red to blue','Complexometry (EDTA VS)',0),
 ('IN-007','Eosin Y TS','50 mg in 10 mL','Water',NULL,NULL,'Pink on precipitate','Argentometry (AgNO3 VS)',8),
 ('IN-008','Potassium chromate TS','1 g in 20 mL','Water',NULL,NULL,'Yellow to red-brown','Argentometry (Mohr)',8),
 ('IN-009','Eriochrome black T','Triturate 1 in 100 NaCl',NULL,10.0,10.0,'Wine red to blue','Complexometry',0)
) AS v(code,name,conc,solvent,tfrom,tto,colour,usage,unit)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v.code);

------------------------------------------------------------------------------
-- 1c. Material master: reference standards
------------------------------------------------------------------------------
INSERT INTO "MaterialMasterEntries"
  ("SectionId","Code","Name","Category","Grade","Source","BaseUnit","IsActive",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.code, v.name, 2, 'Primary reference standard', v.source, 0, true, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('RS-001','Acetaminophen RS','USP'),
 ('RS-002','Caffeine RS','USP'),
 ('RS-003','Amoxicillin RS','USP'),
 ('RS-004','Diclofenac Sodium RS','USP'),
 ('RS-005','Ibuprofen RS','USP'),
 ('RS-006','Metformin Hydrochloride RS','USP'),
 ('RS-007','Diclofenac Related Compound A RS','USP (SST)'),
 ('RS-008','Valerophenone','USP reagent (SST / internal std)'),
 ('RS-009','Ascorbic Acid RS','USP'),
 ('RS-010','Folic Acid RS','USP'),
 ('RS-011','Cyanocobalamin RS','USP'),
 ('RS-012','Cholecalciferol RS','USP'),
 ('RS-013','Thiamine Hydrochloride RS','USP'),
 ('RS-014','Riboflavin RS','USP'),
 ('RS-015','Niacinamide RS','USP'),
 ('RS-016','Pyridoxine Hydrochloride RS','USP'),
 ('RS-017','Folic Acid Related Compound A RS','USP (SST)'),
 ('RS-018','USP Vitamin D Assay System Suitability RS','USP (SST)')
) AS v(code,name,source)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v.code);

------------------------------------------------------------------------------
-- 2. Stock lots (Materials). Code/name copied from the master entry (MaterialService.CreateAsync).
--    Reagent / indicator lots: one lot each. RS lots: purity + moisture. Natural key: entry + batch.
------------------------------------------------------------------------------
INSERT INTO "Materials"
  ("MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location",
   "QuantityReceived","QuantityRemaining","Unit","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt",
   "SectionId","MaterialMasterEntryId")
SELECT CASE e."Category" WHEN 1 THEN 7 ELSE 6 END,
       e."Name",
       (ARRAY['Merck','Sigma-Aldrich','Fisher Scientific'])[1 + (substring(e."Code" from 4)::int % 3)],
       'FP26-' || e."Code",
       timestamptz '2026-09-01 00:00:00+00',
       CASE e."Category" WHEN 1 THEN timestamptz '2027-08-31 00:00:00+00' ELSE timestamptz '2028-08-31 00:00:00+00' END,
       e."Code", 'FP chemical store',
       CASE e."BaseUnit" WHEN 3 THEN 2.5 WHEN 8 THEN 2 ELSE CASE e."Category" WHEN 1 THEN 25 ELSE 500 END END,
       CASE e."BaseUnit" WHEN 3 THEN 2.5 WHEN 8 THEN 2 ELSE CASE e."Category" WHEN 1 THEN 25 ELSE 500 END END,
       e."BaseUnit", c.uid, c.ts, c.uid, c.ts, c.sec, e."Id"
FROM "MaterialMasterEntries" e
CROSS JOIN _ctx c
WHERE e."SectionId" = c.sec AND e."Category" IN (0,1) AND (e."Code" LIKE 'RG-%' OR e."Code" LIKE 'IN-%')
  AND NOT EXISTS (SELECT 1 FROM "Materials" m WHERE m."MaterialMasterEntryId" = e."Id" AND m."BatchNumber" = 'FP26-' || e."Code");

INSERT INTO "Materials"
  ("MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location",
   "QuantityReceived","QuantityRemaining","Unit","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt",
   "SectionId","MaterialMasterEntryId","Purity","MoisturePercent")
SELECT 11, e."Name", 'USP', v.batch, timestamptz '2026-09-01 00:00:00+00', timestamptz '2027-12-31 00:00:00+00',
       e."Code", 'RS fridge 2-8 C', 0.2, 0.2, 0, c.uid, c.ts, c.uid, c.ts, c.sec, e."Id", v.purity, v.mc
FROM (VALUES
 ('RS-001','R10360',99.8,0.1),
 ('RS-002','R10340',99.9,0.2),
 ('RS-003','R11020',86.9,13.2),
 ('RS-004','R08790',99.6,0.3),
 ('RS-005','R09860',99.7,0.1),
 ('RS-006','R10610',99.9,0.1),
 ('RS-007','R05920',98.5,0.5),
 ('RS-008','R02100',99.0,0.0),
 ('RS-009','R12010',99.8,0.1),
 ('RS-010','R13050',97.5,8.0),
 ('RS-011','R14020',97.0,6.5),
 ('RS-012','R15030',99.0,0.1),
 ('RS-013','R16040',99.6,0.3),
 ('RS-014','R17050',98.5,1.2),
 ('RS-015','R18060',99.9,0.1),
 ('RS-016','R19070',99.8,0.1),
 ('RS-017','R20080',96.0,5.0),
 ('RS-018','R21090',100.0,0.0)
) AS v(code,batch,purity,mc)
CROSS JOIN _ctx c
JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = v.code
WHERE NOT EXISTS (SELECT 1 FROM "Materials" m WHERE m."MaterialMasterEntryId" = e."Id" AND m."BatchNumber" = v.batch);

------------------------------------------------------------------------------
-- 3. Chromatography columns (USP L-designation in the name) + compatibility with Agilent HPLC
------------------------------------------------------------------------------
INSERT INTO "ChromatographyColumns"
  ("Code","Name","SerialNumber","SectionId","IsActive","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT v.code, v.name, v.sn, c.sec, true, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('COL-L1-02','Zorbax Eclipse XDB-C18 5um 4.6x150mm (USP L1)','SN-L1-0002'),
 ('COL-L1-03','Inertsil ODS-3 5um 4.6x250mm (USP L1)','SN-L1-0003'),
 ('COL-L1-04','Waters Symmetry C18 5um 3.9x300mm (USP L1)','SN-L1-0004'),
 ('COL-L1-05','Phenomenex Luna C18(2) 5um 4.6x250mm (USP L1)','SN-L1-0005'),
 ('COL-L7-01','Zorbax Eclipse XDB-C8 5um 4.6x150mm (USP L7)','SN-L7-0001'),
 ('COL-L7-02','Inertsil C8-3 5um 4.6x250mm (USP L7)','SN-L7-0002'),
 ('COL-L9-01','Partisil SCX 10um 4.6x250mm (USP L9)','SN-L9-0001'),
 ('COL-L10-01','Zorbax SB-CN 5um 4.6x250mm (USP L10)','SN-L10-0001'),
 ('COL-L11-01','Phenomenex Luna Phenyl-Hexyl 5um 4.6x250mm (USP L11)','SN-L11-0001'),
 ('COL-L3-01','Zorbax Rx-SIL 5um 4.6x250mm (USP L3)','SN-L3-0001'),
 ('COL-L1-06','Zorbax Eclipse XDB-C18 5um 4.6x100mm (USP L1)','SN-L1-0006'),
 ('COL-L39-01','Polymeric L39 column 5um 6.0x150mm (USP L39, supplier model to be chosen)','SN-L39-0001')
) AS v(code,name,sn)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "ChromatographyColumns" x WHERE x."Code" = v.code);

INSERT INTO "ChromatographyColumnEquipment" ("CompatibleColumnsId","CompatibleEquipmentId")
SELECT x."Id", e."Id"
FROM "ChromatographyColumns" x
JOIN "Equipment" e ON e."Code" = 'HPC-F-IL-F-08-028'
WHERE x."Code" IN ('COL-L1-02','COL-L1-03','COL-L1-04','COL-L1-05','COL-L7-01','COL-L7-02','COL-L9-01','COL-L10-01',
                   'COL-L11-01','COL-L3-01','COL-L1-06','COL-L39-01')
  AND NOT EXISTS (SELECT 1 FROM "ChromatographyColumnEquipment" l
                  WHERE l."CompatibleColumnsId" = x."Id" AND l."CompatibleEquipmentId" = e."Id");

------------------------------------------------------------------------------
-- 4. Solution master: mobile phases and diluents (types 0 / 1)
------------------------------------------------------------------------------
INSERT INTO "SolutionMasters"
  ("SectionId","Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","PhTarget","PhTolerance",
   "PhAdjustingEntryId","Instructions","IsActive","BlankRequired",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.name, v.type, v.shelf, v.shelfunit, v.storage, v.vol, v.ph::numeric, v.phtol::numeric,
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v.adj),
       v.instr, true, false, c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('APAP mobile phase (water-methanol 3:1)',0,7,1,'Room temperature, tightly closed',1000,NULL,NULL,NULL,
  'Mix 750 mL HPLC water with 250 mL methanol. Filter and degas.'),
 ('APAP diluent (water-methanol 3:1)',1,3,1,'Room temperature, tightly closed',1000,NULL,NULL,NULL,
  'Same composition as the APAP mobile phase: 750 mL HPLC water and 250 mL methanol.'),
 ('APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)',0,7,1,'Room temperature, tightly closed',1000,NULL,NULL,NULL,
  'Mix 690 mL HPLC water, 280 mL methanol and 30 mL glacial acetic acid. Filter and degas.'),
 ('APAP-CAF solvent mixture (methanol-acetic acid 95:5)',1,3,1,'Room temperature, tightly closed',1000,NULL,NULL,NULL,
  'Mix 950 mL methanol with 50 mL glacial acetic acid. (USP internal standard solution, benzoic acid in methanol, is not modelled: no internal standard in the system.)'),
 ('Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)',0,3,1,'Room temperature, tightly closed',1000,5.0,0.1,'RG-007',
  'Dissolve 6.528 g monobasic potassium phosphate in 960 mL HPLC water, adjust to pH 5.0 +/- 0.1 with 45% KOH, add 40 mL acetonitrile. Filter and degas.'),
 ('Amoxicillin buffer pH 5.0',1,3,1,'Room temperature, tightly closed',1000,5.0,0.1,'RG-007',
  'Dissolve 6.8 g monobasic potassium phosphate in 1000 mL HPLC water; adjust to pH 5.0 +/- 0.1 with 45% KOH. Standard solution to be used within 6 h.'),
 ('Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)',0,3,1,'Room temperature, tightly closed',1000,2.5,0.2,'RG-005',
  'Solution A: equal volumes of 0.01 M phosphoric acid and 0.01 M monobasic sodium phosphate, pH 2.5 +/- 0.2. Mobile phase: 700 mL methanol and 300 mL Solution A. Filter and degas.'),
 ('Diclofenac diluent (methanol-water 7:3)',1,3,1,'Room temperature, tightly closed',1000,NULL,NULL,NULL,
  'Mix 700 mL methanol with 300 mL HPLC water.'),
 ('Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)',0,3,1,'Room temperature, tightly closed',1000,3.0,0.1,'RG-009',
  'Dissolve 4.0 g chloroacetic acid in 400 mL HPLC water, adjust to pH 3.0 with ammonium hydroxide, add 600 mL acetonitrile. Filter and degas.'),
 ('Ibuprofen internal standard diluent (valerophenone in mobile phase)',1,3,1,'Room temperature, tightly closed',1000,3.0,0.1,'RG-009',
  'Ibuprofen mobile phase containing valerophenone about 0.35 mg/mL (USP internal standard solution). The system has no internal standard: kept as the diluent only.'),
 ('Ascorbic acid mobile phase (phosphate pH 2.5)',0,3,1,'Room temperature, tightly closed',1000,2.5,0.05,'RG-005',
  'Dissolve 7.8 g dibasic sodium phosphate and 6.1 g monobasic potassium phosphate in 1000 mL HPLC water; adjust to pH 2.5 +/- 0.05 with phosphoric acid. Filter and degas.'),
 ('Ascorbic acid diluent (mobile phase)',1,24,0,'2-8 C, protected from light',1000,2.5,0.05,'RG-005',
  'Same composition as the ascorbic acid mobile phase. Keep refrigerated and protected from light; use within 3 h after removal from the refrigerator.'),
 ('Folic acid mobile phase (perchlorate-phosphate pH 7.2)',0,3,1,'Room temperature, tightly closed',1000,7.2,0.1,'RG-007',
  'In a 1-L flask dissolve 35.1 g sodium perchlorate and 1.40 g monobasic potassium phosphate in about 900 mL HPLC water, add 7.0 mL 1 N KOH (0.873 mL of 45% KOH diluted) and 40 mL methanol, dilute to 1000 mL, adjust to pH 7.2 with 1 N KOH or phosphoric acid.'),
 ('Folic acid diluent (ammonium hydroxide-perchlorate)',1,3,1,'Room temperature, protected from light',100,NULL,NULL,NULL,
  'Mix 2 mL ammonium hydroxide and 1 g sodium perchlorate in HPLC water to 100 mL.'),
 ('Cyanocobalamin mobile phase (water-methanol 65:35)',0,3,1,'Room temperature, tightly closed',1000,NULL,NULL,NULL,
  'Mix 650 mL HPLC water with 350 mL methanol. Filter and degas.'),
 ('Cyanocobalamin diluent (water)',1,3,1,'Room temperature, protected from light',1000,NULL,NULL,NULL,
  'HPLC water in low-actinic glassware.'),
 ('Cholecalciferol mobile phase (n-amyl alcohol in hexane 3:1000)',0,3,1,'Room temperature, flammables cabinet, tightly closed',1000,NULL,NULL,NULL,
  'Mix 3 mL 1-pentanol with 997 mL dehydrated hexane.'),
 ('Cholecalciferol diluent (toluene)',1,1,1,'Room temperature, flammables cabinet, protected from light',1000,NULL,NULL,NULL,
  'Toluene for the standard/sample stock. Prepare fresh daily in low-actinic glassware.'),
 ('B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)',0,3,1,'Room temperature, tightly closed',1010,NULL,NULL,NULL,
  'Mix 730 mL HPLC water, 270 mL methanol and 10 mL glacial acetic acid containing 1.4 g sodium 1-hexanesulfonate. Filter and degas; adjust per SST if needed.'),
 ('B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)',1,3,1,'Room temperature, protected from light',1000,NULL,NULL,NULL,
  'Mix 940 mL HPLC water, 50 mL acetonitrile and 10 mL glacial acetic acid; heat to 65-70 C to dissolve analytes, cool.')
) AS v(name,type,shelf,shelfunit,storage,vol,ph,phtol,adj,instr)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v.name);

------------------------------------------------------------------------------
-- 4b. Titrants, primary-standard mode (type 2)
------------------------------------------------------------------------------
INSERT INTO "SolutionMasters"
  ("SectionId","Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","Instructions","IsActive",
   "NominalStrength","StrengthUnit","StandardizationMode","StandardEntryId","EquivalenceMgPerMl","ReferenceSolutionId",
   "BlankRequired","ReplicateCount","FactorMin","FactorMax","MaxRsdPercent","ValidityDays",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.name, 2, v.shelf, 1, v.storage, 1000, v.instr, true,
       v.strength, v.sunit, 0,
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v.std),
       v.equiv, NULL, v.blank, 3, 0.970, 1.030, 0.2, v.shelf,
       c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('0.1 N Sodium hydroxide VS',30,'Tightly closed, room temperature, protected from CO2',0.1,0,'RG-022',20.42,true,
  'Dissolve 4.0 g sodium hydroxide in CO2-free water to 1000 mL. Standardize against potassium biphthalate dried at 120 C for 2 h (USP <Volumetric Solutions>).'),
 ('0.1 N Hydrochloric acid VS',30,'Tightly closed, room temperature',0.1,0,'RG-023',12.114,false,
  'Dilute 8.5 mL hydrochloric acid 37% with water to 1000 mL. Standardize against tromethamine (bromocresol green TS).'),
 ('0.1 N Perchloric acid VS (in glacial acetic acid)',30,'Tightly closed, room temperature',0.1,0,'RG-022',20.42,true,
  'Mix 8.5 mL perchloric acid 70% with 500 mL glacial acetic acid and 21 mL acetic anhydride, cool, dilute to 1000 mL with glacial acetic acid. Standardize against potassium biphthalate (crystal violet TS).'),
 ('0.05 M Edetate disodium VS',30,'Tightly closed, room temperature',0.05,1,'RG-024',5.004,false,
  'Dissolve 18.6 g edetate disodium in water to 1000 mL. Standardize against calcium carbonate (hydroxy naphthol blue).'),
 ('0.1 N Sodium thiosulfate VS',30,'Tightly closed, amber glass, room temperature',0.1,0,'RG-025',4.903,true,
  'Dissolve 26 g sodium thiosulfate pentahydrate and 0.2 g sodium carbonate in CO2-free water to 1000 mL. Standardize against potassium dichromate (iodometric).'),
 ('0.1 N Silver nitrate VS',30,'Tightly closed, amber glass, room temperature',0.1,0,'RG-026',5.844,false,
  'Dissolve 17.5 g silver nitrate in water to 1000 mL. Standardize against sodium chloride (eosin Y / potassium chromate).'),
 ('0.1 N Potassium permanganate VS',14,'Tightly closed, amber glass, room temperature',0.1,0,'RG-027',6.700,false,
  'Dissolve 3.3 g potassium permanganate in water to 1000 mL, heat, filter through glass wool. Standardize against sodium oxalate.')
) AS v(name,shelf,storage,strength,sunit,std,equiv,blank,instr)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v.name);

-- 4c. Titrant standardised against another volumetric solution (must run after 4b: references thiosulfate)
INSERT INTO "SolutionMasters"
  ("SectionId","Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","Instructions","IsActive",
   "NominalStrength","StrengthUnit","StandardizationMode","StandardEntryId","EquivalenceMgPerMl","ReferenceSolutionId",
   "BlankRequired","ReplicateCount","FactorMin","FactorMax","MaxRsdPercent","ValidityDays",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, '0.1 N Iodine VS', 2, 14, 1, 'Tightly closed, amber glass, room temperature', 1000,
       'Dissolve 14 g iodine and 36 g potassium iodide in 100 mL water, add 3 drops hydrochloric acid, dilute with water to 1000 mL. Standardize against 0.1 N sodium thiosulfate VS (starch TS).',
       true, 0.1, 0, 1, NULL, NULL,
       (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '0.1 N Sodium thiosulfate VS' AND s."Type" = 2 AND s."IsActive"),
       false, 3, 0.970, 1.030, 0.2, 14, c.uid, c.ts, c.uid, c.ts
FROM _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = '0.1 N Iodine VS');

------------------------------------------------------------------------------
-- 4d. Solution components (Order 1..n). Natural key: solution + order. Quantities per final volume.
--     Units: 0 Gram, 2 Milliliter.
------------------------------------------------------------------------------
INSERT INTO "SolutionComponents" ("SolutionMasterId","Order","MaterialMasterEntryId","Quantity","Unit")
SELECT s."Id", v.ord, e."Id", v.qty, v.unit
FROM (VALUES
 ('APAP mobile phase (water-methanol 3:1)',1,'RG-003',750,2),
 ('APAP mobile phase (water-methanol 3:1)',2,'RG-002',250,2),
 ('APAP diluent (water-methanol 3:1)',1,'RG-003',750,2),
 ('APAP diluent (water-methanol 3:1)',2,'RG-002',250,2),
 ('APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)',1,'RG-003',690,2),
 ('APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)',2,'RG-002',280,2),
 ('APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)',3,'RG-006',30,2),
 ('APAP-CAF solvent mixture (methanol-acetic acid 95:5)',1,'RG-002',950,2),
 ('APAP-CAF solvent mixture (methanol-acetic acid 95:5)',2,'RG-006',50,2),
 ('Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)',1,'RG-001',40,2),
 ('Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)',2,'RG-003',960,2),
 ('Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)',3,'RG-004',6.528,0),
 ('Amoxicillin buffer pH 5.0',1,'RG-004',6.8,0),
 ('Amoxicillin buffer pH 5.0',2,'RG-003',1000,2),
 ('Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)',1,'RG-002',700,2),
 ('Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)',2,'RG-003',300,2),
 ('Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)',3,'RG-005',0.102,2),
 ('Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)',4,'RG-010',0.207,0),
 ('Diclofenac diluent (methanol-water 7:3)',1,'RG-002',700,2),
 ('Diclofenac diluent (methanol-water 7:3)',2,'RG-003',300,2),
 ('Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)',1,'RG-008',4.0,0),
 ('Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)',2,'RG-003',400,2),
 ('Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)',3,'RG-001',600,2),
 ('Ibuprofen internal standard diluent (valerophenone in mobile phase)',1,'RG-008',4.0,0),
 ('Ibuprofen internal standard diluent (valerophenone in mobile phase)',2,'RG-003',400,2),
 ('Ibuprofen internal standard diluent (valerophenone in mobile phase)',3,'RG-001',600,2),
 ('Ibuprofen internal standard diluent (valerophenone in mobile phase)',4,'RS-008',0.35,0),
 ('Ascorbic acid mobile phase (phosphate pH 2.5)',1,'RG-036',7.8,0),
 ('Ascorbic acid mobile phase (phosphate pH 2.5)',2,'RG-004',6.1,0),
 ('Ascorbic acid mobile phase (phosphate pH 2.5)',3,'RG-003',1000,2),
 ('Ascorbic acid diluent (mobile phase)',1,'RG-036',7.8,0),
 ('Ascorbic acid diluent (mobile phase)',2,'RG-004',6.1,0),
 ('Ascorbic acid diluent (mobile phase)',3,'RG-003',1000,2),
 ('Folic acid mobile phase (perchlorate-phosphate pH 7.2)',1,'RG-030',35.1,0),
 ('Folic acid mobile phase (perchlorate-phosphate pH 7.2)',2,'RG-004',1.40,0),
 ('Folic acid mobile phase (perchlorate-phosphate pH 7.2)',3,'RG-007',0.873,2),
 ('Folic acid mobile phase (perchlorate-phosphate pH 7.2)',4,'RG-002',40,2),
 ('Folic acid mobile phase (perchlorate-phosphate pH 7.2)',5,'RG-003',950,2),
 ('Folic acid diluent (ammonium hydroxide-perchlorate)',1,'RG-009',2,2),
 ('Folic acid diluent (ammonium hydroxide-perchlorate)',2,'RG-030',1,0),
 ('Folic acid diluent (ammonium hydroxide-perchlorate)',3,'RG-003',98,2),
 ('Cyanocobalamin mobile phase (water-methanol 65:35)',1,'RG-003',650,2),
 ('Cyanocobalamin mobile phase (water-methanol 65:35)',2,'RG-002',350,2),
 ('Cyanocobalamin diluent (water)',1,'RG-003',1000,2),
 ('Cholecalciferol mobile phase (n-amyl alcohol in hexane 3:1000)',1,'RG-033',3,2),
 ('Cholecalciferol mobile phase (n-amyl alcohol in hexane 3:1000)',2,'RG-032',997,2),
 ('Cholecalciferol diluent (toluene)',1,'RG-034',1000,2),
 ('B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)',1,'RG-003',730,2),
 ('B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)',2,'RG-002',270,2),
 ('B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)',3,'RG-006',10,2),
 ('B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)',4,'RG-031',1.4,0),
 ('B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)',1,'RG-003',940,2),
 ('B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)',2,'RG-001',50,2),
 ('B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)',3,'RG-006',10,2),
 ('0.1 N Sodium hydroxide VS',1,'RG-011',4.0,0),
 ('0.1 N Sodium hydroxide VS',2,'RG-003',1000,2),
 ('0.1 N Hydrochloric acid VS',1,'RG-012',8.5,2),
 ('0.1 N Hydrochloric acid VS',2,'RG-003',991.5,2),
 ('0.1 N Perchloric acid VS (in glacial acetic acid)',1,'RG-013',8.5,2),
 ('0.1 N Perchloric acid VS (in glacial acetic acid)',2,'RG-006',970.5,2),
 ('0.1 N Perchloric acid VS (in glacial acetic acid)',3,'RG-014',21,2),
 ('0.05 M Edetate disodium VS',1,'RG-015',18.6,0),
 ('0.05 M Edetate disodium VS',2,'RG-003',1000,2),
 ('0.1 N Sodium thiosulfate VS',1,'RG-016',26,0),
 ('0.1 N Sodium thiosulfate VS',2,'RG-021',0.2,0),
 ('0.1 N Sodium thiosulfate VS',3,'RG-003',1000,2),
 ('0.1 N Silver nitrate VS',1,'RG-017',17.5,0),
 ('0.1 N Silver nitrate VS',2,'RG-003',1000,2),
 ('0.1 N Potassium permanganate VS',1,'RG-020',3.3,0),
 ('0.1 N Potassium permanganate VS',2,'RG-003',1000,2),
 ('0.1 N Iodine VS',1,'RG-018',14,0),
 ('0.1 N Iodine VS',2,'RG-019',36,0),
 ('0.1 N Iodine VS',3,'RG-012',0.15,2),
 ('0.1 N Iodine VS',4,'RG-003',1000,2)
) AS v(sname,ord,code,qty,unit)
CROSS JOIN _ctx c
JOIN "SolutionMasters" s ON s."SectionId" = c.sec AND s."Name" = v.sname
JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = v.code
WHERE NOT EXISTS (SELECT 1 FROM "SolutionComponents" sc WHERE sc."SolutionMasterId" = s."Id" AND sc."Order" = v.ord);

------------------------------------------------------------------------------
-- 5. HPLC methods (isocratic, single channel A 100 %, UV). Natural key: section + abbreviation.
--    Internal-standard methods (APAP-CAF, IBU) are external-standard in this system.
------------------------------------------------------------------------------
INSERT INTO "HplcMethods"
  ("SectionId","Name","Abbreviation","EffectiveDate","IsActive","ColumnDesignation","ColumnLengthMm","ColumnInternalDiameterMm",
   "ParticleSizeUm","ColumnBrand","ColumnPartNumber","ColumnTemperatureC","ElutionMode","EquilibrationMin","FlowRateMlPerMin",
   "DetectorType","InjectionVolumeUl","RunTimeMin","DiluentSolutionId",
   "CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec, v.name, v.abbr, timestamptz '2026-09-01 00:00:00+00', true, v.des, v.len, v.id, v.part, v.brand, NULL, v.temp, 0, NULL,
       v.flow, 0, v.inj, v.run,
       (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v.dil AND s."Type" = 1),
       c.uid, c.ts, c.uid, c.ts
FROM (VALUES
 ('Acetaminophen Tablets Assay (HPLC)','APAP','L1',300,3.9,5,'Waters Symmetry C18',25,1.5,10,15,'APAP diluent (water-methanol 3:1)'),
 ('Acetaminophen and Caffeine Tablets Assay (HPLC)','APAP-CAF','L1',100,4.6,5,'Zorbax Eclipse XDB-C18',45,2.0,10,15,'APAP-CAF solvent mixture (methanol-acetic acid 95:5)'),
 ('Amoxicillin Capsules Assay (HPLC)','AMOX','L1',250,4.6,5,'Inertsil ODS-3',25,1.5,10,20,'Amoxicillin buffer pH 5.0'),
 ('Diclofenac Sodium DR Tablets Assay (HPLC)','DICLO','L7',250,4.6,5,'Inertsil C8-3',25,1.0,10,20,'Diclofenac diluent (methanol-water 7:3)'),
 ('Ibuprofen Tablets Assay (HPLC)','IBU','L1',250,4.6,5,'Hypersil BDS C18',25,2.0,5,20,'Ibuprofen internal standard diluent (valerophenone in mobile phase)'),
 ('Ascorbic Acid Assay (HPLC)','VIT-C','L39',150,6.0,5,'Polymeric L39',25,0.6,4,10,'Ascorbic acid diluent (mobile phase)'),
 ('Folic Acid Tablets Assay (HPLC)','FOLIC','L1',250,4.6,5,'Hypersil BDS C18',25,1.0,25,25,'Folic acid diluent (ammonium hydroxide-perchlorate)'),
 ('Cyanocobalamin Assay (HPLC)','B12','L1',150,4.6,5,'Zorbax Eclipse XDB-C18',25,0.5,200,15,'Cyanocobalamin diluent (water)'),
 ('Cholecalciferol Assay (HPLC, normal phase)','VIT-D3','L3',250,4.6,5,'Zorbax Rx-SIL',25,2.0,10,25,'Cholecalciferol diluent (toluene)'),
 ('B-complex Assay (HPLC)','VIT-B','L1',300,3.9,5,'Waters Symmetry C18',25,1.0,10,35,'B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)')
) AS v(name,abbr,des,len,id,part,brand,temp,flow,inj,run,dil)
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethods" m WHERE m."SectionId" = c.sec AND m."Abbreviation" = v.abbr);

-- Mobile phase channel A = 100 %
INSERT INTO "HplcMethodMobilePhases" ("HplcMethodId","Channel","SolutionMasterId","RatioPercent")
SELECT m."Id", 'A', s."Id", 100
FROM (VALUES
 ('APAP','APAP mobile phase (water-methanol 3:1)'),
 ('APAP-CAF','APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)'),
 ('AMOX','Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)'),
 ('DICLO','Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)'),
 ('IBU','Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)'),
 ('VIT-C','Ascorbic acid mobile phase (phosphate pH 2.5)'),
 ('FOLIC','Folic acid mobile phase (perchlorate-phosphate pH 7.2)'),
 ('B12','Cyanocobalamin mobile phase (water-methanol 65:35)'),
 ('VIT-D3','Cholecalciferol mobile phase (n-amyl alcohol in hexane 3:1000)'),
 ('VIT-B','B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)')
) AS v(abbr,sname)
CROSS JOIN _ctx c
JOIN "HplcMethods" m ON m."SectionId" = c.sec AND m."Abbreviation" = v.abbr
JOIN "SolutionMasters" s ON s."SectionId" = c.sec AND s."Name" = v.sname AND s."Type" = 0
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethodMobilePhases" p WHERE p."HplcMethodId" = m."Id" AND p."Channel" = 'A');

-- Analytes (external standard, RS master entry; SST criteria from the USP text)
INSERT INTO "HplcMethodAnalytes"
  ("HplcMethodId","DisplayOrder","Name","WavelengthNm","StandardEntryId","TheoreticalWeightStdMg","TheoreticalWeightTestMg",
   "StandardInjections","SstMaxRsdPercent","SstMinResolution","SstMaxTailingFactor","SstMinTheoreticalPlates")
SELECT m."Id", v.ord, v.aname, v.wl, e."Id", v.stdwt, v.testwt, v.inj, v.rsd::numeric, v.res::numeric, v.tail::numeric, v.plates::numeric
FROM (VALUES
 ('APAP',1,'Acetaminophen',243,'RS-001',100,120,5,2.0,NULL,2.0,1000),
 ('APAP-CAF',1,'Acetaminophen',275,'RS-001',25,325,5,2.0,1.4,1.2,NULL),
 ('APAP-CAF',2,'Caffeine',275,'RS-002',3.25,325,5,2.0,1.4,1.2,NULL),
 ('AMOX',1,'Amoxicillin',230,'RS-003',120,240,5,2.0,NULL,2.5,NULL),
 ('DICLO',1,'Diclofenac sodium',254,'RS-004',75,375,5,2.0,6.5,NULL,NULL),
 ('IBU',1,'Ibuprofen',254,'RS-005',1200,1800,5,2.0,2.5,2.5,NULL),
 ('VIT-C',1,'Ascorbic acid',245,'RS-009',50,140,6,1.5,NULL,1.6,3500),
 ('FOLIC',1,'Folic acid',254,'RS-010',10,240,5,2.0,3.6,NULL,NULL),
 ('B12',1,'Cyanocobalamin',550,'RS-011',1.0,50,6,3.0,NULL,NULL,NULL),
 ('VIT-D3',1,'Cholecalciferol',254,'RS-012',6.0,6.0,5,2.0,1.0,NULL,NULL),
 ('VIT-B',1,'Niacinamide',280,'RS-015',80,50,6,3.0,NULL,NULL,NULL),
 ('VIT-B',2,'Pyridoxine HCl',280,'RS-016',20,50,6,3.0,NULL,NULL,NULL),
 ('VIT-B',3,'Riboflavin',280,'RS-014',20,50,6,3.0,NULL,NULL,NULL),
 ('VIT-B',4,'Thiamine HCl',280,'RS-013',20,50,6,3.0,NULL,NULL,NULL)
) AS v(abbr,ord,aname,wl,std,stdwt,testwt,inj,rsd,res,tail,plates)
CROSS JOIN _ctx c
JOIN "HplcMethods" m ON m."SectionId" = c.sec AND m."Abbreviation" = v.abbr
JOIN "MaterialMasterEntries" e ON e."SectionId" = c.sec AND e."Code" = v.std
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethodAnalytes" a WHERE a."HplcMethodId" = m."Id" AND a."Name" = v.aname);

------------------------------------------------------------------------------
-- 6. Test master: one HplcMethodAssay test per method (TestDefinitionMasterDataService rules:
--    workflow 12 + equation 13, RequiresSystemSuitability, MethodAbbreviation = method abbreviation, HplcMethodId set,
--    no analytes / SST criteria on the test, ResponseMode PeakArea, CalMaxRunAgeHours default 24).
------------------------------------------------------------------------------
INSERT INTO "TestDefinitions"
  ("Code","DisplayName","SectionId","WorkflowType","EquationType","RequiresSystemSuitability","MethodAbbreviation",
   "IsActive","ResponseMode","CalMaxRunAgeHours","HplcMethodId")
SELECT 'HPLC-' || m."Abbreviation", m."Name", c.sec, 12, 13, true, m."Abbreviation", true, 0, 24, m."Id"
FROM "HplcMethods" m CROSS JOIN _ctx c
WHERE m."SectionId" = c.sec AND m."IsActive"
  AND m."Abbreviation" IN ('APAP','APAP-CAF','AMOX','DICLO','IBU','VIT-C','FOLIC','B12','VIT-D3','VIT-B')
  AND NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = 'HPLC-' || m."Abbreviation");

-- Stage replicates: Finished 2 std / 2 sample (role 3), Bulk 2 std / 3 sample (role 1)
INSERT INTO "TestDefinitionStageReplicates" ("TestDefinitionId","Role","StandardReplicates","SampleReplicates")
SELECT t."Id", r.role, r.std, r.smp
FROM "TestDefinitions" t
CROSS JOIN (VALUES (3,2,2),(1,2,3)) AS r(role,std,smp)
WHERE t."Code" LIKE 'HPLC-%' AND t."WorkflowType" = 12
  AND NOT EXISTS (SELECT 1 FROM "TestDefinitionStageReplicates" x WHERE x."TestDefinitionId" = t."Id" AND x."Role" = r.role);

------------------------------------------------------------------------------
-- 7. Items (finished product, category 0), assigned test, specifications
------------------------------------------------------------------------------
INSERT INTO "Items" ("Name","Code","Category","SopNumber","IsActive")
SELECT v.name, v.code, 0, 'USP-NF', true
FROM (VALUES
 ('Acetaminophen Tablets 500 mg','APAP-500'),
 ('Acetaminophen and Caffeine Tablets 500 mg/65 mg','APAP-CAF-500'),
 ('Amoxicillin Capsules 500 mg','AMOX-500'),
 ('Diclofenac Sodium Delayed-Release Tablets 50 mg','DICLO-50'),
 ('Ibuprofen Tablets 400 mg','IBU-400'),
 ('Ascorbic Acid Tablets 500 mg','VITC-500'),
 ('Folic Acid Tablets 5 mg','FOLIC-5'),
 ('Cyanocobalamin Tablets 500 mcg','B12-500'),
 ('Cholecalciferol (Vitamin D3) Drug Substance','VITD3-API'),
 ('B-complex Tablets (B1/B2/B3/B6)','VITB-CPX')
) AS v(name,code)
WHERE NOT EXISTS (SELECT 1 FROM "Items" i WHERE i."Code" = v.code);

INSERT INTO "SampleTests" ("ItemId","TestCode","DisplayName")
SELECT i."Id", t."Code", t."DisplayName"
FROM (VALUES
 ('APAP-500','APAP'),('APAP-CAF-500','APAP-CAF'),('AMOX-500','AMOX'),('DICLO-50','DICLO'),('IBU-400','IBU'),
 ('VITC-500','VIT-C'),('FOLIC-5','FOLIC'),('B12-500','B12'),('VITD3-API','VIT-D3'),('VITB-CPX','VIT-B')
) AS v(icode,abbr)
JOIN "Items" i ON i."Code" = v.icode
JOIN "TestDefinitions" t ON t."Code" = 'HPLC-' || v.abbr
WHERE NOT EXISTS (SELECT 1 FROM "SampleTests" st WHERE st."ItemId" = i."Id" AND st."TestCode" = t."Code");

-- Specification rows: assay % (PercentLabelClaim=2, no label claim) and amount per unit (MgPerUnit=1, with label claim
-- and unit, limits = claim * % / 100). Linked to the method analyte; LimitType Range; canonical SpecLimit text.
-- DosageForm is NOT set: SpecificationService.Validate rejects it outside WeightVariation specifications.
-- The per-unit row gets its own ParameterName (service refuses a duplicate name per item+test).
WITH src(icode, abbr, aname, lo, hi, claim, cunit) AS (VALUES
 ('APAP-500','APAP','Acetaminophen',90.0,110.0,500,'mg'),
 ('APAP-CAF-500','APAP-CAF','Acetaminophen',90.0,110.0,500,'mg'),
 ('APAP-CAF-500','APAP-CAF','Caffeine',90.0,110.0,65,'mg'),
 ('AMOX-500','AMOX','Amoxicillin',90.0,120.0,500,'mg'),
 ('DICLO-50','DICLO','Diclofenac sodium',90.0,110.0,50,'mg'),
 ('IBU-400','IBU','Ibuprofen',90.0,110.0,400,'mg'),
 ('VITC-500','VIT-C','Ascorbic acid',90.0,110.0,500,'mg'),
 ('FOLIC-5','FOLIC','Folic acid',90.0,115.0,5,'mg'),
 ('B12-500','B12','Cyanocobalamin',90.0,150.0,500,'mcg'),
 ('VITD3-API','VIT-D3','Cholecalciferol',97.0,103.0,NULL,NULL),
 ('VITB-CPX','VIT-B','Niacinamide',90.0,150.0,100,'mg'),
 ('VITB-CPX','VIT-B','Pyridoxine HCl',90.0,150.0,25,'mg'),
 ('VITB-CPX','VIT-B','Riboflavin',90.0,150.0,25,'mg'),
 ('VITB-CPX','VIT-B','Thiamine HCl',90.0,150.0,25,'mg')
),
rows AS (
  -- basis 2 = assay %, basis 1 = amount per unit (only when a label claim exists)
  SELECT s.icode, s.abbr, s.aname, 2 AS basis, s.lo AS lo, s.hi AS hi, '%'::text AS unit, NULL::numeric AS claim, NULL::text AS cunit,
         s.aname AS pname, 0 AS ord
  FROM src s
  UNION ALL
  SELECT s.icode, s.abbr, s.aname, 1, s.claim * s.lo / 100, s.claim * s.hi / 100, s.cunit, s.claim::numeric, s.cunit,
         s.aname || ' (per unit)', 1
  FROM src s WHERE s.claim IS NOT NULL
)
INSERT INTO "Specifications"
  ("ItemId","TestCode","AlertLimit","ActionLimit","SpecLimit","Unit","ParameterName","DisplayOrder","LimitType",
   "LowerLimit","UpperLimit","LowerInclusive","UpperInclusive","ResultBasis","LabelClaim","LabelClaimUnit",
   "ConversionFactor","HplcMethodAnalyteId")
SELECT i."Id", t."Code", '', '',
       trim_scale(r.lo)::text || '-' || trim_scale(r.hi)::text,
       r.unit, r.pname,
       (dense_rank() OVER (PARTITION BY i."Id" ORDER BY a."DisplayOrder", r.ord) - 1)::int,
       0, r.lo, r.hi, true, true, r.basis, r.claim, r.cunit, 1.0, a."Id"
FROM rows r
JOIN "Items" i ON i."Code" = r.icode
JOIN "TestDefinitions" t ON t."Code" = 'HPLC-' || r.abbr
JOIN "HplcMethods" m ON m."Id" = t."HplcMethodId"
JOIN "HplcMethodAnalytes" a ON a."HplcMethodId" = m."Id" AND a."Name" = r.aname
WHERE NOT EXISTS (SELECT 1 FROM "Specifications" x
                  WHERE x."ItemId" = i."Id" AND x."TestCode" = t."Code"
                    AND x."HplcMethodAnalyteId" = a."Id" AND x."ResultBasis" = r.basis);

COMMIT;
