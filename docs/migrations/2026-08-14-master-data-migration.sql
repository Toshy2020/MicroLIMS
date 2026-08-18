-- ============================================================================
-- MicroLIMS master/config data migration: source (dev) -> Neon (production)
-- Generated 2026-08-14T16:42:22.411Z
-- DO NOT EXECUTE without review. See accompanying migration plan .md file.
-- ============================================================================

BEGIN;

-- Pre-flight: abort immediately if production has no 'admin' user to attribute
-- audit/User-reference columns to. We never copy source Users or their IDs.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = 'admin') THEN
    RAISE EXCEPTION 'Pre-flight failed: no Users row with Username=''admin'' in this database. Aborting.';
  END IF;
END $$;


-- ----------------------------------------------------------------------------
-- 1. Departments (no FK dependencies)
-- ----------------------------------------------------------------------------
INSERT INTO "Departments" ("Id","Name","Class","TestingFrequency")
SELECT v.id, v.name, v.class, v.freq
FROM (VALUES
  (1, 'OSD II', 'D', 'Monthly'),
  (2, 'OSD I', 'D', 'Monthly'),
  (3, 'Capsule filling', 'D', 'Monthly'),
  (4, 'Liquid Department', 'D', 'Monthlt')
) AS v(id, name, class, freq)
WHERE NOT EXISTS (SELECT 1 FROM "Departments" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "Departments" t WHERE t."Name" = v.name);

-- ----------------------------------------------------------------------------
-- 2. Organisms (no FK dependencies)
-- ----------------------------------------------------------------------------
INSERT INTO "Organisms" ("Id","ScientificName","AtccNumber","CommonName")
SELECT v.id, v.name, v.atcc, v.common
FROM (VALUES
  (1, 'Escherichia coli', '8739', 'E.coli#8739'),
  (2, 'Staphylococcus aureus', '6538', 'S. aureus #6538'),
  (3, 'Pseudomonas aeruginosa', '9027', 'P.aeruginosa#9027'),
  (4, 'Aspergillus brasiliensis', '#16404', 'Asp.#16404'),
  (5, 'Candida albicans', '#10231', 'C.A #10231'),
  (6, 'Burkholderia cepacia', '#25416', 'BCC'),
  (7, '----', NULL, NULL),
  (17, '...', '...', '...'),
  (18, 'Bacillus subtilis', '# 6633', 'B.Sub# 6633'),
  (21, 'Burkholderia cenocepacia', '# BAA-245', 'B-Ceno'),
  (22, 'Salmonella  typhimurium', '14028', 'S.typhimurium#14028')
) AS v(id, name, atcc, common)
WHERE NOT EXISTS (SELECT 1 FROM "Organisms" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "Organisms" t WHERE lower(t."ScientificName") = lower(v.name))
  AND (v.atcc IS NULL OR NOT EXISTS (SELECT 1 FROM "Organisms" t WHERE t."AtccNumber" = v.atcc));

-- ----------------------------------------------------------------------------
-- 3. MediaTypes (no FK dependencies)
-- ----------------------------------------------------------------------------
INSERT INTO "MediaTypes" ("Id","Class","IncubationMinHours","IncubationMaxHours","RequiredTemperatureMin","RequiredTemperatureMax","ApprovedTestCodes","RecoveryPercentMin","RecoveryPercentMax")
SELECT v.id, v.class, v.incmin, v.incmax, v.tmin, v.tmax, v.codes, v.recmin, v.recmax
FROM (VALUES
  (7, 0, 24, 50, 30, 35, ARRAY['TAMC']::text[], 70, 200),
  (8, 1, 24, 48, 30, 35, ARRAY['Sterility']::text[], NULL, NULL),
  (9, 2, 18, 24, 32.5, 35, ARRAY['PATHOGEN_ECOLI']::text[], NULL, NULL),
  (10, 3, 18, 24, 32.5, 35, ARRAY['PATHOGEN_ECOLI']::text[], NULL, NULL)
) AS v(id, class, incmin, incmax, tmin, tmax, codes, recmin, recmax)
WHERE NOT EXISTS (SELECT 1 FROM "MediaTypes" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "MediaTypes" t WHERE t."Class" = v.class);

-- ----------------------------------------------------------------------------
-- 4. Equipment (no FK dependencies)
-- ----------------------------------------------------------------------------
INSERT INTO "Equipment" ("Id","Name","Code","Type","Location","SetPointTemperature","CalibrationDueDate")
SELECT v.id, v.name, v.code, v.type, v.loc, v.setpoint, v.caldue
FROM (VALUES
  (1, 'Incubator 03', 'INC-03', 0, NULL, 32.5, '2027-01-29T20:14:37.011Z'::timestamptz),
  (2, 'Autoclave 1', 'AUT-01', 1, NULL, NULL, NULL),
  (3, 'Qualitemp', 'INC-F-ML-F-01-003', 0, 'Instruments room F-ML-F-01', 32.5, '2027-08-03T00:00:00.000Z'::timestamptz),
  (4, 'Biochemical', 'INC-F-ML-F-01-005', 0, 'Instruments room F-ML-F-01', 22.5, '2027-08-03T00:00:00.000Z'::timestamptz),
  (5, 'Binder', 'INC-F-ML-F-01-007', 0, 'Instruments room F-ML-F-01', 42, '2027-08-03T00:00:00.000Z'::timestamptz),
  (6, 'Labtop', 'INC-F-ML-F-01-077', 0, 'Instruments room F-ML-F-01', 32.5, '2027-08-03T00:00:00.000Z'::timestamptz)
) AS v(id, name, code, type, loc, setpoint, caldue)
WHERE NOT EXISTS (SELECT 1 FROM "Equipment" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "Equipment" t WHERE t."Code" = v.code);

-- ----------------------------------------------------------------------------
-- 5. EquipmentInventories (no FK dependencies; User-audit cols -> admin)
-- ----------------------------------------------------------------------------
INSERT INTO "EquipmentInventories" ("Id","InstrumentType","ManufacturerName","SerialNumber","FirmwareVersion","Code","Location","CalibrationDueDate","Status","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT v.id, v.itype, v.mfr, v.serial, v.firmware, v.code, v.loc, v.caldue, v.status, (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' ORDER BY "Id" LIMIT 1), v.createdat, (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' ORDER BY "Id" LIMIT 1), v.modifiedat
FROM (VALUES
  (1, 'Incubator', 'Qualitemp', '61631/02', 'N.A', 'INC-F-ML-F-01-003', 'Instruments room  F-ML-F-01', '2027-07-31T00:00:00.000Z'::timestamptz, 0, '2026-07-31T10:18:46.512Z'::timestamptz, '2026-07-31T10:18:46.512Z'::timestamptz),
  (2, 'Incubator', 'Biochemical ', '0706425', 'N.A', 'INC-F-ML-F-01-005', 'Instruments room  F-ML-F-01', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T13:40:56.294Z'::timestamptz, '2026-08-02T13:40:56.294Z'::timestamptz),
  (3, 'Incubator', 'Binder', '13-19088', 'N.A', 'INC-F-ML-F-01-007', 'Instruments room F-ML-F-01', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T13:42:19.622Z'::timestamptz, '2026-08-02T13:42:19.622Z'::timestamptz),
  (4, 'Incubator', 'INCUCELL', 'D141445', 'N.A', 'INC-F-ML-F-01-002', 'Instruments room F-ML-F-01', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T13:43:08.678Z'::timestamptz, '2026-08-02T13:43:08.678Z'::timestamptz),
  (5, 'Incubator', 'Labtop', '250713573', 'Datalinx v3.0', 'INC-F-ML-F-01-077', 'Instruments room F-ML-F-01', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T13:45:39.923Z'::timestamptz, '2026-08-02T13:45:39.923Z'::timestamptz),
  (6, 'Autoclave', 'Hirayama', '30317012128', 'N/A', 'ATC-F-ML-F-03-045', 'Sterilization room F-ML-F-04', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T13:46:28.778Z'::timestamptz, '2026-08-02T13:46:28.778Z'::timestamptz),
  (7, 'Refrigerator ', 'LG', '1051NZY4R709', 'N.A', 'COD-F-ML-F-01-054', 'Instruments room F-ML-F-01', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T16:02:39.487Z'::timestamptz, '2026-08-02T16:02:39.487Z'::timestamptz),
  (8, 'Deep Freezer', 'SHARP', '24D03#010843FB4C', 'N.A', 'COD-F-ML-D-06-071', 'LAF-3 room F-ML-D-06', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T16:03:17.430Z'::timestamptz, '2026-08-02T16:03:17.430Z'::timestamptz),
  (9, 'LAF', 'Rich-tek', 'N/A', 'N/A', 'LAF-F-ML-D-07-028', 'LAF-1 room F-ML-D-07', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T16:03:58.598Z'::timestamptz, '2026-08-02T16:03:58.598Z'::timestamptz),
  (10, 'LAF', 'Rich-tek', 'N/A', 'N/A', 'LAF-F-ML-D-10-037', 'LAF-2 room F-ML-D-10', NULL, 0, '2026-08-02T16:04:34.109Z'::timestamptz, '2026-08-02T16:04:34.109Z'::timestamptz),
  (11, 'Biological Safety Cabinet', 'JSR', '170417-39', 'N/A', 'LAF-F-ML-D-06-026', 'LAF-3 room F-ML-D-06', '2027-08-02T00:00:00.000Z'::timestamptz, 0, '2026-08-02T16:05:14.814Z'::timestamptz, '2026-08-02T16:05:14.814Z'::timestamptz)
) AS v(id, itype, mfr, serial, firmware, code, loc, caldue, status, createdat, modifiedat)
WHERE NOT EXISTS (SELECT 1 FROM "EquipmentInventories" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "EquipmentInventories" t WHERE t."Code" = v.code);

-- ----------------------------------------------------------------------------
-- 6. Items (no FK dependencies)
-- ----------------------------------------------------------------------------
INSERT INTO "Items" ("Id","Name","Code","Category","SopNumber","IsActive")
SELECT v.id, v.name, v.code, v.cat, v.sop, v.active
FROM (VALUES
  (2, 'Osteocare Liquid', 'ost.liq', 0, 'stm-pro-01', TRUE),
  (3, 'Osteocare Tablet', 'ost.tab', 0, 'STM-PC-023-EDITED', TRUE),
  (8, 'city water', 'SP101', 3, 'C2F-91-115', TRUE),
  (9, 'Honey', 'P.Honey', 1, 'C2I-91-128', TRUE),
  (10, 'Smoke Test Item', 'SMOKE01', 0, '', TRUE),
  (11, 'SP301', 'SP301', 3, 'C2F-91-115', TRUE),
  (12, 'xanthan gum', 'Xanth gum', 1, 'STM-91-128', TRUE),
  (13, 'Feroglobin Capsule B12', 'Fero.caps', 0, 'STM-91-02', TRUE),
  (14, 'Calcium Carbonate MD', 'Caco3 MD', 1, 'STM-RM-01', TRUE),
  (15, 'PET bottles 150 ml', 'PET 150ml', 2, 'STM-PM-01', TRUE)
) AS v(id, name, code, cat, sop, active)
WHERE NOT EXISTS (SELECT 1 FROM "Items" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "Items" t WHERE t."Code" = v.code);

-- ----------------------------------------------------------------------------
-- 7. TestDefinitions (no FK dependencies)
-- ----------------------------------------------------------------------------
INSERT INTO "TestDefinitions" ("Id","Code","DisplayName","IsActive","WorkflowType")
SELECT v.id, v.code, v.display, v.active, v.wftype
FROM (VALUES
  (1, 'TAMC', 'Total Aerobic Microbial Count', TRUE, 0),
  (2, 'TYMC', 'Total Yeast Mold Count', TRUE, 0),
  (3, 'TAMC Surface sample', 'TAMC Surface sample', TRUE, 0),
  (4, 'P. aeruginosa', 'Detection of Pseudomonas aeruginosa', TRUE, 1),
  (5, 'BCC', 'Detection of Burkholderia cepacia complex', TRUE, 1),
  (6, 'E.coli', 'Detection Of E. coli', TRUE, 1),
  (7, 'TAMC Passive air sample', 'TAMC Passive air sample', TRUE, 0),
  (8, 'S.aureus ', 'Detection Of S. aureus ', TRUE, 1),
  (9, 'Salmonella ', 'Detection of Salmonella  typhimurium', TRUE, 1),
  (10, 'TAMC-Water', 'Water TAMC', TRUE, 0),
  (11, 'After cleaning TAMC', 'TAMC-After cleaning', TRUE, 0),
  (12, 'TAMC-transfere', 'TAMC', TRUE, 0)
) AS v(id, code, display, active, wftype)
WHERE NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = v.code);

-- ----------------------------------------------------------------------------
-- 8. WaterSamplingPoints (no FK dependencies)
-- ----------------------------------------------------------------------------
INSERT INTO "WaterSamplingPoints" ("Id","Code","Location","AssignedTestCodes")
SELECT v.id, v.code, v.loc, v.codes
FROM (VALUES
  (2, 'SP 103', 'SP 103', ARRAY['BCC','E.coli','P. aeruginosa','S.aureus ','TAMC-Water','Salmonella ']::text[]),
  (3, 'SP104', 'SP 104', ARRAY['BCC','E.coli','P. aeruginosa','S.aureus ','TAMC-Water','Salmonella ']::text[]),
  (4, 'SP105', 'WTU', ARRAY['BCC','E.coli','P. aeruginosa','S.aureus ','TAMC-Water','Salmonella ']::text[]),
  (5, 'SP106', 'WTU', ARRAY['BCC','E.coli','P. aeruginosa','S.aureus ','TAMC-Water','Salmonella ']::text[]),
  (6, 'SWT', 'WTU', ARRAY['BCC','E.coli','P. aeruginosa','S.aureus ','TAMC-Water','Salmonella ']::text[])
) AS v(id, code, loc, codes)
WHERE NOT EXISTS (SELECT 1 FROM "WaterSamplingPoints" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "WaterSamplingPoints" t WHERE t."Code" = v.code);

-- ----------------------------------------------------------------------------
-- 9. Rooms (FK: DepartmentId resolved via Departments.Name business key)
-- ----------------------------------------------------------------------------
INSERT INTO "Rooms" ("Id","Name","DepartmentId","GradeClassification")
SELECT v.id, v.name, d."Id", v.grade
FROM (VALUES
  (1, 'Preparation suit', 'OSD II', 'D'),
  (5, 'Coating', 'OSD II', 'D'),
  (6, 'Capsule filling corridor', 'Capsule filling', 'D'),
  (7, 'Capsule filling room 1', 'Capsule filling', 'D'),
  (8, 'Compression', 'OSD II', 'D'),
  (9, 'Blistering PG Machine', 'OSD II', 'D'),
  (10, 'Quarantine ', 'OSD II', 'D'),
  (11, 'Preparation 2', 'OSD II', 'D'),
  (12, 'Clean Equipment room', 'OSD II', 'D')
) AS v(id, name, deptname, grade)
JOIN "Departments" d ON d."Name" = v.deptname
WHERE NOT EXISTS (SELECT 1 FROM "Rooms" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "Rooms" t WHERE t."Name" = v.name AND t."DepartmentId" = d."Id"
  );

-- ----------------------------------------------------------------------------
-- 10. Materials (FK: OrganismId resolved via Organisms.ScientificName, nullable; no reliable Materials business key -- see caveat in plan doc, guarded by Id only)
-- ----------------------------------------------------------------------------
INSERT INTO "Materials" ("Id","MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location","QuantityReceived","QuantityRemaining","Unit","MinimumStockLevel","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt","AtccNumber","OrganismId")
SELECT v.id, v.mtype, v.mname, v.mfr, v.batch, v.recvdate, v.expdate, v.code, v.loc, v.qtyrecv, v.qtyrem, v.unit, v.minstock, (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' ORDER BY "Id" LIMIT 1), v.createdat, (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' ORDER BY "Id" LIMIT 1), v.modifiedat, v.atcc,
  (SELECT o."Id" FROM "Organisms" o WHERE o."ScientificName" = v.orgname)
FROM (VALUES
  (4, 0, 'Tryptic Soy Agar', 'Seed Data', 'SEED-0001', '2026-08-01T00:00:00.000Z'::timestamptz, NULL, 'TSA', 'Micro Lab', 1000.000, 660.000, 0, NULL, '2026-08-01T07:48:17.153Z'::timestamptz, '2026-08-02T17:29:33.746Z'::timestamptz, NULL, NULL),
  (5, 0, 'XLD Agar', 'Himedia', 'XLD-001', '2026-08-01T00:00:00.000Z'::timestamptz, '2027-08-01T00:00:00.000Z'::timestamptz, 'XLD', 'Micro Lab', 500.000, 250.000, 0, NULL, '2026-08-01T08:00:24.082Z'::timestamptz, '2026-08-05T13:33:06.800Z'::timestamptz, NULL, NULL),
  (6, 0, 'Cetrimide Agar', 'Merck', 'VM1077984', '2025-09-03T00:00:00.000Z'::timestamptz, '2029-01-31T00:00:00.000Z'::timestamptz, 'CAM', 'Micro Lab', 2000.000, 1990.000, 0, 200.000, '2026-08-01T08:01:45.895Z'::timestamptz, '2026-08-02T10:41:08.351Z'::timestamptz, NULL, NULL),
  (7, 0, 'Tryptic soy broth', 'Merck', 'VM1091459', '2026-05-14T00:00:00.000Z'::timestamptz, '2029-05-31T00:00:00.000Z'::timestamptz, 'TSB', 'Micro Lab', 2000.000, 1890.000, 0, NULL, '2026-08-01T08:29:31.536Z'::timestamptz, '2026-08-01T19:54:05.573Z'::timestamptz, NULL, NULL),
  (8, 0, 'Macconkey broth Purple', 'Oxoid', '6267096', '2026-04-19T00:00:00.000Z'::timestamptz, '2030-05-31T00:00:00.000Z'::timestamptz, 'MBP', 'Micro Lab', 2000.000, 2000.000, 0, 200.000, '2026-08-01T08:30:48.224Z'::timestamptz, '2026-08-01T08:30:48.224Z'::timestamptz, NULL, NULL),
  (9, 0, 'MacConkey agar', 'Merck', 'VM1057505', '2025-12-15T00:00:00.000Z'::timestamptz, '2028-05-31T00:00:00.000Z'::timestamptz, 'MAR', 'Micro Lab', 2000.000, 1960.000, 0, 200.000, '2026-08-01T08:32:35.347Z'::timestamptz, '2026-08-03T22:30:19.072Z'::timestamptz, NULL, NULL),
  (10, 1, 'Escherichia coli', 'Tody laboratories', '03802303', '2026-03-17T00:00:00.000Z'::timestamptz, '2027-08-31T00:00:00.000Z'::timestamptz, 'E.coli', 'Micro refrigerator', 10.000, 6.000, 4, 2.000, '2026-08-01T17:08:52.991Z'::timestamptz, '2026-08-08T16:09:13.790Z'::timestamptz, '#8739', 'Escherichia coli'),
  (11, 1, 'Pseudomonas aeruginosa ', 'Tody laboratories', '04001703', '2026-02-24T00:00:00.000Z'::timestamptz, '2026-09-30T00:00:00.000Z'::timestamptz, 'Ps.', 'Micro refrigerator', 10.000, 10.000, 4, 2.000, '2026-08-01T17:10:02.774Z'::timestamptz, '2026-08-08T16:09:48.703Z'::timestamptz, '#9027', 'Pseudomonas aeruginosa'),
  (12, 1, 'Escherichia coli', 'Tody laboratories', 'LOT-EC-01', '2026-08-01T00:00:00.000Z'::timestamptz, '2027-08-01T00:00:00.000Z'::timestamptz, NULL, 'Micro refrigerator', 10.000, 8.000, 4, NULL, '2026-08-01T17:10:30.799Z'::timestamptz, '2026-08-01T17:15:51.541Z'::timestamptz, '8739', 'Escherichia coli'),
  (13, 1, 'Pseudomonas aeruginosa', 'Tody laboratories', '04001804', '2026-03-17T00:00:00.000Z'::timestamptz, '2027-05-31T00:00:00.000Z'::timestamptz, 'Ps.', 'Micro refrigerator', 10.000, 9.000, 4, 2.000, '2026-08-01T17:12:01.951Z'::timestamptz, '2026-08-08T16:09:34.269Z'::timestamptz, ' #9027', 'Pseudomonas aeruginosa'),
  (14, 1, 'Salmonella  typhimurium', 'Tody laboratories', '01103101', '2025-02-24T00:00:00.000Z'::timestamptz, '2026-09-30T00:00:00.000Z'::timestamptz, 'Sal.', 'Micro refrigerator', 10.000, 9.000, 4, 2.000, '2026-08-02T06:38:52.434Z'::timestamptz, '2026-08-08T16:10:03.635Z'::timestamptz, '# 14028', 'Salmonella  typhimurium'),
  (15, 0, 'Sabouraud dextrose agar', 'OXOID', '3773319', '2026-04-28T00:00:00.000Z'::timestamptz, '2028-12-31T00:00:00.000Z'::timestamptz, 'SDA', 'Micro Lab', 2000.000, 1980.000, 0, 200.000, '2026-08-02T09:16:28.204Z'::timestamptz, '2026-08-02T10:48:23.678Z'::timestamptz, NULL, NULL),
  (16, 0, 'Macconkey broth Purple', 'Oxoid', '6267096', '2026-04-19T00:00:00.000Z'::timestamptz, '2030-05-31T00:00:00.000Z'::timestamptz, 'MBP', 'Micro Lab', 2000.000, 1980.000, 0, 200.000, '2026-08-02T09:17:48.339Z'::timestamptz, '2026-08-02T10:47:41.968Z'::timestamptz, NULL, NULL),
  (17, 0, 'Eosin methylene blue agar', 'Oxoid', '6205653', '2025-12-16T00:00:00.000Z'::timestamptz, '2027-12-31T00:00:00.000Z'::timestamptz, 'EMB', 'Microbiology lab', 2000.000, 1980.000, 0, 200.000, '2026-08-02T09:19:16.839Z'::timestamptz, '2026-08-02T10:42:17.153Z'::timestamptz, NULL, NULL),
  (18, 0, 'Rappaport Vasiliadis Salmonella enrichment broth', 'Himedia', '0000682699', '2025-12-15T00:00:00.000Z'::timestamptz, '2029-12-31T00:00:00.000Z'::timestamptz, 'RVS', 'Microlab', 2000.000, 1970.000, 0, 200.000, '2026-08-02T09:22:36.863Z'::timestamptz, '2026-08-05T13:38:18.033Z'::timestamptz, NULL, NULL),
  (19, 0, 'Triple Sugar Iron Agar', 'Himedia', '0000608756', '2025-12-15T00:00:00.000Z'::timestamptz, '2028-08-31T00:00:00.000Z'::timestamptz, 'TSI', 'Micro LAb', 2000.000, 1780.000, 0, 200.000, '2026-08-02T09:24:09.124Z'::timestamptz, '2026-08-06T09:02:24.918Z'::timestamptz, NULL, NULL),
  (20, 0, ' Burkholderia Cepacia Medium', 'OXOID', '3749511', '2024-12-03T00:00:00.000Z'::timestamptz, '2026-11-30T00:00:00.000Z'::timestamptz, 'BUR', 'Micro Lab', 2000.000, 1980.000, 0, 200.000, '2026-08-02T09:25:03.282Z'::timestamptz, '2026-08-02T10:37:35.834Z'::timestamptz, NULL, NULL),
  (21, 0, 'Mannitol salt agar ', 'Oxoid ', '3754600', '2026-04-19T00:00:00.000Z'::timestamptz, '2028-11-02T00:00:00.000Z'::timestamptz, 'MSA', 'Micro Lab', 2000.000, 1980.000, 0, 200.000, '2026-08-02T09:27:06.215Z'::timestamptz, '2026-08-02T10:45:51.527Z'::timestamptz, NULL, NULL),
  (22, 1, 'Aspergillus brasiliensis ', 'Tody laboratories', '09402702', '2024-11-18T00:00:00.000Z'::timestamptz, '2026-09-30T00:00:00.000Z'::timestamptz, 'Asp.bra.', 'Micro refrigerator', 10.000, 8.000, 4, 2.000, '2026-08-02T09:29:32.072Z'::timestamptz, '2026-08-04T11:56:17.788Z'::timestamptz, '#16404', 'Aspergillus brasiliensis'),
  (23, 1, 'Candida albicans', 'Tody laboratories', '04206602', '2025-08-21T00:00:00.000Z'::timestamptz, '2027-05-31T00:00:00.000Z'::timestamptz, 'C.A', 'Micro refrigerator', 10.000, 9.000, 4, 2.000, '2026-08-02T09:31:27.203Z'::timestamptz, '2026-08-02T17:30:31.899Z'::timestamptz, '#10231', 'Candida albicans'),
  (24, 1, 'Burkholderia cepecia', 'Tody laboratories', '07/09/2023', '2023-12-25T00:00:00.000Z'::timestamptz, '2026-09-02T00:00:00.000Z'::timestamptz, 'B.C', 'Micro Freezer', 10.000, 8.000, 4, 2.000, '2026-08-02T09:34:05.776Z'::timestamptz, '2026-08-09T06:50:39.234Z'::timestamptz, ' #25416', 'Burkholderia cepacia'),
  (25, 1, 'Staphylococcus aureus', 'Tody laboratories', '04601701', '2026-03-17T00:00:00.000Z'::timestamptz, '2027-11-30T00:00:00.000Z'::timestamptz, 'S.A', 'Micro refrigerator', 10.000, 8.000, 4, 2.000, '2026-08-02T09:36:01.988Z'::timestamptz, '2026-08-02T16:09:53.340Z'::timestamptz, '#6538', 'Staphylococcus aureus'),
  (26, 1, 'Bacillus subtilis ', 'Tody laboratories', '02901902', '2026-03-17T00:00:00.000Z'::timestamptz, '2027-05-05T00:00:00.000Z'::timestamptz, 'B.S', 'Micro refrigerator', 10.000, 9.000, 4, 2.000, '2026-08-02T09:37:13.569Z'::timestamptz, '2026-08-02T17:30:17.323Z'::timestamptz, '# 6633', 'Bacillus subtilis'),
  (27, 0, 'R2A agar', 'OXOID', '6262818', '2026-08-02T00:00:00.000Z'::timestamptz, '2030-05-31T00:00:00.000Z'::timestamptz, NULL, 'Micro Lab', 2000.000, 1960.000, 0, 200.000, '2026-08-02T10:51:02.250Z'::timestamptz, '2026-08-02T13:32:24.927Z'::timestamptz, NULL, NULL)
) AS v(id, mtype, mname, mfr, batch, recvdate, expdate, code, loc, qtyrecv, qtyrem, unit, minstock, createdat, modifiedat, atcc, orgname)
WHERE NOT EXISTS (SELECT 1 FROM "Materials" t WHERE t."Id" = v.id);

-- ----------------------------------------------------------------------------
-- 11. Specifications (FK: ItemId resolved via Items.Code; (ItemId,TestCode) confirmed unique in source as of 2026-08-14 correction, guarded on both Id and business key)
-- ----------------------------------------------------------------------------
INSERT INTO "Specifications" ("Id","ItemId","TestCode","AlertLimit","ActionLimit","SpecLimit")
SELECT v.id, i."Id", v.testcode, v.alert, v.action, v.spec
FROM (VALUES
  (3, 'ost.tab', 'TYMC', '', '', '100'),
  (4, 'ost.tab', 'TAMC', '', '', '1000'),
  (5, 'SP101', 'TAMC-Water', '250', '350', '500'),
  (6, 'ost.liq', 'TAMC', '1000', '1000', '1000'),
  (7, 'ost.liq', 'TYMC', '100', '100', '100'),
  (8, 'P.Honey', 'TAMC', '', '', '1000'),
  (9, 'P.Honey', 'TYMC', '', '', '100'),
  (10, 'SP301', 'TAMC-Water', '50', '70', '100'),
  (11, 'Xanth gum', 'TAMC', '1000', '1000', '1000'),
  (12, 'Xanth gum', 'TYMC', '100', '100', '200')
) AS v(id, itemcode, testcode, alert, action, spec)
JOIN "Items" i ON i."Code" = v.itemcode
WHERE NOT EXISTS (SELECT 1 FROM "Specifications" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "Specifications" t WHERE t."ItemId" = i."Id" AND t."TestCode" = v.testcode
  );

-- ----------------------------------------------------------------------------
-- 12. TestWorkflowSteps (FK: TestDefinitionId via TestDefinitions.Code, MediaTypeId via MediaTypes.Class, TargetOrganismId via Organisms.ScientificName nullable)
-- ----------------------------------------------------------------------------
INSERT INTO "TestWorkflowSteps" ("Id","TestDefinitionId","StepOrder","StepName","MediaTypeId","IncubationMinHours","IncubationMaxHours","TemperatureMin","TemperatureMax","IsFinalStep","StepType","TargetOrganismId","RequiresIncubationTransfer")
SELECT v.id, td."Id", v.steporder, v.stepname, mt."Id", v.incmin, v.incmax, v.tmin, v.tmax, v.isfinal, v.steptype,
  (SELECT o."Id" FROM "Organisms" o WHERE o."ScientificName" = v.targetorg), v.reqtransfer
FROM (VALUES
  (1, 'TAMC', 1, 'TSA', 0, 2, 4, 30.00, 35.00, TRUE, 0, NULL, FALSE),
  (3, 'E.coli', 1, 'Broth enrichment', 1, 24, 71, 30.00, 35.00, FALSE, 1, NULL, FALSE),
  (4, 'E.coli', 3, 'MAR', 2, 24, 72, 30.00, 35.00, FALSE, 3, 'Escherichia coli', FALSE),
  (5, 'Salmonella ', 1, 'Broth enrichment', 1, 24, 24, 30.00, 35.00, FALSE, 1, NULL, FALSE),
  (8, 'BCC', 1, 'Broth enrichment ', 1, 24, 72, 30.00, 35.00, FALSE, 1, NULL, FALSE),
  (9, 'BCC', 2, 'BUR', 2, 24, 72, 30.00, 35.00, TRUE, 3, 'Burkholderia cepacia', FALSE),
  (12, 'Salmonella ', 2, 'RVS', 3, 24, 48, 30.00, 35.00, FALSE, 2, NULL, FALSE),
  (19, 'E.coli', 2, 'MBP', 3, 24, 48, 42.00, 44.00, FALSE, 2, NULL, FALSE),
  (20, 'P. aeruginosa', 1, 'Broth enrichment', 1, 24, 72, 30.00, 35.00, FALSE, 1, NULL, FALSE),
  (21, 'P. aeruginosa', 2, 'CAM', 2, 24, 72, 30.00, 35.00, TRUE, 3, NULL, FALSE),
  (23, 'TAMC-Water', 1, 'CountIncubation', 0, 72, 120, 30.00, 35.00, TRUE, 0, NULL, FALSE),
  (24, 'TAMC Passive air sample', 1, 'TSA', 0, 72, 96, 30.00, 35.00, FALSE, 0, NULL, TRUE),
  (26, 'TAMC Surface sample', 1, 'CountIncubation', 0, 72, 96, 30.00, 35.00, FALSE, 0, NULL, TRUE),
  (28, 'TYMC', 1, 'SDA', 0, 120, 168, 20.00, 25.00, TRUE, 0, NULL, FALSE),
  (29, 'S.aureus ', 2, 'MSA', 2, 24, 72, 30.00, 35.00, FALSE, 3, 'Staphylococcus aureus', FALSE),
  (30, 'S.aureus ', 1, 'Broth enrichment', 1, 24, 72, 30.00, 35.00, FALSE, 1, NULL, FALSE),
  (31, 'After cleaning TAMC', 1, 'first incubation', 0, 72, 96, 30.00, 35.00, FALSE, 0, NULL, TRUE),
  (32, 'After cleaning TAMC', 2, 'second Incubation', 0, 48, 72, 20.00, 25.00, TRUE, 0, NULL, FALSE),
  (34, 'Salmonella ', 3, 'XLD', 2, 24, 72, 30.00, 35.00, FALSE, 3, 'Salmonella  typhimurium', FALSE),
  (36, 'E.coli', 4, 'EMB', 2, 1, 2, 30.00, 35.00, TRUE, 4, 'Escherichia coli', FALSE),
  (37, 'TAMC-transfere', 1, 'Incubation', 0, 24, 72, 30.00, 35.00, FALSE, 0, NULL, TRUE),
  (39, 'Salmonella ', 4, 'TSI', 2, 0, 0, 0.00, 0.00, FALSE, 4, 'Salmonella  typhimurium', FALSE)
) AS v(id, tdcode, steporder, stepname, mtclass, incmin, incmax, tmin, tmax, isfinal, steptype, targetorg, reqtransfer)
JOIN "TestDefinitions" td ON td."Code" = v.tdcode
JOIN "MediaTypes" mt ON mt."Class" = v.mtclass
WHERE NOT EXISTS (SELECT 1 FROM "TestWorkflowSteps" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "TestWorkflowSteps" t WHERE t."TestDefinitionId" = td."Id" AND t."StepOrder" = v.steporder
  );

-- ----------------------------------------------------------------------------
-- 13. Media (FK: MediaTypeId via MediaTypes.Class, AutoclaveEquipmentId via Equipment.Code nullable, MaterialId literal verified against Materials identity -- see caveat; User-audit cols -> admin/null)
-- ----------------------------------------------------------------------------
INSERT INTO "Media" ("Id","MediaTypeId","LotNumber","ManufacturerLot","ManufacturerName","TotalWeight","TotalVolume","AutoclaveEquipmentId","AutoclaveProgram","LoadType","Temperature","CycleTime","CycleNumber","Ph","ExpiryDate","PreparedAt","Status","MaterialId","IsReleasedForUse","PreparedByUserId","ApprovalStatus","ApprovedAt","ApprovedByUserId")
SELECT v.id, mt."Id", v.lot, v.mfrlot, v.mfrname, v.totweight, v.totvol, aeq."Id", v.autoprog, v.loadtype, v.temp, v.cycletime, v.cyclenum, v.ph, v.expdate, v.prepat, v.status, v.materialid, v.released, (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' ORDER BY "Id" LIMIT 1), v.approvalstatus, v.approvedat,
  CASE WHEN v.hadapprover THEN (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' ORDER BY "Id" LIMIT 1) ELSE NULL END
FROM (VALUES
  (4, 0, 'TSA/01/26', 'SEED-0001', 'Seed Data', 0, '', NULL, '', '', 0, 0, 0, 0, '2027-08-01T07:48:17.268Z'::timestamptz, '2026-08-01T07:48:17.256Z'::timestamptz, 1, 4, 'Tryptic Soy Agar', 'SEED-0001', TRUE, 1, NULL, FALSE),
  (5, 0, 'TSA/02/26', 'SEED-0001', 'Seed Data', 100, '500 ml', 'AUT-01', 'Program A', 'agar (500 ml)', 121, 15, 1, 7.2, '2027-08-01T00:00:00.000Z'::timestamptz, '2026-08-01T08:22:31.233Z'::timestamptz, 0, 4, 'Tryptic Soy Agar', 'SEED-0001', FALSE, 0, NULL, FALSE),
  (6, 0, 'TSA/03/26', 'SEED-0001', 'Seed Data', 40, '1 liter', 'AUT-01', 'liquid 500 ml', 'full load', 121, 34, 12, 7.2, '2026-09-15T00:00:00.000Z'::timestamptz, '2026-08-01T17:14:56.579Z'::timestamptz, 0, 4, 'Tryptic Soy Agar', 'SEED-0001', FALSE, 0, NULL, FALSE),
  (7, 1, 'TSB/01/26', 'VM1091459', 'Merck', 30, '1000 ml', 'AUT-01', '1000 ml', 'maximum', 121, 34, 112, 7.2, '2026-09-15T00:00:00.000Z'::timestamptz, '2026-08-01T19:37:26.301Z'::timestamptz, 1, 7, 'Tryptic soy broth', 'VM1091459', TRUE, 1, NULL, FALSE),
  (8, 1, 'TSB/02/26', 'VM1091459', 'Merck', 30, '1000 ml', 'AUT-01', '1000 ml', 'minimum ', 121, 32, 110, 7.2, '2026-09-30T00:00:00.000Z'::timestamptz, '2026-08-01T19:40:21.998Z'::timestamptz, 1, 7, 'Tryptic soy broth', 'VM1091459', TRUE, 1, NULL, FALSE),
  (9, 0, 'TSA/04/26', 'SEED-0001', 'Seed Data', 100, '500 ml', 'AUT-01', 'Program A', 'agar (500 ml)', 121, 15, 1, 7.2, '2027-08-01T00:00:00.000Z'::timestamptz, '2026-08-01T19:42:44.505Z'::timestamptz, 1, 4, 'Tryptic Soy Agar', 'SEED-0001', TRUE, 1, NULL, FALSE),
  (10, 2, 'XLD/01/26', 'XLD-001', 'Himedia', 50, '500 ml', 'AUT-01', 'Program A', 'agar (500 ml)', 121, 15, 1, 7.2, '2027-08-01T00:00:00.000Z'::timestamptz, '2026-08-01T19:48:20.568Z'::timestamptz, 1, 5, 'XLD Agar', 'XLD-001', TRUE, 1, '2026-08-06T08:43:52.347Z'::timestamptz, TRUE),
  (11, 1, 'TSB/03/26', 'VM1091459', 'Merck', 50, '500 ml', 'AUT-01', 'Program A', 'liquid (500 ml)', 121, 15, 1, 7.2, '2027-08-01T00:00:00.000Z'::timestamptz, '2026-08-01T19:54:05.578Z'::timestamptz, 1, 7, 'Tryptic soy broth', 'VM1091459', TRUE, 1, NULL, FALSE),
  (12, 2, 'BUR/02/26', '3749511', 'OXOID', 20, '1000', 'AUT-01', '1000 ml bottles', 'full', 121, 30, 120, 7.2, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:37:35.839Z'::timestamptz, 1, 20, ' Burkholderia Cepacia Medium', '3749511', TRUE, 1, NULL, FALSE),
  (13, 2, 'CAM/03/26', 'VM1077984', 'Merck', 10, '1000 ml', 'AUT-01', '1000', 'full', 121, 30, 14, 7.1, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:41:08.352Z'::timestamptz, 1, 6, 'Cetrimide Agar', 'VM1077984', TRUE, 1, NULL, FALSE),
  (14, 2, 'EMB/04/26', '6205653', 'Oxoid', 20, '1000', 'AUT-01', '1000', 'full', 121, 30, 15, 7.1, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:42:17.154Z'::timestamptz, 1, 17, 'Eosin methylene blue agar', '6205653', TRUE, 1, NULL, FALSE),
  (15, 2, 'MAR/05/26', 'VM1057505', 'Merck', 20, '1000', 'AUT-01', '1000', 'full', 121, 30, 16, 7.4, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:43:12.912Z'::timestamptz, 1, 9, 'MacConkey agar', 'VM1057505', TRUE, 1, NULL, FALSE),
  (16, 2, 'MSA/06/26', '3754600', 'Oxoid ', 20, '1000', 'AUT-01', '1000', 'full', 121, 30, 17, 7.3, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:45:51.532Z'::timestamptz, 1, 21, 'Mannitol salt agar ', '3754600', TRUE, 1, NULL, FALSE),
  (17, 3, 'RVS/01/26', '0000682699', 'Himedia', 20, '1000', 'AUT-01', '1000', 'full', 115, 20, 18, 7.2, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:46:40.519Z'::timestamptz, 1, 18, 'Rappaport Vasiliadis Salmonella enrichment broth', '0000682699', TRUE, 1, '2026-08-06T08:43:47.277Z'::timestamptz, TRUE),
  (18, 3, 'MBP/02/26', '6267096', 'Oxoid', 20, '1000', 'AUT-01', '1000', 'full', 121, 30, 18, 7.1, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:47:41.974Z'::timestamptz, 1, 16, 'Macconkey broth Purple', '6267096', TRUE, 1, NULL, FALSE),
  (19, 0, 'SDA/05/26', '3773319', 'OXOID', 20, '1000', 'AUT-01', '1000', 'full', 121, 30, 19, 7.2, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:48:23.680Z'::timestamptz, 1, 15, 'Sabouraud dextrose agar', '3773319', TRUE, 1, NULL, FALSE),
  (20, 0, 'R2AAGAR/06/26', '6262818', 'OXOID', 20, '2000', 'AUT-01', '1000', 'FULL', 121, 30, 20, 7.1, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:51:51.785Z'::timestamptz, 0, 27, 'R2A agar', '6262818', FALSE, 0, NULL, FALSE),
  (21, 2, 'TSI/07/26', '0000608756', 'Himedia', 20, '1000', 'AUT-01', '1000', 'FULL', 121, 30, 21, 7.3, '2026-09-25T00:00:00.000Z'::timestamptz, '2026-08-02T10:53:00.369Z'::timestamptz, 3, 19, 'Triple Sugar Iron Agar', '0000608756', FALSE, 2, NULL, FALSE),
  (22, 0, 'R2AAGAR/07/26', '6262818', 'OXOID', 20, '1000', 'AUT-01', '2', 'full', 121, 30, 12, 7.1, '2026-09-23T00:00:00.000Z'::timestamptz, '2026-08-02T13:32:24.935Z'::timestamptz, 1, 27, 'R2A agar', '6262818', TRUE, 1, NULL, FALSE),
  (23, 2, 'MAR/08/26', 'VM1057505', 'Merck', 20, '1000', 'AUT-01', 'liquid', 'full', 121, 15, 11, 7.1, '2026-09-30T00:00:00.000Z'::timestamptz, '2026-08-03T22:30:19.076Z'::timestamptz, 3, 9, 'MacConkey agar', 'VM1057505', FALSE, 0, NULL, FALSE),
  (24, 2, 'XLD/09/26', 'XLD-001', 'Himedia', 200, '5000', 'AUT-01', '1000 ml', 'Full', 121, 30, 121, 7.5, '2026-10-05T00:00:00.000Z'::timestamptz, '2026-08-05T13:33:06.807Z'::timestamptz, 1, 5, 'XLD Agar', 'XLD-001', TRUE, 1, '2026-08-06T08:43:38.131Z'::timestamptz, TRUE),
  (25, 3, 'RVS/03/26', '0000682699', 'Himedia', 10, '1000', 'AUT-01', 'test', 'test', 121, 35, 121, 7.2, '2026-09-05T00:00:00.000Z'::timestamptz, '2026-08-05T13:38:18.035Z'::timestamptz, 1, 18, 'Rappaport Vasiliadis Salmonella enrichment broth', '0000682699', TRUE, 1, '2026-08-06T08:43:32.077Z'::timestamptz, TRUE),
  (26, 2, 'TSI/10/26', '0000608756', 'Himedia', 200, '5000', 'AUT-01', '1000ml', 'full', 121, 30, 187, 6.7, '2026-09-06T00:00:00.000Z'::timestamptz, '2026-08-06T09:02:24.923Z'::timestamptz, 1, 19, 'Triple Sugar Iron Agar', '0000608756', TRUE, 1, '2026-08-07T11:54:16.390Z'::timestamptz, TRUE)
) AS v(id, mtclass, lot, mfrlot, mfrname, totweight, totvol, autoclavecode, autoprog, loadtype, temp, cycletime, cyclenum, ph, expdate, prepat, status, materialid, matname, matbatch, released, approvalstatus, approvedat, hadapprover)
JOIN "MediaTypes" mt ON mt."Class" = v.mtclass
LEFT JOIN "Equipment" aeq ON aeq."Code" = v.autoclavecode
WHERE NOT EXISTS (SELECT 1 FROM "Media" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "Media" t WHERE t."LotNumber" = v.lot)
  -- Safety check: the Material row occupying MaterialId must actually be the
  -- one this Media lot was prepared from (Materials has no reliable business
  -- key -- see plan doc). Skips the row rather than attaching to a stranger.
  AND EXISTS (
    SELECT 1 FROM "Materials" m WHERE m."Id" = v.materialid
      AND m."MaterialName" = v.matname AND m."BatchNumber" = v.matbatch
  );

-- ----------------------------------------------------------------------------
-- 14. TestWorkflowStepMedias (FK: TestWorkflowStepId via (TestDefinitions.Code, StepOrder), MaterialId literal verified against Materials identity -- see caveat)
-- ----------------------------------------------------------------------------
INSERT INTO "TestWorkflowStepMedias" ("Id","TestWorkflowStepId","MaterialId","TempMin","TempMax","IsRequired","DisplayOrder")
SELECT v.id, tws."Id", v.materialid, v.tempmin, v.tempmax, v.isrequired, v.displayorder
FROM (VALUES
  (1, 'TAMC', 1, 4, 'Tryptic Soy Agar', 'SEED-0001', 30.00, 35.00, TRUE, 0),
  (5, 'E.coli', 3, 9, 'MacConkey agar', 'VM1057505', 30.00, 35.00, TRUE, 0),
  (6, 'E.coli', 4, 17, 'Eosin methylene blue agar', '6205653', 30.00, 35.00, FALSE, 0),
  (10, 'E.coli', 1, 7, 'Tryptic soy broth', 'VM1091459', 30.00, 35.00, TRUE, 0),
  (11, 'E.coli', 2, 16, 'Macconkey broth Purple', '6267096', 42.00, 44.00, TRUE, 0),
  (14, 'BCC', 1, 7, 'Tryptic soy broth', 'VM1091459', 30.00, 35.00, TRUE, 0),
  (15, 'BCC', 2, 20, ' Burkholderia Cepacia Medium', '3749511', 30.00, 35.00, TRUE, 0),
  (17, 'After cleaning TAMC', 1, 4, 'Tryptic Soy Agar', 'SEED-0001', 30.00, 35.00, TRUE, 0),
  (18, 'TAMC-transfere', 1, 4, 'Tryptic Soy Agar', 'SEED-0001', 30.00, 35.00, TRUE, 0),
  (20, 'TYMC', 1, 15, 'Sabouraud dextrose agar', '3773319', 20.00, 25.00, TRUE, 0),
  (21, 'TAMC Surface sample', 1, 4, 'Tryptic Soy Agar', 'SEED-0001', 30.00, 35.00, TRUE, 0),
  (22, 'TAMC-Water', 1, 27, 'R2A agar', '6262818', 30.00, 35.00, TRUE, 0),
  (24, 'Salmonella ', 1, 7, 'Tryptic soy broth', 'VM1091459', 30.00, 35.00, TRUE, 0),
  (25, 'Salmonella ', 2, 18, 'Rappaport Vasiliadis Salmonella enrichment broth', '0000682699', 30.00, 35.00, TRUE, 0),
  (27, 'Salmonella ', 4, 19, 'Triple Sugar Iron Agar', '0000608756', 30.00, 35.00, FALSE, 0),
  (28, 'Salmonella ', 3, 5, 'XLD Agar', 'XLD-001', 30.00, 35.00, TRUE, 0),
  (29, 'S.aureus ', 1, 7, 'Tryptic soy broth', 'VM1091459', 30.00, 35.00, TRUE, 0),
  (31, 'S.aureus ', 2, 21, 'Mannitol salt agar ', '3754600', 30.00, 35.00, TRUE, 0)
) AS v(id, tdcode, steporder, materialid, matname, matbatch, tempmin, tempmax, isrequired, displayorder)
JOIN "TestDefinitions" td ON td."Code" = v.tdcode
JOIN "TestWorkflowSteps" tws ON tws."TestDefinitionId" = td."Id" AND tws."StepOrder" = v.steporder
WHERE NOT EXISTS (SELECT 1 FROM "TestWorkflowStepMedias" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "TestWorkflowStepMedias" t WHERE t."TestWorkflowStepId" = tws."Id" AND t."MaterialId" = v.materialid
  )
  -- Safety check: verify the Material identity at that Id, same reasoning as Media above.
  AND EXISTS (
    SELECT 1 FROM "Materials" m WHERE m."Id" = v.materialid
      AND m."MaterialName" = v.matname AND m."BatchNumber" = v.matbatch
  );

-- ----------------------------------------------------------------------------
-- Sequence resets (align identity sequences with the highest Id now present)
-- ----------------------------------------------------------------------------
SELECT setval(pg_get_serial_sequence('"Departments"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Departments"), 1), true);
SELECT setval(pg_get_serial_sequence('"Organisms"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Organisms"), 1), true);
SELECT setval(pg_get_serial_sequence('"MediaTypes"', 'Id'), COALESCE((SELECT MAX("Id") FROM "MediaTypes"), 1), true);
SELECT setval(pg_get_serial_sequence('"Equipment"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Equipment"), 1), true);
SELECT setval(pg_get_serial_sequence('"EquipmentInventories"', 'Id'), COALESCE((SELECT MAX("Id") FROM "EquipmentInventories"), 1), true);
SELECT setval(pg_get_serial_sequence('"Items"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Items"), 1), true);
SELECT setval(pg_get_serial_sequence('"TestDefinitions"', 'Id'), COALESCE((SELECT MAX("Id") FROM "TestDefinitions"), 1), true);
SELECT setval(pg_get_serial_sequence('"WaterSamplingPoints"', 'Id'), COALESCE((SELECT MAX("Id") FROM "WaterSamplingPoints"), 1), true);
SELECT setval(pg_get_serial_sequence('"Rooms"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Rooms"), 1), true);
SELECT setval(pg_get_serial_sequence('"Materials"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Materials"), 1), true);
SELECT setval(pg_get_serial_sequence('"Specifications"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Specifications"), 1), true);
SELECT setval(pg_get_serial_sequence('"TestWorkflowSteps"', 'Id'), COALESCE((SELECT MAX("Id") FROM "TestWorkflowSteps"), 1), true);
SELECT setval(pg_get_serial_sequence('"Media"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Media"), 1), true);
SELECT setval(pg_get_serial_sequence('"TestWorkflowStepMedias"', 'Id'), COALESCE((SELECT MAX("Id") FROM "TestWorkflowStepMedias"), 1), true);

COMMIT;

-- ============================================================================
-- POST-MIGRATION VERIFICATION (run manually after COMMIT, not part of the
-- transaction). Compares actual row counts to the expected source counts.
-- A lower actual count than expected means some rows were skipped by a
-- guard (Id already taken, business key already present, or a Materials
-- identity check failed) -- investigate before assuming the migration is
-- complete. Expected counts are >= these numbers because some pre-existing
-- production rows may already legitimately overlap.
-- ============================================================================
SELECT
  (SELECT count(*) FROM "Departments") AS "Departments_count",
  (SELECT count(*) FROM "Organisms") AS "Organisms_count",
  (SELECT count(*) FROM "MediaTypes") AS "MediaTypes_count",
  (SELECT count(*) FROM "Equipment") AS "Equipment_count",
  (SELECT count(*) FROM "EquipmentInventories") AS "EquipmentInventories_count",
  (SELECT count(*) FROM "Items") AS "Items_count",
  (SELECT count(*) FROM "TestDefinitions") AS "TestDefinitions_count",
  (SELECT count(*) FROM "WaterSamplingPoints") AS "WaterSamplingPoints_count",
  (SELECT count(*) FROM "Rooms") AS "Rooms_count",
  (SELECT count(*) FROM "Materials") AS "Materials_count",
  (SELECT count(*) FROM "Specifications") AS "Specifications_count",
  (SELECT count(*) FROM "TestWorkflowSteps") AS "TestWorkflowSteps_count",
  (SELECT count(*) FROM "Media") AS "Media_count",
  (SELECT count(*) FROM "TestWorkflowStepMedias") AS "TestWorkflowStepMedias_count"
;
-- Expected source row counts: Departments=4, Organisms=11, MediaTypes=4, Equipment=6, EquipmentInventories=11, Items=10, TestDefinitions=12, WaterSamplingPoints=5, Rooms=9, Materials=24, Specifications=10, TestWorkflowSteps=22, Media=23, TestWorkflowStepMedias=18