-- ============================================================================
-- MicroLIMS Phase 2 migration: operational/historical data, source -> Neon
-- Generated 2026-08-14T17:48:35.095Z
-- DO NOT EXECUTE without review. Depends on Phase 1 already being applied to Neon.
-- ============================================================================

BEGIN;

-- ----------------------------------------------------------------------------
-- Pre-flight guards
-- ----------------------------------------------------------------------------
-- 1) The three explicitly-approved Neon user identities must exist exactly as
--    specified (by Id AND Username together) -- we never assume a source Id
--    equals a Neon Id; these three pairings were explicitly approved by the
--    human reviewer and are checked, not assumed.
-- 2) Phase 1 master data (Organisms/Materials/Media/Equipment) must already be
--    present -- Phase 2 has hard dependencies on all four.
-- 3) The two system Roles needed to create historical-attribution-only Neon
--    users (Section Head, Analyst) must exist (seeded by DbSeeder in every
--    environment on first migration).
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Id" = 1 AND "Username" = 'admin') THEN
    RAISE EXCEPTION 'Pre-flight failed: Neon Users.Id=1 is not Username=''admin'' as approved. Aborting.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Id" = 2 AND "Username" = 'MMA') THEN
    RAISE EXCEPTION 'Pre-flight failed: Neon Users.Id=2 is not Username=''MMA'' as approved. Aborting.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Id" = 5 AND "Username" = 'MMASH') THEN
    RAISE EXCEPTION 'Pre-flight failed: Neon Users.Id=5 is not Username=''MMASH'' as approved. Aborting.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Organisms") THEN
    RAISE EXCEPTION 'Pre-flight failed: Phase 1 Organisms data not found. Run Phase 1 first.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Materials") THEN
    RAISE EXCEPTION 'Pre-flight failed: Phase 1 Materials data not found. Run Phase 1 first.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Media") THEN
    RAISE EXCEPTION 'Pre-flight failed: Phase 1 Media data not found. Run Phase 1 first.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Equipment") THEN
    RAISE EXCEPTION 'Pre-flight failed: Phase 1 Equipment data not found. Run Phase 1 first.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Roles" WHERE "Type" = 1) THEN
    RAISE EXCEPTION 'Pre-flight failed: Roles.Type=1 (Section Head) not found -- needed to create the Amal Hamdy historical user.';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM "Roles" WHERE "Type" = 3) THEN
    RAISE EXCEPTION 'Pre-flight failed: Roles.Type=3 (Analyst) not found -- needed to create the MMAAN historical user.';
  END IF;
END $$;

-- ----------------------------------------------------------------------------
-- Approved user-attribution mapping (session-local function, dropped
-- automatically at end of transaction/session -- documents every intentional
-- source-UserId -> Neon-UserId transformation in one place).
-- ----------------------------------------------------------------------------
-- Parameter is numeric (not integer): the N() helper types NULL literals as
-- NULL::numeric so all-NULL VALUES columns don't get misinferred as text, and
-- that unifies the whole column (including non-null rows) to numeric.
CREATE FUNCTION pg_temp.phase2_map_user(source_user_id numeric) RETURNS integer AS $$
  SELECT CASE source_user_id
    -- Invalid source sentinel (0 was never a valid Users.Id in the source DB --
    -- Ids start at 1). Mapped to Neon admin ONLY as an "unattributed/system"
    -- placeholder to satisfy a NOT NULL column. This is NOT evidence that the
    -- Neon admin account actually performed these actions historically.
    WHEN 0 THEN 1
    WHEN 1 THEN 1   -- source admin (Id 1)   -> Neon admin (Id 1), approved mapping
    WHEN 4 THEN 2   -- source MMA (Id 4)     -> Neon MMA (Id 2), approved mapping (explicit Id pair, not inferred from matching username)
    WHEN 5 THEN 5   -- source MMASH (Id 5)   -> Neon MMASH (Id 5), approved mapping (explicit Id pair)
    WHEN 7 THEN (SELECT "Id" FROM "Users" WHERE "Username" = 'MMAAN' ORDER BY "Id" LIMIT 1)        -- source MMAAN (Id 7) -> newly created Neon historical-attribution-only user
    WHEN 10 THEN (SELECT "Id" FROM "Users" WHERE "Username" = 'Amal Hamdy' ORDER BY "Id" LIMIT 1)  -- source Amal Hamdy (Id 10) -> newly created Neon historical-attribution-only user
    ELSE NULL
  END;
$$ LANGUAGE sql STABLE;

-- ----------------------------------------------------------------------------
-- Historical-attribution-only Neon users (created only if missing; never
-- overwritten if already present). No PasswordHash, RefreshTokens,
-- PasswordResetTokens, PasswordHistories, or login history copied from the
-- old database -- PasswordHash is set to a fixed marker that cannot
-- authenticate, and the account is created inactive (no login required).
-- ----------------------------------------------------------------------------
INSERT INTO "Users" ("FullName","Username","PasswordHash","RoleId","IsActive","CreatedAt","FailedLoginAttempts","MustChangePassword")
SELECT 'Mohamed Mahmoud', 'MMAAN', 'MIGRATED_HISTORICAL_NO_LOGIN', (SELECT "Id" FROM "Roles" WHERE "Type" = 3), FALSE, now(), 0, TRUE
WHERE NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = 'MMAAN');

INSERT INTO "Users" ("FullName","Username","PasswordHash","RoleId","IsActive","CreatedAt","FailedLoginAttempts","MustChangePassword")
SELECT 'Amal Hamdy', 'Amal Hamdy', 'MIGRATED_HISTORICAL_NO_LOGIN', (SELECT "Id" FROM "Roles" WHERE "Type" = 1), FALSE, now(), 0, TRUE
WHERE NOT EXISTS (SELECT 1 FROM "Users" WHERE "Username" = 'Amal Hamdy');

-- ----------------------------------------------------------------------------
-- 1. MediaChallengeSpecs (FK: OrganismId via Organisms.ScientificName; no User refs)
-- ----------------------------------------------------------------------------
INSERT INTO "MediaChallengeSpecs" ("Id","MaterialName","EvaluationType","ChallengeRole","ExpectedDescription","OrganismId")
SELECT v.id, v.matname, v.evaltype, v.role, v.expdesc, o."Id"
FROM (VALUES
  (1, 'Tryptic Soy Agar (Dehydrated)', 0, NULL::numeric, NULL::text, 'Bacillus subtilis'),
  (2, 'XLD Agar', 1, 1, 'Black centered colonies', 'Salmonella  typhimurium'),
  (3, 'MacConkey agar', 1, 0, NULL::text, 'Staphylococcus aureus'),
  (4, 'XLD Agar', 1, 0, NULL::text, 'Escherichia coli'),
  (5, 'Tryptic soy broth', 2, NULL::numeric, NULL::text, 'Escherichia coli'),
  (6, 'Tryptic soy broth', 2, NULL::numeric, NULL::text, 'Aspergillus brasiliensis'),
  (7, 'Tryptic soy broth', 2, NULL::numeric, NULL::text, 'Staphylococcus aureus'),
  (8, ' Burkholderia Cepacia Medium', 1, 0, NULL::text, 'Pseudomonas aeruginosa'),
  (9, ' Burkholderia Cepacia Medium', 1, 1, 'Greenish–brown colonies with yellow halo', 'Burkholderia cepacia'),
  (10, 'Cetrimide Agar', 1, 1, 'yellow-green or yellow-brown fluorescent pyoverdin', 'Pseudomonas aeruginosa'),
  (11, 'Cetrimide Agar', 1, 0, NULL::text, 'Escherichia coli'),
  (12, 'Eosin methylene blue agar', 1, 1, 'metallic sheen with a dark center', 'Escherichia coli'),
  (13, 'Eosin methylene blue agar', 1, 0, NULL::text, 'Staphylococcus aureus'),
  (14, 'MacConkey agar', 1, 1, 'pink to dark pink, non-mucoid, surrounded by darker pink halo of precipitated bile salts', 'Escherichia coli'),
  (16, 'Macconkey broth Purple', 1, 0, NULL::text, 'Staphylococcus aureus'),
  (17, 'Macconkey broth Purple', 1, 1, 'Growth ,Acid production (yellow) ,Gas production', 'Escherichia coli'),
  (18, 'Mannitol salt agar ', 1, 0, NULL::text, 'Escherichia coli'),
  (19, 'Mannitol salt agar ', 1, 1, 'yellow colonies', 'Staphylococcus aureus'),
  (21, 'Rappaport Vasiliadis Salmonella enrichment broth', 1, 0, NULL::text, 'Staphylococcus aureus'),
  (22, 'Sabouraud dextrose agar', 0, NULL::numeric, NULL::text, 'Aspergillus brasiliensis'),
  (23, 'Sabouraud dextrose agar', 0, NULL::numeric, NULL::text, 'Candida albicans'),
  (24, 'Triple Sugar Iron Agar', 1, 1, 'Slant red and butt yellow with black colour ', 'Salmonella  typhimurium'),
  (25, 'Triple Sugar Iron Agar', 1, 0, NULL::text, 'Staphylococcus aureus'),
  (26, 'Tryptic Soy Agar (Dehydrated)', 0, NULL::numeric, NULL::text, 'Staphylococcus aureus'),
  (27, 'Tryptic Soy Agar (Dehydrated)', 0, NULL::numeric, NULL::text, 'Pseudomonas aeruginosa'),
  (28, 'Tryptic Soy Agar (Dehydrated)', 0, NULL::numeric, NULL::text, 'Aspergillus brasiliensis'),
  (29, 'Tryptic Soy Agar (Dehydrated)', 0, NULL::numeric, NULL::text, 'Candida albicans'),
  (31, 'R2A agar', 0, NULL::numeric, NULL::text, 'Staphylococcus aureus'),
  (35, 'R2A agar', 0, NULL::numeric, NULL::text, 'Aspergillus brasiliensis'),
  (36, 'R2A agar', 0, NULL::numeric, NULL::text, 'Bacillus subtilis'),
  (37, 'R2A agar', 0, NULL::numeric, NULL::text, 'Candida albicans'),
  (38, 'R2A agar', 0, NULL::numeric, NULL::text, 'Pseudomonas aeruginosa'),
  (40, 'Rappaport Vasiliadis Salmonella enrichment broth', 1, 1, 'turbid', 'Salmonella  typhimurium')
) AS v(id, matname, evaltype, role, expdesc, orgname)
JOIN "Organisms" o ON o."ScientificName" = v.orgname
WHERE NOT EXISTS (SELECT 1 FROM "MediaChallengeSpecs" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "MediaChallengeSpecs" t
    WHERE t."MaterialName" = v.matname AND t."OrganismId" = o."Id"
      AND t."EvaluationType" = v.evaltype AND (t."ChallengeRole" IS NOT DISTINCT FROM v.role)
  );

-- ----------------------------------------------------------------------------
-- 2. Machines (no FK dependencies; no User refs)
-- ----------------------------------------------------------------------------
INSERT INTO "Machines" ("Id","Name")
SELECT v.id, v.name
FROM (VALUES
  (1, 'CTX'),
  (2, 'CAM'),
  (3, 'PG'),
  (4, 'OSD I'),
  (5, 'Fette'),
  (6, 'CMb4D'),
  (7, 'ACG')
) AS v(id, name)
WHERE NOT EXISTS (SELECT 1 FROM "Machines" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "Machines" t WHERE t."Name" = v.name);

-- ----------------------------------------------------------------------------
-- 3. MachineParts (FK: MachineId via Machines.Name; no User refs)
-- ----------------------------------------------------------------------------
INSERT INTO "MachineParts" ("Id","MachineId","Name")
SELECT v.id, mch."Id", v.name
FROM (VALUES
  (1, 'CTX', 'hopper'),
  (2, 'OSD I', 'Blender'),
  (3, 'OSD I', 'FBD'),
  (4, 'OSD I', 'FBD filter'),
  (5, 'OSD I', 'Powel 1 '),
  (6, 'OSD I', 'Powel 2 '),
  (7, 'OSD I', 'RMG'),
  (8, 'OSD I', 'Tipper'),
  (9, 'OSD I', 'Multi Mill'),
  (10, 'OSD I', 'Vibrosifter '),
  (11, 'CTX', 'outlet nozzel'),
  (12, 'CTX', 'St.St Scoop'),
  (13, 'CTX', 'Hopper Pipe'),
  (14, 'CTX', 'Hopper pipe rubber '),
  (15, 'CTX', 'Feeder 1'),
  (16, 'CTX', 'Fan 1'),
  (17, 'CTX', 'Fan 2 '),
  (18, 'CTX', 'Gate descent powder'),
  (19, 'CTX', 'Barrier powder'),
  (20, 'CTX', 'Barrier tablets '),
  (21, 'CTX', 'Feeder pipe '),
  (22, 'CTX', 'Feeder 2'),
  (23, 'CTX', 'Turret die table'),
  (24, 'CTX', 'Inlet metal detector '),
  (25, 'CAM', 'hopper')
) AS v(id, machinename, name)
JOIN "Machines" mch ON mch."Name" = v.machinename
WHERE NOT EXISTS (SELECT 1 FROM "MachineParts" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "MachineParts" t WHERE t."MachineId" = mch."Id" AND t."Name" = v.name
  );

-- ----------------------------------------------------------------------------
-- 4. MachinePartConfigurations (FK: MachinePartId via (Machine.Name, MachinePart.Name); no User refs)
-- ----------------------------------------------------------------------------
INSERT INTO "MachinePartConfigurations" ("Id","MachinePartId","TestType","TestCode","AlertLimit","ActionLimit","SpecLimit","IsPathogenTest")
SELECT v.id, mp."Id", v.testtype, v.testcode, v.alert, v.action, v.spec, v.ispathogen
FROM (VALUES
  (1, 'CTX', 'hopper', 'Swab', 'After cleaning TAMC', '50', '50', '50', FALSE),
  (2, 'OSD I', 'Blender', 'Rinse', 'After cleaning TAMC', '', '', '100', FALSE),
  (3, 'CTX', 'outlet nozzel', 'Swab', 'After cleaning TAMC', '50', '50', '50', FALSE),
  (4, 'CTX', 'St.St Scoop', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (5, 'CTX', 'Hopper Pipe', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (6, 'CTX', 'Hopper pipe rubber ', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (7, 'CTX', 'Feeder 1', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (8, 'CTX', 'Fan 1', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (9, 'CTX', 'Fan 2 ', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (10, 'CTX', 'Gate descent powder', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (11, 'CTX', 'Barrier powder', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (12, 'CTX', 'Barrier tablets ', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (13, 'CTX', 'Feeder pipe ', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (15, 'CTX', 'Feeder 2', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (16, 'CTX', 'Turret die table', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (17, 'CTX', 'Inlet metal detector ', 'Swab', 'After cleaning TAMC', '', '', '50', FALSE),
  (18, 'CAM', 'hopper', 'Swab', 'TAMC-transfere', '50', '50', '50', FALSE)
) AS v(id, machinename, partname, testtype, testcode, alert, action, spec, ispathogen)
JOIN "Machines" mch ON mch."Name" = v.machinename
JOIN "MachineParts" mp ON mp."MachineId" = mch."Id" AND mp."Name" = v.partname
WHERE NOT EXISTS (SELECT 1 FROM "MachinePartConfigurations" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "MachinePartConfigurations" t WHERE t."MachinePartId" = mp."Id" AND t."TestCode" = v.testcode
  );

-- ----------------------------------------------------------------------------
-- 5. Cryovials (FK: MaterialId literal verified against Materials identity -- no reliable business key, same caveat as Phase 1; OrganismId via Organisms.ScientificName; PreparedByUserId/ApprovedByUserId via phase2_map_user)
-- ----------------------------------------------------------------------------
INSERT INTO "Cryovials" ("Id","Code","VialsRemaining","MaterialId","ManufacturerName","ExpiryDate","NumberOfVialsPrepared","StorageCondition","PhysicalCheckText","ApprovalStatus","IsDestroyed","OrganismNameSnapshot","PreparedAt","OrganismId","PreparedByUserId","ApprovedAt","ApprovedByUserId")
SELECT v.id, v.code, v.vialsremaining, v.materialid, v.manufacturername, v.expdate, v.numprepared, v.storage, v.physcheck, v.approvalstatus, v.isdestroyed, v.orgsnapshot, v.preparedat, o."Id", pg_temp.phase2_map_user(v.preparedbyuserid), v.approvedat, pg_temp.phase2_map_user(v.approvedbyuserid)
FROM (VALUES
  (2, '8739/01/26', 17, 10, 'Escherichia coli', '03802303', 'Tody laboratories', '2027-08-01T00:00:00.000Z'::timestamptz, 20, '-20', 'Conform', 1, FALSE, 'E. coli #8739', '2026-08-01T17:13:23.082Z'::timestamptz, 'Burkholderia cenocepacia', 0, NULL::timestamptz, NULL::numeric),
  (3, 'ESCHERICHIACOLI/01/26', 5, 12, 'Escherichia coli', 'LOT-EC-01', 'Tody laboratories', '2027-02-01T00:00:00.000Z'::timestamptz, 5, 'Freezer -15 to -25', 'OK', 1, TRUE, 'Escherichia coli', '2026-08-01T17:15:51.544Z'::timestamptz, 'Escherichia coli', 0, NULL::timestamptz, NULL::numeric),
  (4, '# 14028/01/26', 18, 14, 'Salmonella  typhimurium', '01103101', 'Tody laboratories', '2027-08-02T00:00:00.000Z'::timestamptz, 20, 'Deep Freezer', 'Conform', 1, FALSE, 'Salmonella  typhimurium', '2026-08-02T06:40:19.648Z'::timestamptz, 'Salmonella  typhimurium', 0, NULL::timestamptz, NULL::numeric),
  (5, '#16404/01/26', 19, 22, 'Aspergillus brasiliensis ', '09402702', 'Tody laboratories', '2027-08-02T00:00:00.000Z'::timestamptz, 20, 'deep freezer', 'Pure', 1, FALSE, 'Aspergillus brasiliensis ', '2026-08-02T10:26:09.744Z'::timestamptz, 'Aspergillus brasiliensis', 0, NULL::timestamptz, NULL::numeric),
  (6, '# 6633/01/26', 17, 26, 'Bacillus subtilis ', '02901902', 'Tody laboratories', '2027-08-02T00:00:00.000Z'::timestamptz, 20, 'deep freeze', 'pure', 1, FALSE, 'Bacillus subtilis ', '2026-08-02T10:29:26.339Z'::timestamptz, 'Bacillus subtilis', 0, NULL::timestamptz, NULL::numeric),
  (7, ' #25416/01/26', 19, 24, 'Burkholderia cepecia', '07/09/2023', 'Tody laboratories', '2027-09-02T00:00:00.000Z'::timestamptz, 20, 'deep freeze', 'pure', 1, FALSE, 'Burkholderia cepecia', '2026-08-02T10:30:58.489Z'::timestamptz, 'Burkholderia cepacia', 0, NULL::timestamptz, NULL::numeric),
  (8, '#10231/01/26', 19, 23, 'Candida albicans', '04206602', 'Tody laboratories', '2027-08-02T00:00:00.000Z'::timestamptz, 20, 'deep freezer', 'pure', 1, FALSE, 'Candida albicans', '2026-08-02T10:31:43.306Z'::timestamptz, 'Candida albicans', 0, NULL::timestamptz, NULL::numeric),
  (9, '8739/02/26', 20, 10, 'Escherichia coli', '03802303', 'Tody laboratories', '2026-08-02T00:00:00.000Z'::timestamptz, 20, 'deep freez', 'pure', 1, FALSE, 'E. coli #8739', '2026-08-02T10:33:43.551Z'::timestamptz, 'Burkholderia cenocepacia', 0, NULL::timestamptz, NULL::numeric),
  (10, ' #9027/01/26', 19, 13, 'Pseudomonas aeruginosa', '04001804', 'Tody laboratories', '2027-12-02T00:00:00.000Z'::timestamptz, 20, 'deep freezer', 'pure', 1, FALSE, 'Pseudomonas aeruginosa', '2026-08-02T10:34:54.332Z'::timestamptz, 'Pseudomonas aeruginosa', 0, NULL::timestamptz, NULL::numeric),
  (11, '#6538/01/26', 19, 25, 'Staphylococcus aureus', '04601701', 'Tody laboratories', '2027-08-02T00:00:00.000Z'::timestamptz, 20, 'deep freezer', 'pure', 1, TRUE, 'S. aureus ', '2026-08-02T10:35:42.433Z'::timestamptz, '...', 0, NULL::timestamptz, NULL::numeric),
  (12, 'S.A/02/26', 20, 25, 'Staphylococcus aureus', '04601701', 'Tody laboratories', '2027-08-31T00:00:00.000Z'::timestamptz, 20, 'deep freezer', 'pure', 1, FALSE, 'Staphylococcus aureus', '2026-08-02T16:09:53.348Z'::timestamptz, 'Staphylococcus aureus', 0, NULL::timestamptz, NULL::numeric),
  (13, '8739/03/26', 20, 10, 'Escherichia coli', '03802303', 'Tody laboratories', '2027-08-31T00:00:00.000Z'::timestamptz, 20, 'deep freezer', 'pure', 1, FALSE, 'Escherichia coli', '2026-08-02T16:11:57.500Z'::timestamptz, 'Escherichia coli', 0, NULL::timestamptz, NULL::numeric),
  (14, '8739/04/26', 8, 10, 'Escherichia coli', '03802303', 'Tody laboratories', '2027-08-04T00:00:00.000Z'::timestamptz, 10, '-20', 'pure', 1, FALSE, 'Escherichia coli', '2026-08-03T22:29:07.318Z'::timestamptz, 'Escherichia coli', 4, '2026-08-03T22:30:53.541Z'::timestamptz, 5),
  (15, 'Asp.bra./02/26', 20, 22, 'Aspergillus brasiliensis ', '09402702', 'Tody laboratories', '2027-08-04T00:00:00.000Z'::timestamptz, 20, 'deep freezer', 'pure', 1, TRUE, 'Aspergillus brasiliensis', '2026-08-04T11:56:17.800Z'::timestamptz, 'Aspergillus brasiliensis', 5, '2026-08-04T11:57:10.250Z'::timestamptz, 4),
  (16, 'B.C/02/26', 15, 24, 'Burkholderia cepecia', '07/09/2023', 'Tody laboratories', '2027-08-09T00:00:00.000Z'::timestamptz, 15, 'freezing', 'pure colony', 1, FALSE, 'Burkholderia cepacia', '2026-08-09T06:50:39.244Z'::timestamptz, 'Burkholderia cepacia', 5, '2026-08-09T17:52:12.287Z'::timestamptz, 10)
) AS v(id, code, vialsremaining, materialid, matname, matbatch, manufacturername, expdate, numprepared, storage, physcheck, approvalstatus, isdestroyed, orgsnapshot, preparedat, orgname, preparedbyuserid, approvedat, approvedbyuserid)
JOIN "Organisms" o ON o."ScientificName" = v.orgname
WHERE NOT EXISTS (SELECT 1 FROM "Cryovials" t WHERE t."Id" = v.id)
  AND NOT EXISTS (SELECT 1 FROM "Cryovials" t WHERE t."Code" = v.code)
  -- Safety check: the Material row occupying MaterialId must actually be the
  -- one this Cryovial was prepared from (Materials has no reliable business key).
  AND EXISTS (
    SELECT 1 FROM "Materials" m WHERE m."Id" = v.materialid
      AND m."MaterialName" = v.matname AND m."BatchNumber" = v.matbatch
  );

-- ----------------------------------------------------------------------------
-- 6. ThawEvents (FK: CryovialId via Cryovials.Code; ThawedByUserId via phase2_map_user)
-- ----------------------------------------------------------------------------
INSERT INTO "ThawEvents" ("Id","CryovialId","ThawedAt","ThawedByUserId","Notes")
SELECT v.id, c."Id", v.thawedat, pg_temp.phase2_map_user(v.thawedbyuserid), v.notes
FROM (VALUES
  (1, '8739/01/26', '2026-08-01T17:13:40.150Z'::timestamptz, 1, NULL::text),
  (2, '8739/01/26', '2026-08-01T17:13:42.577Z'::timestamptz, 1, NULL::text),
  (3, '# 14028/01/26', '2026-08-02T06:40:24.529Z'::timestamptz, 1, NULL::text),
  (4, '# 6633/01/26', '2026-08-02T10:29:31.976Z'::timestamptz, 1, NULL::text),
  (5, '# 6633/01/26', '2026-08-02T10:29:34.160Z'::timestamptz, 1, NULL::text),
  (6, '#16404/01/26', '2026-08-02T10:29:35.203Z'::timestamptz, 1, NULL::text),
  (7, '# 14028/01/26', '2026-08-02T10:29:35.700Z'::timestamptz, 1, NULL::text),
  (8, '8739/01/26', '2026-08-02T10:29:36.646Z'::timestamptz, 1, NULL::text),
  (9, '# 6633/01/26', '2026-08-02T10:29:50.275Z'::timestamptz, 1, NULL::text),
  (10, '#6538/01/26', '2026-08-02T10:55:22.815Z'::timestamptz, 1, NULL::text),
  (11, ' #9027/01/26', '2026-08-02T10:55:23.711Z'::timestamptz, 1, NULL::text),
  (12, '#10231/01/26', '2026-08-02T10:55:24.852Z'::timestamptz, 1, NULL::text),
  (13, ' #25416/01/26', '2026-08-02T10:55:25.594Z'::timestamptz, 1, NULL::text),
  (14, '8739/04/26', '2026-08-03T22:30:56.322Z'::timestamptz, 5, NULL::text),
  (15, '8739/04/26', '2026-08-09T06:51:08.543Z'::timestamptz, 5, NULL::text)
) AS v(id, cryocode, thawedat, thawedbyuserid, notes)
JOIN "Cryovials" c ON c."Code" = v.cryocode
WHERE NOT EXISTS (SELECT 1 FROM "ThawEvents" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "ThawEvents" t WHERE t."CryovialId" = c."Id" AND t."ThawedAt" = v.thawedat
  );

-- ----------------------------------------------------------------------------
-- 7. IdentityConfirmationEntries (FK: CryovialId via Cryovials.Code, MediaId via Media.LotNumber, IncubatorEquipmentId via Equipment.Code; no User refs)
-- ----------------------------------------------------------------------------
INSERT INTO "IdentityConfirmationEntries" ("Id","CryovialId","MediaId","IncubatorEquipmentId","IncubationStart","IncubationEnd","ObservationText")
SELECT v.id, c."Id", m."Id", e."Id", v.incstart, v.incend, v.obstext
FROM (VALUES
  (5, '8739/01/26', 'TSA/01/26', 'INC-03', '2026-07-30T00:00:00.000Z'::timestamptz, '2026-08-01T00:00:00.000Z'::timestamptz, 'pure colonies'),
  (6, 'ESCHERICHIACOLI/01/26', 'TSA/01/26', 'INC-03', '2026-07-30T00:00:00.000Z'::timestamptz, '2026-07-31T00:00:00.000Z'::timestamptz, 'Typical colonies, catalase positive'),
  (7, '# 14028/01/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'Pure colonies'),
  (8, '#16404/01/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'Pure'),
  (9, '# 6633/01/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'pure'),
  (10, ' #25416/01/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'pure'),
  (11, '#10231/01/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'pure'),
  (12, '8739/02/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'pure'),
  (13, ' #9027/01/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'pure'),
  (14, '#6538/01/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'pure'),
  (15, 'S.A/02/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'confrom'),
  (16, '8739/03/26', 'TSA/04/26', 'INC-03', '2026-08-02T00:00:00.000Z'::timestamptz, '2026-08-02T00:00:00.000Z'::timestamptz, 'conform'),
  (17, '8739/04/26', 'TSA/04/26', 'INC-F-ML-F-01-003', '2026-08-04T00:00:00.000Z'::timestamptz, '2026-08-04T00:00:00.000Z'::timestamptz, 'Conform'),
  (18, '8739/04/26', 'MAR/05/26', 'INC-F-ML-F-01-003', '2026-08-04T00:00:00.000Z'::timestamptz, '2026-08-04T00:00:00.000Z'::timestamptz, 'Conform'),
  (19, '8739/04/26', 'EMB/04/26', 'INC-F-ML-F-01-003', '2026-08-04T00:00:00.000Z'::timestamptz, '2026-08-04T00:00:00.000Z'::timestamptz, 'Conform'),
  (20, 'Asp.bra./02/26', 'SDA/05/26', 'INC-F-ML-F-01-005', '2026-08-01T00:00:00.000Z'::timestamptz, '2026-08-04T00:00:00.000Z'::timestamptz, 'conform'),
  (21, 'B.C/02/26', 'BUR/02/26', 'INC-F-ML-F-01-003', '2026-08-09T00:00:00.000Z'::timestamptz, '2026-08-15T00:00:00.000Z'::timestamptz, 'conform')
) AS v(id, cryocode, medialot, equipcode, incstart, incend, obstext)
JOIN "Cryovials" c ON c."Code" = v.cryocode
JOIN "Media" m ON m."LotNumber" = v.medialot
JOIN "Equipment" e ON e."Code" = v.equipcode
WHERE NOT EXISTS (SELECT 1 FROM "IdentityConfirmationEntries" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "IdentityConfirmationEntries" t
    WHERE t."CryovialId" = c."Id" AND t."MediaId" = m."Id" AND t."IncubationStart" = v.incstart
  );

-- ----------------------------------------------------------------------------
-- 8. Incubations -- ONLY the 46 rows required by the authorized MediaEvaluationChallenges (TestOrderId IS NULL AND StepName = 'MediaEvaluation'). No unrelated Incubations. FK: MediaId via Media.LotNumber, IncubatorEquipmentId via Equipment.Code. StartedByUserId is NULL for all 46 -- no mapping needed.
-- ----------------------------------------------------------------------------
INSERT INTO "Incubations" ("Id","TestOrderId","StepNumber","StepName","StartedAt","CompletedAt","Outcome","Duration","ExpectedReadingAt","IncubatorEquipmentId","MediaId","Temperature","IncubationEndUtc","IncubationStartUtc","WindowReceivedAtUtc","ParentIncubationId","StageNumber","StartedByUserId")
SELECT v.id, NULL, v.stepnum, v.stepname, v.startedat, v.completedat, v.outcome, v.duration, v.exprdreadingat, e."Id", m."Id", v.temperature, v.incendutc, v.incstartutc, v.windowrecvat, NULL, v.stagenum, NULL
FROM (VALUES
  (12, 0, 'MediaEvaluation', '2026-08-01T19:37:58.781Z'::timestamptz, NULL::timestamptz, NULL::text, '24-48', NULL::timestamptz, 'INC-03', 'TSB/01/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (13, 0, 'MediaEvaluation', '2026-08-01T19:40:51.670Z'::timestamptz, NULL::timestamptz, NULL::text, '24-48', NULL::timestamptz, 'INC-03', 'TSB/02/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (14, 0, 'MediaEvaluation', '2026-08-01T19:56:08.795Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', NULL::timestamptz, 'INC-03', 'TSA/04/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (15, 0, 'MediaEvaluation', '2026-08-01T19:58:29.312Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', NULL::timestamptz, 'INC-03', 'XLD/01/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (16, 0, 'MediaEvaluation', '2026-08-02T06:40:41.884Z'::timestamptz, NULL::timestamptz, NULL::text, '24-48', '2026-08-03T06:40:41.884Z'::timestamptz, 'INC-03', 'TSB/03/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (17, 0, 'MediaEvaluation', '2026-08-02T07:12:01.966Z'::timestamptz, NULL::timestamptz, NULL::text, '24-48', '2026-08-03T07:12:01.966Z'::timestamptz, 'INC-03', 'TSB/02/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (18, 0, 'MediaEvaluation', '2026-08-02T07:12:10.883Z'::timestamptz, NULL::timestamptz, NULL::text, '24-48', '2026-08-03T07:12:10.883Z'::timestamptz, 'INC-03', 'TSB/02/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (19, 0, 'MediaEvaluation', '2026-08-02T10:36:03.746Z'::timestamptz, NULL::timestamptz, NULL::text, '24-48', '2026-08-03T10:36:03.746Z'::timestamptz, 'INC-03', 'TSB/03/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (20, 0, 'MediaEvaluation', '2026-08-02T10:36:07.061Z'::timestamptz, NULL::timestamptz, NULL::text, '24-48', '2026-08-03T10:36:07.061Z'::timestamptz, 'INC-03', 'TSB/03/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (21, 0, 'MediaEvaluation', '2026-08-02T10:54:50.760Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T04:54:50.760Z'::timestamptz, 'INC-03', 'TSI/07/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (22, 0, 'MediaEvaluation', '2026-08-02T10:55:35.262Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T04:55:35.262Z'::timestamptz, 'INC-03', 'TSI/07/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (23, 0, 'MediaEvaluation', '2026-08-02T10:56:07.311Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T10:56:07.311Z'::timestamptz, 'INC-03', 'SDA/05/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (24, 0, 'MediaEvaluation', '2026-08-02T10:56:12.877Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T10:56:12.877Z'::timestamptz, 'INC-03', 'SDA/05/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (33, 0, 'MediaEvaluation', '2026-08-02T16:14:11.284Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:14:11.284Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (34, 0, 'MediaEvaluation', '2026-08-02T16:14:21.348Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:14:21.348Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (35, 0, 'MediaEvaluation', '2026-08-02T16:14:36.427Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:14:36.427Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (36, 0, 'MediaEvaluation', '2026-08-02T16:14:41.908Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:14:41.908Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (37, 0, 'MediaEvaluation', '2026-08-02T16:14:56.081Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:14:56.081Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (38, 0, 'MediaEvaluation', '2026-08-02T16:15:00.605Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:15:00.605Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (39, 0, 'MediaEvaluation', '2026-08-02T16:15:06.738Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:15:06.738Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (40, 0, 'MediaEvaluation', '2026-08-02T16:15:12.281Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:15:12.281Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (41, 0, 'MediaEvaluation', '2026-08-02T16:15:17.219Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:15:17.219Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (42, 0, 'MediaEvaluation', '2026-08-02T16:15:22.004Z'::timestamptz, NULL::timestamptz, NULL::text, '24-50', '2026-08-03T16:15:22.004Z'::timestamptz, 'INC-03', 'R2AAGAR/07/26', '30-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (43, 0, 'MediaEvaluation', '2026-08-02T16:15:41.942Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:15:41.942Z'::timestamptz, 'INC-03', 'MBP/02/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (44, 0, 'MediaEvaluation', '2026-08-02T16:15:46.509Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:15:46.509Z'::timestamptz, 'INC-03', 'MBP/02/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (45, 0, 'MediaEvaluation', '2026-08-02T16:23:25.958Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:23:25.958Z'::timestamptz, 'INC-03', 'MSA/06/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (46, 0, 'MediaEvaluation', '2026-08-02T16:23:32.358Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:23:32.358Z'::timestamptz, 'INC-03', 'MSA/06/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (47, 0, 'MediaEvaluation', '2026-08-02T16:23:47.963Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:23:47.963Z'::timestamptz, 'INC-03', 'MAR/05/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (48, 0, 'MediaEvaluation', '2026-08-02T16:23:51.956Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:23:51.956Z'::timestamptz, 'INC-03', 'MAR/05/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (49, 0, 'MediaEvaluation', '2026-08-02T16:24:00.390Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:24:00.390Z'::timestamptz, 'INC-03', 'EMB/04/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (50, 0, 'MediaEvaluation', '2026-08-02T16:24:04.546Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:24:04.546Z'::timestamptz, 'INC-03', 'EMB/04/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (51, 0, 'MediaEvaluation', '2026-08-02T16:24:21.130Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:24:21.130Z'::timestamptz, 'INC-03', 'CAM/03/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (52, 0, 'MediaEvaluation', '2026-08-02T16:24:26.212Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:24:26.212Z'::timestamptz, 'INC-03', 'CAM/03/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (53, 0, 'MediaEvaluation', '2026-08-02T16:24:33.038Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:24:33.038Z'::timestamptz, 'INC-03', 'BUR/02/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (54, 0, 'MediaEvaluation', '2026-08-02T16:24:39.022Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-03T10:24:39.022Z'::timestamptz, 'INC-03', 'BUR/02/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (75, 0, 'MediaEvaluation', '2026-08-03T11:33:34.637Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-04T05:33:34.637Z'::timestamptz, 'INC-03', 'XLD/01/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (112, 0, 'MediaEvaluation', '2026-08-03T22:31:12.809Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-04T16:31:12.809Z'::timestamptz, 'INC-F-ML-F-01-003', 'MAR/08/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (113, 0, 'MediaEvaluation', '2026-08-03T22:31:26.793Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-04T16:31:26.793Z'::timestamptz, 'INC-F-ML-F-01-003', 'MAR/08/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (114, 0, 'MediaEvaluation', '2026-08-03T22:32:02.954Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-04T16:32:02.954Z'::timestamptz, 'INC-F-ML-F-01-003', 'RVS/01/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (115, 0, 'MediaEvaluation', '2026-08-03T22:32:07.309Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-04T16:32:07.309Z'::timestamptz, 'INC-F-ML-F-01-003', 'RVS/01/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (142, 0, 'MediaEvaluation', '2026-08-05T13:36:25.834Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-06T07:36:25.834Z'::timestamptz, 'INC-F-ML-F-01-003', 'XLD/09/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (143, 0, 'MediaEvaluation', '2026-08-05T13:37:13.019Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-06T07:37:13.019Z'::timestamptz, 'INC-F-ML-F-01-003', 'XLD/09/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (144, 0, 'MediaEvaluation', '2026-08-05T13:39:44.007Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-06T07:39:44.007Z'::timestamptz, 'INC-F-ML-F-01-003', 'RVS/03/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (145, 0, 'MediaEvaluation', '2026-08-05T13:39:54.418Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-06T07:39:54.418Z'::timestamptz, 'INC-F-ML-F-01-003', 'RVS/03/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (163, 0, 'MediaEvaluation', '2026-08-06T09:02:45.690Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-07T03:02:45.690Z'::timestamptz, 'INC-F-ML-F-01-003', 'TSI/10/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1),
  (164, 0, 'MediaEvaluation', '2026-08-06T09:02:59.166Z'::timestamptz, NULL::timestamptz, NULL::text, '18-24', '2026-08-07T03:02:59.166Z'::timestamptz, 'INC-F-ML-F-01-003', 'TSI/10/26', '32.5-35', NULL::timestamptz, NULL::timestamptz, NULL::timestamptz, 1)
) AS v(id, stepnum, stepname, startedat, completedat, outcome, duration, exprdreadingat, equipcode, medialot, temperature, incendutc, incstartutc, windowrecvat, stagenum)
JOIN "Equipment" e ON e."Code" = v.equipcode
JOIN "Media" m ON m."LotNumber" = v.medialot
WHERE NOT EXISTS (SELECT 1 FROM "Incubations" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "Incubations" t
    WHERE t."MediaId" = m."Id" AND t."StepName" = v.stepname AND t."StartedAt" = v.startedat
  );

-- ----------------------------------------------------------------------------
-- 9. MediaEvaluations -- ONLY the 19 rows required by the authorized MediaEvaluationChallenges (excludes unused Id 14, an unfinished evaluation nothing references). FK: MediaId via Media.LotNumber; CompletedByUserId via phase2_map_user
-- ----------------------------------------------------------------------------
INSERT INTO "MediaEvaluations" ("Id","MediaId","EvaluationType","Status","Outcome","AssignedAt","CompletedAt","CompletedByUserId")
SELECT v.id, m."Id", v.evaltype, v.status, v.outcome, v.assignedat, v.completedat, pg_temp.phase2_map_user(v.completedbyuserid)
FROM (VALUES
  (1, 'TSB/01/26', 2, 2, 0, '2026-08-01T19:37:26.317Z'::timestamptz, '2026-08-01T19:38:05.992Z'::timestamptz, 1),
  (2, 'TSB/02/26', 2, 2, 0, '2026-08-01T19:40:22.001Z'::timestamptz, '2026-08-03T11:33:43.091Z'::timestamptz, 4),
  (3, 'TSA/04/26', 0, 2, 0, '2026-08-01T19:42:44.509Z'::timestamptz, '2026-08-01T19:57:30.456Z'::timestamptz, 1),
  (4, 'XLD/01/26', 1, 2, 0, '2026-08-01T19:48:20.572Z'::timestamptz, '2026-08-04T18:49:44.156Z'::timestamptz, 1),
  (5, 'TSB/03/26', 2, 2, 0, '2026-08-01T19:54:05.582Z'::timestamptz, '2026-08-03T11:33:24.126Z'::timestamptz, 4),
  (6, 'BUR/02/26', 1, 2, 0, '2026-08-02T10:37:35.851Z'::timestamptz, '2026-08-03T11:33:01.165Z'::timestamptz, 4),
  (7, 'CAM/03/26', 1, 2, 0, '2026-08-02T10:41:08.355Z'::timestamptz, '2026-08-03T11:32:46.295Z'::timestamptz, 4),
  (8, 'EMB/04/26', 1, 2, 0, '2026-08-02T10:42:17.156Z'::timestamptz, '2026-08-03T11:32:02.936Z'::timestamptz, 4),
  (9, 'MAR/05/26', 1, 2, 0, '2026-08-02T10:43:12.913Z'::timestamptz, '2026-08-03T11:31:14.058Z'::timestamptz, 4),
  (10, 'MSA/06/26', 1, 2, 0, '2026-08-02T10:45:51.535Z'::timestamptz, '2026-08-03T11:30:46.368Z'::timestamptz, 4),
  (11, 'RVS/01/26', 1, 2, 0, '2026-08-02T10:46:40.520Z'::timestamptz, '2026-08-04T18:50:06.574Z'::timestamptz, 1),
  (12, 'MBP/02/26', 1, 2, 0, '2026-08-02T10:47:41.975Z'::timestamptz, '2026-08-03T11:31:33.060Z'::timestamptz, 4),
  (13, 'SDA/05/26', 0, 2, 0, '2026-08-02T10:48:23.681Z'::timestamptz, '2026-08-03T11:30:23.502Z'::timestamptz, 4),
  (15, 'TSI/07/26', 1, 2, 1, '2026-08-02T10:53:00.370Z'::timestamptz, '2026-08-03T11:29:44.887Z'::timestamptz, 4),
  (16, 'R2AAGAR/07/26', 0, 2, 0, '2026-08-02T13:32:24.952Z'::timestamptz, '2026-08-03T16:58:31.977Z'::timestamptz, 7),
  (17, 'MAR/08/26', 1, 2, 1, '2026-08-03T22:30:19.112Z'::timestamptz, '2026-08-04T18:49:24.993Z'::timestamptz, 1),
  (18, 'XLD/09/26', 1, 2, 0, '2026-08-05T13:33:06.846Z'::timestamptz, '2026-08-06T08:42:41.941Z'::timestamptz, 7),
  (19, 'RVS/03/26', 1, 2, 0, '2026-08-05T13:38:18.040Z'::timestamptz, '2026-08-06T08:42:24.096Z'::timestamptz, 7),
  (20, 'TSI/10/26', 1, 2, 0, '2026-08-06T09:02:24.938Z'::timestamptz, '2026-08-07T11:52:10.548Z'::timestamptz, 5)
) AS v(id, medialot, evaltype, status, outcome, assignedat, completedat, completedbyuserid)
JOIN "Media" m ON m."LotNumber" = v.medialot
WHERE NOT EXISTS (SELECT 1 FROM "MediaEvaluations" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "MediaEvaluations" t WHERE t."MediaId" = m."Id" AND t."EvaluationType" = v.evaltype
  );

-- ----------------------------------------------------------------------------
-- 10. MediaEvaluationChallenges -- all 46 authorized rows. FK: MediaEvaluationId via (Media.LotNumber, EvaluationType), CryovialId via Cryovials.Code (nullable), IncubationId via (Media.LotNumber, StepName, StartedAt), OrganismId via Organisms.ScientificName; ReadByUserId via phase2_map_user
-- ----------------------------------------------------------------------------
INSERT INTO "MediaEvaluationChallenges" ("Id","MediaEvaluationId","CryovialId","ChallengeRole","InitialInoculum","IncubationId","OldMediaCount","NewMediaCount","RecoveryPercent","GrowthObserved","ObservedDescription","ExpectedDescription","IsTurbid","Outcome","ReadAt","ReadByUserId","OrganismId")
SELECT v.id, me."Id", cv."Id", v.role, v.initinoc, inc."Id", v.oldcount, v.newcount, v.recovpct, v.growthobs, v.obsdesc, v.expdesc, v.isturbid, v.outcome, v.readat, pg_temp.phase2_map_user(v.readbyuserid), o."Id"
FROM (VALUES
  (1, 'TSB/01/26', 2, 'ESCHERICHIACOLI/01/26', NULL::numeric, '10^2', 'TSB/01/26', 'MediaEvaluation', '2026-08-01T19:37:58.781Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, NULL::text, NULL::text, TRUE, 0, '2026-08-01T19:38:05.991Z'::timestamptz, 1, 'Escherichia coli'),
  (2, 'TSB/02/26', 2, 'ESCHERICHIACOLI/01/26', NULL::numeric, '10^2', 'TSB/02/26', 'MediaEvaluation', '2026-08-01T19:40:51.670Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, NULL::text, NULL::text, TRUE, 0, '2026-08-02T07:11:55.985Z'::timestamptz, 1, 'Escherichia coli'),
  (3, 'TSB/02/26', 2, NULL::text, NULL::numeric, '10^2', 'TSB/02/26', 'MediaEvaluation', '2026-08-02T07:12:01.966Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, NULL::text, NULL::text, TRUE, 0, '2026-08-03T11:33:40.648Z'::timestamptz, 4, '----'),
  (4, 'TSB/02/26', 2, 'S.A/02/26', NULL::numeric, '10^2', 'TSB/02/26', 'MediaEvaluation', '2026-08-02T07:12:10.883Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, NULL::text, NULL::text, TRUE, 0, '2026-08-03T11:33:43.091Z'::timestamptz, 4, 'Staphylococcus aureus'),
  (5, 'TSA/04/26', 0, 'ESCHERICHIACOLI/01/26', NULL::numeric, '10^2', 'TSA/04/26', 'MediaEvaluation', '2026-08-01T19:56:08.795Z'::timestamptz, 90, 95, 105.6, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-01T19:57:30.456Z'::timestamptz, 1, 'Escherichia coli'),
  (6, 'XLD/01/26', 1, 'ESCHERICHIACOLI/01/26', 0, '10^3', 'XLD/01/26', 'MediaEvaluation', '2026-08-01T19:58:29.312Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:33:29.977Z'::timestamptz, 4, 'Escherichia coli'),
  (7, 'XLD/01/26', 1, NULL::text, 1, '10^2', 'XLD/01/26', 'MediaEvaluation', '2026-08-03T11:33:34.637Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, 'Black centered colonies', 'Black centered colonies', NULL::boolean, 0, '2026-08-04T18:49:44.156Z'::timestamptz, 1, '----'),
  (8, 'TSB/03/26', 2, 'ESCHERICHIACOLI/01/26', NULL::numeric, '10^2', 'TSB/03/26', 'MediaEvaluation', '2026-08-02T06:40:41.884Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, NULL::text, NULL::text, TRUE, 0, '2026-08-03T11:33:12.344Z'::timestamptz, 4, 'Escherichia coli'),
  (9, 'TSB/03/26', 2, NULL::text, NULL::numeric, '10^2', 'TSB/03/26', 'MediaEvaluation', '2026-08-02T10:36:03.746Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, NULL::text, NULL::text, TRUE, 0, '2026-08-03T11:33:14.720Z'::timestamptz, 4, '----'),
  (10, 'TSB/03/26', 2, 'S.A/02/26', NULL::numeric, '10^2', 'TSB/03/26', 'MediaEvaluation', '2026-08-02T10:36:07.061Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, NULL::text, NULL::text, TRUE, 0, '2026-08-03T11:33:24.126Z'::timestamptz, 4, 'Staphylococcus aureus'),
  (11, 'BUR/02/26', 1, ' #25416/01/26', 1, '10^2', 'BUR/02/26', 'MediaEvaluation', '2026-08-02T16:24:33.038Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, 'Greenish–brown colonies with yellow halo', 'Greenish–brown colonies with yellow halo', NULL::boolean, 0, '2026-08-03T11:32:58.350Z'::timestamptz, 4, 'Burkholderia cepacia'),
  (12, 'BUR/02/26', 1, ' #9027/01/26', 0, '10^3', 'BUR/02/26', 'MediaEvaluation', '2026-08-02T16:24:39.022Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:33:01.165Z'::timestamptz, 4, 'Pseudomonas aeruginosa'),
  (13, 'CAM/03/26', 1, '8739/03/26', 0, '10^3', 'CAM/03/26', 'MediaEvaluation', '2026-08-02T16:24:21.130Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:32:36.918Z'::timestamptz, 4, 'Escherichia coli'),
  (14, 'CAM/03/26', 1, ' #9027/01/26', 1, '10^2', 'CAM/03/26', 'MediaEvaluation', '2026-08-02T16:24:26.212Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, ' yellow-green or yellow-brown fluorescent pyoverdin', 'yellow-green or yellow-brown fluorescent pyoverdin', NULL::boolean, 0, '2026-08-03T11:32:46.295Z'::timestamptz, 4, 'Pseudomonas aeruginosa'),
  (15, 'EMB/04/26', 1, '8739/03/26', 1, '10^2', 'EMB/04/26', 'MediaEvaluation', '2026-08-02T16:24:00.390Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, 'metallic sheen with a dark center', 'metallic sheen with a dark center', NULL::boolean, 0, '2026-08-03T11:31:58.206Z'::timestamptz, 4, 'Escherichia coli'),
  (16, 'EMB/04/26', 1, 'S.A/02/26', 0, '10^3', 'EMB/04/26', 'MediaEvaluation', '2026-08-02T16:24:04.546Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:32:02.936Z'::timestamptz, 4, 'Staphylococcus aureus'),
  (17, 'MAR/05/26', 1, '8739/03/26', 1, '10^2', 'MAR/05/26', 'MediaEvaluation', '2026-08-02T16:23:47.963Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, ' pink to dark pink, non-mucoid, surrounded by darker pink halo of precipitated bile salts', 'pink to dark pink, non-mucoid, surrounded by darker pink halo of precipitated bile salts', NULL::boolean, 0, '2026-08-03T11:31:08.019Z'::timestamptz, 4, 'Escherichia coli'),
  (18, 'MAR/05/26', 1, 'S.A/02/26', 0, '10^3', 'MAR/05/26', 'MediaEvaluation', '2026-08-02T16:23:51.956Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:31:14.058Z'::timestamptz, 4, 'Staphylococcus aureus'),
  (19, 'MSA/06/26', 1, '8739/03/26', 0, '10^3', 'MSA/06/26', 'MediaEvaluation', '2026-08-02T16:23:25.958Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:30:39.636Z'::timestamptz, 4, 'Escherichia coli'),
  (20, 'MSA/06/26', 1, 'S.A/02/26', 1, '10^2', 'MSA/06/26', 'MediaEvaluation', '2026-08-02T16:23:32.358Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, '', 'yellow colonies', NULL::boolean, 0, '2026-08-03T11:30:46.368Z'::timestamptz, 4, 'Staphylococcus aureus'),
  (21, 'RVS/01/26', 1, NULL::text, 1, '10^2', 'RVS/01/26', 'MediaEvaluation', '2026-08-03T22:32:07.309Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, 'Turbid', 'Turbid', NULL::boolean, 0, '2026-08-04T18:50:03.154Z'::timestamptz, 1, '----'),
  (22, 'RVS/01/26', 1, 'S.A/02/26', 0, '10^3', 'RVS/01/26', 'MediaEvaluation', '2026-08-03T22:32:02.954Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-04T18:50:06.574Z'::timestamptz, 1, 'Staphylococcus aureus'),
  (23, 'MBP/02/26', 1, '8739/03/26', 1, '10^2', 'MBP/02/26', 'MediaEvaluation', '2026-08-02T16:15:41.942Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, 'Growth ,Acid production (yellow) ,Gas production', 'Growth ,Acid production (yellow) ,Gas production', NULL::boolean, 0, '2026-08-03T11:31:29.123Z'::timestamptz, 4, 'Escherichia coli'),
  (24, 'MBP/02/26', 1, 'S.A/02/26', 0, '10^3', 'MBP/02/26', 'MediaEvaluation', '2026-08-02T16:15:46.509Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:31:33.060Z'::timestamptz, 4, 'Staphylococcus aureus'),
  (25, 'SDA/05/26', 0, '#16404/01/26', NULL::numeric, '10^2', 'SDA/05/26', 'MediaEvaluation', '2026-08-02T10:56:12.877Z'::timestamptz, 90, 98, 108.9, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:30:11.996Z'::timestamptz, 4, 'Aspergillus brasiliensis'),
  (26, 'SDA/05/26', 0, '#10231/01/26', NULL::numeric, '10^2', 'SDA/05/26', 'MediaEvaluation', '2026-08-02T10:56:07.311Z'::timestamptz, 95, 90, 94.7, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T11:30:23.502Z'::timestamptz, 4, 'Candida albicans'),
  (27, 'TSI/07/26', 1, NULL::text, 1, '10^2', 'TSI/07/26', 'MediaEvaluation', '2026-08-02T10:54:50.760Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, '', 'Slant red and butt yellow with black colour ', NULL::boolean, 0, '2026-08-03T11:29:37.269Z'::timestamptz, 4, '----'),
  (28, 'TSI/07/26', 1, 'S.A/02/26', 0, '10^3', 'TSI/07/26', 'MediaEvaluation', '2026-08-02T10:55:35.262Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, TRUE, NULL::text, NULL::text, NULL::boolean, 1, '2026-08-03T11:29:44.886Z'::timestamptz, 4, 'Staphylococcus aureus'),
  (29, 'R2AAGAR/07/26', 0, '8739/03/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:14:11.284Z'::timestamptz, 90, 100, 111.1, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:57:18.825Z'::timestamptz, 7, 'Escherichia coli'),
  (30, 'R2AAGAR/07/26', 0, 'S.A/02/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:14:21.348Z'::timestamptz, 80, 88, 110.0, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:57:29.126Z'::timestamptz, 7, 'Staphylococcus aureus'),
  (31, 'R2AAGAR/07/26', 0, '#16404/01/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:14:36.427Z'::timestamptz, 91, 93, 102.2, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:57:34.474Z'::timestamptz, 7, 'Aspergillus brasiliensis'),
  (32, 'R2AAGAR/07/26', 0, ' #9027/01/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:14:41.908Z'::timestamptz, 70, 80, 114.3, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:57:41.295Z'::timestamptz, 7, 'Pseudomonas aeruginosa'),
  (33, 'R2AAGAR/07/26', 0, '#10231/01/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:14:56.081Z'::timestamptz, 102, 90, 88.2, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:57:48.664Z'::timestamptz, 7, 'Candida albicans'),
  (34, 'R2AAGAR/07/26', 0, '#16404/01/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:15:00.605Z'::timestamptz, 64, 70, 109.4, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:57:55.589Z'::timestamptz, 7, 'Aspergillus brasiliensis'),
  (35, 'R2AAGAR/07/26', 0, '# 6633/01/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:15:06.738Z'::timestamptz, 100, 89, 89.0, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:58:23.343Z'::timestamptz, 7, 'Bacillus subtilis'),
  (36, 'R2AAGAR/07/26', 0, '#10231/01/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:15:12.281Z'::timestamptz, 100, 90, 90.0, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:58:26.242Z'::timestamptz, 7, 'Candida albicans'),
  (37, 'R2AAGAR/07/26', 0, ' #9027/01/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:15:17.219Z'::timestamptz, 100, 102, 102.0, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:58:29.161Z'::timestamptz, 7, 'Pseudomonas aeruginosa'),
  (38, 'R2AAGAR/07/26', 0, 'S.A/02/26', NULL::numeric, '10^2', 'R2AAGAR/07/26', 'MediaEvaluation', '2026-08-02T16:15:22.004Z'::timestamptz, 100, 103, 103.0, NULL::boolean, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-03T16:58:31.976Z'::timestamptz, 7, 'Staphylococcus aureus'),
  (39, 'MAR/08/26', 1, 'S.A/02/26', 0, '10^3', 'MAR/08/26', 'MediaEvaluation', '2026-08-03T22:31:12.809Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, TRUE, NULL::text, NULL::text, NULL::boolean, 1, '2026-08-04T18:49:08.902Z'::timestamptz, 1, 'Staphylococcus aureus'),
  (40, 'MAR/08/26', 1, '8739/04/26', 1, '10^2', 'MAR/08/26', 'MediaEvaluation', '2026-08-03T22:31:26.793Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, ' pink to dark pink, non-mucoid, surrounded by darker pink halo of precipitated bile salts', 'pink to dark pink, non-mucoid, surrounded by darker pink halo of precipitated bile salts', NULL::boolean, 0, '2026-08-04T18:49:24.992Z'::timestamptz, 1, 'Escherichia coli'),
  (41, 'XLD/09/26', 1, '8739/03/26', 0, '10^3', 'XLD/09/26', 'MediaEvaluation', '2026-08-05T13:36:25.834Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-06T08:42:33.093Z'::timestamptz, 7, 'Escherichia coli'),
  (42, 'XLD/09/26', 1, '# 14028/01/26', 1, '10^2', 'XLD/09/26', 'MediaEvaluation', '2026-08-05T13:37:13.019Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, 'Black centered colonies', 'Black centered colonies', NULL::boolean, 0, '2026-08-06T08:42:41.941Z'::timestamptz, 7, 'Salmonella  typhimurium'),
  (43, 'RVS/03/26', 1, 'S.A/02/26', 0, '10^3', 'RVS/03/26', 'MediaEvaluation', '2026-08-05T13:39:44.007Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-06T08:42:11.067Z'::timestamptz, 7, 'Staphylococcus aureus'),
  (44, 'RVS/03/26', 1, '# 14028/01/26', 1, '10^2', 'RVS/03/26', 'MediaEvaluation', '2026-08-05T13:39:54.418Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, 'turbid', 'turbid', NULL::boolean, 0, '2026-08-06T08:42:24.096Z'::timestamptz, 7, 'Salmonella  typhimurium'),
  (45, 'TSI/10/26', 1, 'S.A/02/26', 0, '10^3', 'TSI/10/26', 'MediaEvaluation', '2026-08-06T09:02:45.690Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, FALSE, NULL::text, NULL::text, NULL::boolean, 0, '2026-08-07T11:51:57.337Z'::timestamptz, 5, 'Staphylococcus aureus'),
  (46, 'TSI/10/26', 1, '# 14028/01/26', 1, '10^2', 'TSI/10/26', 'MediaEvaluation', '2026-08-06T09:02:59.166Z'::timestamptz, NULL::numeric, NULL::numeric, NULL::numeric, NULL::boolean, 'Slant red and butt yellow with black colour', 'Slant red and butt yellow with black colour ', NULL::boolean, 0, '2026-08-07T11:52:10.547Z'::timestamptz, 5, 'Salmonella  typhimurium')
) AS v(id, me_medialot, me_evaltype, cryocode, role, initinoc, inc_medialot, inc_stepname, inc_startedat, oldcount, newcount, recovpct, growthobs, obsdesc, expdesc, isturbid, outcome, readat, readbyuserid, orgname)
JOIN "Media" me_m ON me_m."LotNumber" = v.me_medialot
JOIN "MediaEvaluations" me ON me."MediaId" = me_m."Id" AND me."EvaluationType" = v.me_evaltype
LEFT JOIN "Cryovials" cv ON cv."Code" = v.cryocode
JOIN "Media" inc_m ON inc_m."LotNumber" = v.inc_medialot
JOIN "Incubations" inc ON inc."MediaId" = inc_m."Id" AND inc."StepName" = v.inc_stepname AND inc."StartedAt" = v.inc_startedat
JOIN "Organisms" o ON o."ScientificName" = v.orgname
WHERE NOT EXISTS (SELECT 1 FROM "MediaEvaluationChallenges" t WHERE t."Id" = v.id)
  AND NOT EXISTS (
    SELECT 1 FROM "MediaEvaluationChallenges" t
    WHERE t."MediaEvaluationId" = me."Id" AND t."IncubationId" = inc."Id"
  );

-- ----------------------------------------------------------------------------
-- Sequence resets
-- ----------------------------------------------------------------------------
SELECT setval(pg_get_serial_sequence('"MediaChallengeSpecs"', 'Id'), COALESCE((SELECT MAX("Id") FROM "MediaChallengeSpecs"), 1), true);
SELECT setval(pg_get_serial_sequence('"Machines"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Machines"), 1), true);
SELECT setval(pg_get_serial_sequence('"MachineParts"', 'Id'), COALESCE((SELECT MAX("Id") FROM "MachineParts"), 1), true);
SELECT setval(pg_get_serial_sequence('"MachinePartConfigurations"', 'Id'), COALESCE((SELECT MAX("Id") FROM "MachinePartConfigurations"), 1), true);
SELECT setval(pg_get_serial_sequence('"Cryovials"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Cryovials"), 1), true);
SELECT setval(pg_get_serial_sequence('"ThawEvents"', 'Id'), COALESCE((SELECT MAX("Id") FROM "ThawEvents"), 1), true);
SELECT setval(pg_get_serial_sequence('"IdentityConfirmationEntries"', 'Id'), COALESCE((SELECT MAX("Id") FROM "IdentityConfirmationEntries"), 1), true);
SELECT setval(pg_get_serial_sequence('"Incubations"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Incubations"), 1), true);
SELECT setval(pg_get_serial_sequence('"MediaEvaluations"', 'Id'), COALESCE((SELECT MAX("Id") FROM "MediaEvaluations"), 1), true);
SELECT setval(pg_get_serial_sequence('"MediaEvaluationChallenges"', 'Id'), COALESCE((SELECT MAX("Id") FROM "MediaEvaluationChallenges"), 1), true);
SELECT setval(pg_get_serial_sequence('"Users"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Users"), 1), true);

COMMIT;

-- ============================================================================
-- POST-MIGRATION VERIFICATION (run manually after COMMIT). Compares actual row
-- counts to expected. A lower count than expected means some rows were
-- skipped by a guard -- investigate before assuming completeness.
-- ============================================================================
SELECT
  (SELECT count(*) FROM "MediaChallengeSpecs") AS "MediaChallengeSpecs_count",
  (SELECT count(*) FROM "Machines") AS "Machines_count",
  (SELECT count(*) FROM "MachineParts") AS "MachineParts_count",
  (SELECT count(*) FROM "MachinePartConfigurations") AS "MachinePartConfigurations_count",
  (SELECT count(*) FROM "Cryovials") AS "Cryovials_count",
  (SELECT count(*) FROM "ThawEvents") AS "ThawEvents_count",
  (SELECT count(*) FROM "IdentityConfirmationEntries") AS "IdentityConfirmationEntries_count",
  (SELECT count(*) FROM "Incubations") AS "Incubations_count",
  (SELECT count(*) FROM "MediaEvaluations") AS "MediaEvaluations_count",
  (SELECT count(*) FROM "MediaEvaluationChallenges") AS "MediaEvaluationChallenges_count",
  (SELECT count(*) FROM "Users" WHERE "Username" IN ('MMAAN','Amal Hamdy')) AS "historical_users_created"
;
-- Expected: MediaChallengeSpecs=33, Machines=7, MachineParts=25, MachinePartConfigurations=17, Cryovials=15, ThawEvents=15, IdentityConfirmationEntries=17, Incubations=46, MediaEvaluations=19, MediaEvaluationChallenges=46, historical_users_created=2

-- User attribution verification (run manually after COMMIT):
-- SELECT u."Username", count(*) FROM "Cryovials" c JOIN "Users" u ON u."Id" = c."PreparedByUserId" GROUP BY 1 ORDER BY 1;
-- SELECT u."Username", count(*) FROM "Cryovials" c JOIN "Users" u ON u."Id" = c."ApprovedByUserId" GROUP BY 1 ORDER BY 1;
-- SELECT u."Username", count(*) FROM "ThawEvents" te JOIN "Users" u ON u."Id" = te."ThawedByUserId" GROUP BY 1 ORDER BY 1;
-- SELECT u."Username", count(*) FROM "MediaEvaluations" me JOIN "Users" u ON u."Id" = me."CompletedByUserId" GROUP BY 1 ORDER BY 1;
-- SELECT u."Username", count(*) FROM "MediaEvaluationChallenges" mec JOIN "Users" u ON u."Id" = mec."ReadByUserId" GROUP BY 1 ORDER BY 1;