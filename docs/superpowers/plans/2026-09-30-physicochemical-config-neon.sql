-- =============================================================================
-- Physicochemical Laboratory (section 'FP') CONFIGURATION seed for PRODUCTION (Neon).
-- Generated from the local development database LIMSV2 (2026-09-30). Run in the Neon SQL editor.
--
-- PREREQUISITE: PR #64 (feat/hplc-chain, which brings the physicochemical schema) must be merged and
-- deployed first, so the migrations have run on the production database. A pre-flight check below
-- aborts with the list of missing columns if the schema is behind.
--
-- COPIES (everything under Physicochemical Configuration, FK-resolved by natural key, never by local Id):
--   Equipment (instruments used by HPLC/ICP/GC), Reagents & Standards (MaterialMasterEntries) and their stock lots
--   (Materials, including made-up lots), Chromatography Columns (+ UspDesignation) and column<->instrument links,
--   Solutions (masters + components), HPLC Methods (+ mobile phases, analytes with SST criteria, gradient steps),
--   Physicochemical Test Master (TestDefinitions + TestAnalytes + TestDefinitionStageReplicates),
--   Items that carry physicochemical tests, their SampleTests and Specifications.
--   Equation Types are code (TestDefinitionMasterDataService), not database rows: nothing to copy.
--
-- EXCLUDES: transactional data (samples, test orders, HPLC runs, SST, calibration runs, preparations,
--   standardizations, evidence, audit trail, signatures), users/roles, the equipment inventory register
--   (EquipmentInventories, seeded separately), and every smoke/E2E artefact (any row whose code or name contains
--   SMOKE or E2E: equipment AAS-SMOKE-01, test 'Minerals AAS (SMOKE TEST)' and its analytes, item SMOKE01 and its
--   tests/specs). Creator/modifier user columns are set to the 'admin' user; original CreatedAt/LastModifiedAt kept.
--
-- IDEMPOTENT: every INSERT is guarded by WHERE NOT EXISTS on its natural key; a second run inserts nothing.
-- Existing production rows are never updated.
-- =============================================================================
BEGIN;

CREATE TEMP TABLE _ctx ON COMMIT DROP AS
SELECT (SELECT "Id" FROM "DocumentSections" WHERE "Code" = 'FP') AS sec,
       (SELECT "Id" FROM "Users" WHERE "Username" = 'admin' AND "IsActive") AS uid;

-- sanity: refuse to run without the FP section and the admin user
DO $$ BEGIN
  IF (SELECT sec FROM _ctx) IS NULL OR (SELECT uid FROM _ctx) IS NULL THEN
    RAISE EXCEPTION 'FP section or admin user not found';
  END IF;
END $$;

-- schema pre-flight: every table/column this script writes must exist (i.e. PR #64 migrations are applied)
DO $$
DECLARE missing text;
BEGIN
  SELECT string_agg(t.tbl || '.' || t.col, ', ') INTO missing
  FROM (VALUES
   ('Equipment','Name'),
   ('Equipment','Code'),
   ('Equipment','Type'),
   ('Equipment','Location'),
   ('Equipment','SetPointTemperature'),
   ('Equipment','CalibrationDueDate'),
   ('Equipment','CdsSoftware'),
   ('Equipment','ConnectionSettings'),
   ('Equipment','SectionId'),
   ('Equipment','Vendor'),
   ('MaterialMasterEntries','SectionId'),
   ('MaterialMasterEntries','Code'),
   ('MaterialMasterEntries','Name'),
   ('MaterialMasterEntries','Category'),
   ('MaterialMasterEntries','Grade'),
   ('MaterialMasterEntries','Source'),
   ('MaterialMasterEntries','BaseUnit'),
   ('MaterialMasterEntries','IsActive'),
   ('MaterialMasterEntries','WorkingConcentration'),
   ('MaterialMasterEntries','Solvent'),
   ('MaterialMasterEntries','TransitionRangeFrom'),
   ('MaterialMasterEntries','TransitionRangeTo'),
   ('MaterialMasterEntries','ColourChange'),
   ('MaterialMasterEntries','IndicatorUse'),
   ('MaterialMasterEntries','CreatedByUserId'),
   ('MaterialMasterEntries','CreatedAt'),
   ('MaterialMasterEntries','LastModifiedByUserId'),
   ('MaterialMasterEntries','LastModifiedAt'),
   ('Materials','MaterialType'),
   ('Materials','MaterialName'),
   ('Materials','ManufacturerName'),
   ('Materials','BatchNumber'),
   ('Materials','ReceivingDate'),
   ('Materials','ExpiryDate'),
   ('Materials','Code'),
   ('Materials','Location'),
   ('Materials','QuantityReceived'),
   ('Materials','QuantityRemaining'),
   ('Materials','Unit'),
   ('Materials','MinimumStockLevel'),
   ('Materials','CreatedByUserId'),
   ('Materials','CreatedAt'),
   ('Materials','LastModifiedByUserId'),
   ('Materials','LastModifiedAt'),
   ('Materials','AtccNumber'),
   ('Materials','SectionId'),
   ('Materials','Purity'),
   ('Materials','CustomType'),
   ('Materials','MaterialMasterEntryId'),
   ('Materials','MoisturePercent'),
   ('ChromatographyColumns','Code'),
   ('ChromatographyColumns','Name'),
   ('ChromatographyColumns','SerialNumber'),
   ('ChromatographyColumns','SectionId'),
   ('ChromatographyColumns','IsActive'),
   ('ChromatographyColumns','CreatedByUserId'),
   ('ChromatographyColumns','CreatedAt'),
   ('ChromatographyColumns','LastModifiedByUserId'),
   ('ChromatographyColumns','LastModifiedAt'),
   ('ChromatographyColumns','UspDesignation'),
   ('ChromatographyColumnEquipment','CompatibleColumnsId'),
   ('ChromatographyColumnEquipment','CompatibleEquipmentId'),
   ('SolutionMasters','SectionId'),
   ('SolutionMasters','Name'),
   ('SolutionMasters','Type'),
   ('SolutionMasters','ShelfLifeValue'),
   ('SolutionMasters','ShelfLifeUnit'),
   ('SolutionMasters','StorageCondition'),
   ('SolutionMasters','FinalVolumeMl'),
   ('SolutionMasters','PhTarget'),
   ('SolutionMasters','PhTolerance'),
   ('SolutionMasters','PhAdjustingEntryId'),
   ('SolutionMasters','Instructions'),
   ('SolutionMasters','IsActive'),
   ('SolutionMasters','NominalStrength'),
   ('SolutionMasters','StrengthUnit'),
   ('SolutionMasters','StandardizationMode'),
   ('SolutionMasters','StandardEntryId'),
   ('SolutionMasters','EquivalenceMgPerMl'),
   ('SolutionMasters','ReferenceSolutionId'),
   ('SolutionMasters','BlankRequired'),
   ('SolutionMasters','ReplicateCount'),
   ('SolutionMasters','FactorMin'),
   ('SolutionMasters','FactorMax'),
   ('SolutionMasters','MaxRsdPercent'),
   ('SolutionMasters','ValidityDays'),
   ('SolutionMasters','CreatedByUserId'),
   ('SolutionMasters','CreatedAt'),
   ('SolutionMasters','LastModifiedByUserId'),
   ('SolutionMasters','LastModifiedAt'),
   ('SolutionComponents','SolutionMasterId'),
   ('SolutionComponents','Order'),
   ('SolutionComponents','MaterialMasterEntryId'),
   ('SolutionComponents','Quantity'),
   ('SolutionComponents','Unit'),
   ('HplcMethods','SectionId'),
   ('HplcMethods','Name'),
   ('HplcMethods','Abbreviation'),
   ('HplcMethods','EffectiveDate'),
   ('HplcMethods','IsActive'),
   ('HplcMethods','ColumnDesignation'),
   ('HplcMethods','ColumnLengthMm'),
   ('HplcMethods','ColumnInternalDiameterMm'),
   ('HplcMethods','ParticleSizeUm'),
   ('HplcMethods','ColumnBrand'),
   ('HplcMethods','ColumnPartNumber'),
   ('HplcMethods','ColumnTemperatureC'),
   ('HplcMethods','ElutionMode'),
   ('HplcMethods','EquilibrationMin'),
   ('HplcMethods','FlowRateMlPerMin'),
   ('HplcMethods','DetectorType'),
   ('HplcMethods','InjectionVolumeUl'),
   ('HplcMethods','RunTimeMin'),
   ('HplcMethods','DiluentSolutionId'),
   ('HplcMethods','CreatedByUserId'),
   ('HplcMethods','CreatedAt'),
   ('HplcMethods','LastModifiedByUserId'),
   ('HplcMethods','LastModifiedAt'),
   ('HplcMethodMobilePhases','HplcMethodId'),
   ('HplcMethodMobilePhases','Channel'),
   ('HplcMethodMobilePhases','SolutionMasterId'),
   ('HplcMethodMobilePhases','RatioPercent'),
   ('HplcMethodAnalytes','HplcMethodId'),
   ('HplcMethodAnalytes','DisplayOrder'),
   ('HplcMethodAnalytes','Name'),
   ('HplcMethodAnalytes','WavelengthNm'),
   ('HplcMethodAnalytes','StandardEntryId'),
   ('HplcMethodAnalytes','TheoreticalWeightStdMg'),
   ('HplcMethodAnalytes','TheoreticalWeightTestMg'),
   ('HplcMethodAnalytes','StandardInjections'),
   ('HplcMethodAnalytes','SstMaxRsdPercent'),
   ('HplcMethodAnalytes','SstMinResolution'),
   ('HplcMethodAnalytes','SstMaxTailingFactor'),
   ('HplcMethodAnalytes','SstMinTheoreticalPlates'),
   ('HplcMethodAnalytes','SstMinRetentionFactor'),
   ('HplcMethodAnalytes','SstMinSignalToNoise'),
   ('HplcMethodAnalytes','SstMinPeakToValley'),
   ('TestDefinitions','Code'),
   ('TestDefinitions','DisplayName'),
   ('TestDefinitions','IsActive'),
   ('TestDefinitions','WorkflowType'),
   ('TestDefinitions','SectionId'),
   ('TestDefinitions','EquationType'),
   ('TestDefinitions','MethodAbbreviation'),
   ('TestDefinitions','RequiresSystemSuitability'),
   ('TestDefinitions','SstMaxRsdPercent'),
   ('TestDefinitions','SstMaxTailingFactor'),
   ('TestDefinitions','SstMinResolution'),
   ('TestDefinitions','SstMinTheoreticalPlates'),
   ('TestDefinitions','CalBlankMax'),
   ('TestDefinitions','CalCheckRecoveryHighPercent'),
   ('TestDefinitions','CalCheckRecoveryLowPercent'),
   ('TestDefinitions','CalCorrelationType'),
   ('TestDefinitions','CalIsRecoveryHighPercent'),
   ('TestDefinitions','CalIsRecoveryLowPercent'),
   ('TestDefinitions','CalMaxRunAgeHours'),
   ('TestDefinitions','CalMinCorrelation'),
   ('TestDefinitions','CalMinStandards'),
   ('TestDefinitions','CalRequireBlank'),
   ('TestDefinitions','CalRequireCcv'),
   ('TestDefinitions','CalRequireIcv'),
   ('TestDefinitions','CalRequireInternalStandard'),
   ('TestDefinitions','CalibrationEntryMode'),
   ('TestDefinitions','ReportedConcentrationBasis'),
   ('TestDefinitions','EvaluationBasis'),
   ('TestDefinitions','ReplicateCount'),
   ('TestDefinitions','ConditionFields'),
   ('TestDefinitions','UsesTare'),
   ('TestDefinitions','DissolutionS1Offset'),
   ('TestDefinitions','DissolutionS2MinOffset'),
   ('TestDefinitions','DissolutionS3MaxBelowS2Min'),
   ('TestDefinitions','DissolutionS3MinOffset'),
   ('TestDefinitions','DisintegrationMaxStage1Failures'),
   ('TestDefinitions','DisintegrationMinPassTotal'),
   ('TestDefinitions','DisintegrationStage1Units'),
   ('TestDefinitions','DisintegrationStage2Units'),
   ('TestDefinitions','WvCapsuleInnerPercent'),
   ('TestDefinitions','WvCapsuleOuterPercent'),
   ('TestDefinitions','WvCapsuleS1MaxForRetest'),
   ('TestDefinitions','WvCapsuleS1MaxOutside'),
   ('TestDefinitions','WvCapsuleS2ExtraUnits'),
   ('TestDefinitions','WvCapsuleS2MaxOutside'),
   ('TestDefinitions','WvTabletBand1MaxMg'),
   ('TestDefinitions','WvTabletBand1Percent'),
   ('TestDefinitions','WvTabletBand2MaxMg'),
   ('TestDefinitions','WvTabletBand2Percent'),
   ('TestDefinitions','WvTabletBand3Percent'),
   ('TestDefinitions','WvTabletMaxOutside'),
   ('TestDefinitions','WvUnitCount'),
   ('TestDefinitions','HplcMaxPreparationRsdPercent'),
   ('TestDefinitions','ResponseMode'),
   ('TestDefinitions','CalInstrumentType'),
   ('TestDefinitions','CalStandardLevelsMgPerL'),
   ('TestDefinitions','HplcMethodId'),
   ('TestAnalytes','TestDefinitionId'),
   ('TestAnalytes','Element'),
   ('TestAnalytes','WavelengthNm'),
   ('TestAnalytes','View'),
   ('TestAnalytes','LoqMgPerL'),
   ('TestAnalytes','DisplayOrder'),
   ('TestAnalytes','IsActive'),
   ('TestAnalytes','SstMaxRsdPercent'),
   ('TestAnalytes','SstMaxTailingFactor'),
   ('TestAnalytes','SstMinResolution'),
   ('TestAnalytes','SstMinTheoreticalPlates'),
   ('TestDefinitionStageReplicates','TestDefinitionId'),
   ('TestDefinitionStageReplicates','Role'),
   ('TestDefinitionStageReplicates','SampleReplicates'),
   ('Items','Name'),
   ('Items','Code'),
   ('Items','Category'),
   ('Items','SopNumber'),
   ('Items','IsActive'),
   ('SampleTests','ItemId'),
   ('SampleTests','TestCode'),
   ('SampleTests','DisplayName'),
   ('Specifications','ItemId'),
   ('Specifications','TestCode'),
   ('Specifications','AlertLimit'),
   ('Specifications','ActionLimit'),
   ('Specifications','SpecLimit'),
   ('Specifications','Unit'),
   ('Specifications','DilutionFactor'),
   ('Specifications','DisplayOrder'),
   ('Specifications','ExpectedResultText'),
   ('Specifications','ExpectedState'),
   ('Specifications','LimitType'),
   ('Specifications','LowerInclusive'),
   ('Specifications','LowerLimit'),
   ('Specifications','ParameterName'),
   ('Specifications','ReferenceStandard'),
   ('Specifications','SampleQuantity'),
   ('Specifications','SampleQuantityUnit'),
   ('Specifications','Target'),
   ('Specifications','Tolerance'),
   ('Specifications','ToleranceMode'),
   ('Specifications','UpperInclusive'),
   ('Specifications','UpperLimit'),
   ('Specifications','ConversionFactor'),
   ('Specifications','LabelClaim'),
   ('Specifications','LabelClaimUnit'),
   ('Specifications','ResultBasis'),
   ('Specifications','SampleMatrix'),
   ('Specifications','TestAnalyteId'),
   ('Specifications','DosageForm'),
   ('Specifications','HplcMethodAnalyteId')
  ) AS t(tbl, col)
  WHERE NOT EXISTS (SELECT 1 FROM information_schema.columns i
                    WHERE i.table_schema = 'public' AND i.table_name = t.tbl AND i.column_name = t.col);
  IF missing IS NOT NULL THEN
    RAISE EXCEPTION 'Schema is behind this script (deploy PR #64 first). Missing: %', missing;
  END IF;
END $$;


------------------------------------------------------------------------------
-- 0. Instruments (Equipment table) used by HPLC/ICP/GC workflows and column compatibility
-- Natural key: Code. Smoke/E2E rows (AAS-SMOKE-01) excluded.
------------------------------------------------------------------------------
INSERT INTO "Equipment"
  ("Name","Code","Type","Location","SetPointTemperature","CalibrationDueDate","CdsSoftware","ConnectionSettings","SectionId","Vendor")
SELECT v."Name",
       v."Code",
       v."Type",
       v."Location",
       v."SetPointTemperature",
       v."CalibrationDueDate",
       v."CdsSoftware",
       v."ConnectionSettings",
       c.sec,
       v."Vendor"
FROM (VALUES
 ('Shimadzu GC'::text,'GCS-F-IL-F-11-068'::text,'22'::integer,'Instruments Lab'::text,NULL::numeric,NULL::timestamp with time zone,NULL::integer,NULL::jsonb,'Shimadzu'::character varying(100)),
 ('Agilent HPLC','HPC-F-IL-F-08-028','6','Finished Product Instrument lab',NULL,'2027-10-01 03:00:00+03','1',NULL,'Agilent'),
 ('Perkinelmer ICP','ICP-F-IL-F-11-069','9','Finished Product Instruments Lab',NULL,'2027-10-01 03:00:00+03','3',NULL,'Perkinelmer')
) AS v("Name","Code","Type","Location","SetPointTemperature","CalibrationDueDate","CdsSoftware","ConnectionSettings","Vendor")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "Equipment" x WHERE x."Code" = v."Code");

------------------------------------------------------------------------------
-- 1. Reagents & Standards: material master (reagents, indicators, reference standards)
-- Natural key: section FP + Code (unique index).
------------------------------------------------------------------------------
INSERT INTO "MaterialMasterEntries"
  ("SectionId","Code","Name","Category","Grade","Source","BaseUnit","IsActive","WorkingConcentration","Solvent","TransitionRangeFrom","TransitionRangeTo","ColourChange","IndicatorUse","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec,
       v."Code",
       v."Name",
       v."Category",
       v."Grade",
       v."Source",
       v."BaseUnit",
       v."IsActive",
       v."WorkingConcentration",
       v."Solvent",
       v."TransitionRangeFrom",
       v."TransitionRangeTo",
       v."ColourChange",
       v."IndicatorUse",
       c.uid,
       v."CreatedAt",
       c.uid,
       v."LastModifiedAt"
FROM (VALUES
 ('IN-001'::character varying(50),'Phenolphthalein TS'::character varying(200),'1'::integer,'Indicator grade'::character varying(100),NULL::character varying(150),'8'::integer,'true'::boolean,'1 g in 100 mL'::character varying(100),'Alcohol'::character varying(100),'8.20'::numeric(5,2),'10.00'::numeric(5,2),'Colourless to pink'::character varying(100),'Acid-base (NaOH VS)'::character varying(100),'2026-09-30 19:56:56.029912+03'::timestamp with time zone,'2026-09-30 19:56:56.029912+03'::timestamp with time zone),
 ('IN-002','Methyl orange TS','1','Indicator grade',NULL,'8','true','0.1 g in 100 mL','Water','3.10','4.40','Red to yellow','Acid-base','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('IN-003','Bromocresol green TS','1','Indicator grade',NULL,'8','true','0.05 g in 100 mL','Alcohol','3.80','5.40','Yellow to blue','HCl VS vs tromethamine','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('IN-004','Crystal violet TS','1','Indicator grade',NULL,'8','true','0.5 g in 100 mL','Glacial acetic acid',NULL,NULL,'Violet to blue-green','Non-aqueous (HClO4 VS)','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('IN-005','Starch TS','1','Indicator grade',NULL,'8','true','1 g in 100 mL','Water',NULL,NULL,'Blue to colourless','Iodometry','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('IN-006','Hydroxy naphthol blue','1','Indicator grade',NULL,'0','true','Triturate',NULL,'12.00','13.00','Red to blue','Complexometry (EDTA VS)','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('IN-007','Eosin Y TS','1','Indicator grade',NULL,'8','true','50 mg in 10 mL','Water',NULL,NULL,'Pink on precipitate','Argentometry (AgNO3 VS)','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('IN-008','Potassium chromate TS','1','Indicator grade',NULL,'8','true','1 g in 20 mL','Water',NULL,NULL,'Yellow to red-brown','Argentometry (Mohr)','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('IN-009','Eriochrome black T','1','Indicator grade',NULL,'0','true','Triturate 1 in 100 NaCl',NULL,'10.00','10.00','Wine red to blue','Complexometry','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-001','Acetonitrile','0','HPLC grade',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-002','Methanol','0','HPLC grade',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-003','Water, HPLC','0','HPLC grade (Milli-Q)',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-004','Monobasic potassium phosphate (KH2PO4)','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-005','Phosphoric acid 85%','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-006','Glacial acetic acid','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-007','Potassium hydroxide (45% w/v solution)','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-008','Chloroacetic acid','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-009','Ammonium hydroxide 28%','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-010','Monobasic sodium phosphate (NaH2PO4.H2O)','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-011','Sodium hydroxide pellets','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-012','Hydrochloric acid 37%','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-013','Perchloric acid 70%','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-014','Acetic anhydride','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-015','Edetate disodium (EDTA-Na2.2H2O)','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-016','Sodium thiosulfate pentahydrate','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-017','Silver nitrate','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-018','Iodine','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-019','Potassium iodide','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-020','Potassium permanganate','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-021','Sodium carbonate, anhydrous','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-022','Potassium biphthalate (KHP)','0','Primary standard',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-023','Tromethamine','0','Primary standard',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-024','Calcium carbonate (chelometric standard)','0','Primary standard',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-025','Potassium dichromate','0','Primary standard',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-026','Sodium chloride','0','Primary standard',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-027','Sodium oxalate','0','Primary standard',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-028','Sodium 1-heptanesulfonate','0','HPLC grade',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-029','Sodium hydroxide 10 N (pH adjustment)','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-030','Sodium perchlorate','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-031','Sodium 1-hexanesulfonate','0','HPLC grade',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-032','n-Hexane (HPLC)','0','HPLC grade',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-033','1-Pentanol (n-amyl alcohol)','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-034','Toluene (HPLC)','0','HPLC grade',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-035','Benzoic acid','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-036','Dibasic sodium phosphate (Na2HPO4)','0','ACS reagent',NULL,'0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RG-037','Diethyl phthalate','0','ACS reagent',NULL,'3','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-001','Acetaminophen RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-002','Caffeine RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-003','Amoxicillin RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-004','Diclofenac Sodium RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-005','Ibuprofen RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-006','Metformin Hydrochloride RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-007','Diclofenac Related Compound A RS','2','Primary reference standard','USP (SST)','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-008','Valerophenone','2','Primary reference standard','USP reagent (SST / internal std)','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-009','Ascorbic Acid RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-010','Folic Acid RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-011','Cyanocobalamin RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-012','Cholecalciferol RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-013','Thiamine Hydrochloride RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-014','Riboflavin RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-015','Niacinamide RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-016','Pyridoxine Hydrochloride RS','2','Primary reference standard','USP','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-017','Folic Acid Related Compound A RS','2','Primary reference standard','USP (SST)','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03'),
 ('RS-018','USP Vitamin D Assay System Suitability RS','2','Primary reference standard','USP (SST)','0','true',NULL,NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03')
) AS v("Code","Name","Category","Grade","Source","BaseUnit","IsActive","WorkingConcentration","Solvent","TransitionRangeFrom","TransitionRangeTo","ColourChange","IndicatorUse","CreatedAt","LastModifiedAt")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v."Code");

------------------------------------------------------------------------------
-- 2. Stock lots (Materials) of the FP section
-- Natural key: section FP + Code + BatchNumber (no unique index exists). MediaProductId/OrganismId are micro-only and null for FP lots.
------------------------------------------------------------------------------
INSERT INTO "Materials"
  ("MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location","QuantityReceived","QuantityRemaining","Unit","MinimumStockLevel","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt","AtccNumber","SectionId","Purity","CustomType","MaterialMasterEntryId","MoisturePercent")
SELECT v."MaterialType",
       v."MaterialName",
       v."ManufacturerName",
       v."BatchNumber",
       v."ReceivingDate",
       v."ExpiryDate",
       v."Code",
       v."Location",
       v."QuantityReceived",
       v."QuantityRemaining",
       v."Unit",
       v."MinimumStockLevel",
       c.uid,
       v."CreatedAt",
       c.uid,
       v."LastModifiedAt",
       v."AtccNumber",
       c.sec,
       v."Purity",
       v."CustomType",
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v."_k_mme"),
       v."MoisturePercent"
FROM (VALUES
 ('7'::integer,'Phenolphthalein TS'::character varying(200),'Sigma-Aldrich'::character varying(150),'FP26-IN-001'::character varying(100),'2026-09-01 03:00:00+03'::timestamp with time zone,'2027-08-31 03:00:00+03'::timestamp with time zone,'IN-001'::character varying(50),'FP chemical store'::character varying(150),'2.000'::numeric(18,3),'2.000'::numeric(18,3),'8'::integer,NULL::numeric(18,3),'2026-09-30 19:56:56.029912+03'::timestamp with time zone,'2026-09-30 19:56:56.029912+03'::timestamp with time zone,NULL::character varying(50),NULL::numeric(6,3),NULL::character varying(100),NULL::numeric(6,3),'IN-001'::text),
 ('7','Methyl orange TS','Fisher Scientific','FP26-IN-002','2026-09-01 03:00:00+03','2027-08-31 03:00:00+03','IN-002','FP chemical store','2.000','2.000','8',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'IN-002'),
 ('7','Bromocresol green TS','Merck','FP26-IN-003','2026-09-01 03:00:00+03','2027-08-31 03:00:00+03','IN-003','FP chemical store','2.000','2.000','8',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'IN-003'),
 ('7','Crystal violet TS','Sigma-Aldrich','FP26-IN-004','2026-09-01 03:00:00+03','2027-08-31 03:00:00+03','IN-004','FP chemical store','2.000','2.000','8',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'IN-004'),
 ('7','Starch TS','Fisher Scientific','FP26-IN-005','2026-09-01 03:00:00+03','2027-08-31 03:00:00+03','IN-005','FP chemical store','2.000','2.000','8',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'IN-005'),
 ('7','Hydroxy naphthol blue','Merck','FP26-IN-006','2026-09-01 03:00:00+03','2027-08-31 03:00:00+03','IN-006','FP chemical store','25.000','25.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'IN-006'),
 ('7','Eosin Y TS','Sigma-Aldrich','FP26-IN-007','2026-09-01 03:00:00+03','2027-08-31 03:00:00+03','IN-007','FP chemical store','2.000','2.000','8',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'IN-007'),
 ('7','Potassium chromate TS','Fisher Scientific','FP26-IN-008','2026-09-01 03:00:00+03','2027-08-31 03:00:00+03','IN-008','FP chemical store','2.000','2.000','8',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'IN-008'),
 ('7','Eriochrome black T','Merck','FP26-IN-009','2026-09-01 03:00:00+03','2027-08-31 03:00:00+03','IN-009','FP chemical store','25.000','25.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'IN-009'),
 ('6','Acetonitrile','Sigma-Aldrich','FP26-RG-001','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-001','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-001'),
 ('6','Methanol','Fisher Scientific','FP26-RG-002','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-002','FP chemical store','2.500','2.250','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 20:09:37.174991+03',NULL,NULL,NULL,NULL,'RG-002'),
 ('6','Water, HPLC','Merck','FP26-RG-003','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-003','FP chemical store','2.500','0.250','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 20:35:33.594055+03',NULL,NULL,NULL,NULL,'RG-003'),
 ('6','Monobasic potassium phosphate (KH2PO4)','Sigma-Aldrich','FP26-RG-004','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-004','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-004'),
 ('6','Phosphoric acid 85%','Fisher Scientific','FP26-RG-005','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-005','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-005'),
 ('6','Glacial acetic acid','Merck','FP26-RG-006','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-006','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-006'),
 ('6','Potassium hydroxide (45% w/v solution)','Sigma-Aldrich','FP26-RG-007','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-007','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-007'),
 ('6','Chloroacetic acid','Fisher Scientific','FP26-RG-008','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-008','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-008'),
 ('6','Ammonium hydroxide 28%','Merck','FP26-RG-009','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-009','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-009'),
 ('6','Monobasic sodium phosphate (NaH2PO4.H2O)','Sigma-Aldrich','FP26-RG-010','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-010','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-010'),
 ('6','Sodium hydroxide pellets','Fisher Scientific','FP26-RG-011','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-011','FP chemical store','500.000','491.970','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 20:35:33.589283+03',NULL,NULL,NULL,NULL,'RG-011'),
 ('6','Hydrochloric acid 37%','Merck','FP26-RG-012','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-012','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-012'),
 ('6','Perchloric acid 70%','Sigma-Aldrich','FP26-RG-013','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-013','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-013'),
 ('6','Acetic anhydride','Fisher Scientific','FP26-RG-014','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-014','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-014'),
 ('6','Edetate disodium (EDTA-Na2.2H2O)','Merck','FP26-RG-015','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-015','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-015'),
 ('6','Sodium thiosulfate pentahydrate','Sigma-Aldrich','FP26-RG-016','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-016','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-016'),
 ('6','Silver nitrate','Fisher Scientific','FP26-RG-017','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-017','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-017'),
 ('6','Iodine','Merck','FP26-RG-018','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-018','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-018'),
 ('6','Potassium iodide','Sigma-Aldrich','FP26-RG-019','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-019','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-019'),
 ('6','Potassium permanganate','Fisher Scientific','FP26-RG-020','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-020','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-020'),
 ('6','Sodium carbonate, anhydrous','Merck','FP26-RG-021','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-021','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-021'),
 ('6','Potassium biphthalate (KHP)','Sigma-Aldrich','FP26-RG-022','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-022','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-022'),
 ('6','Tromethamine','Fisher Scientific','FP26-RG-023','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-023','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-023'),
 ('6','Calcium carbonate (chelometric standard)','Merck','FP26-RG-024','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-024','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-024'),
 ('6','Potassium dichromate','Sigma-Aldrich','FP26-RG-025','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-025','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-025'),
 ('6','Sodium chloride','Fisher Scientific','FP26-RG-026','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-026','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-026'),
 ('6','Sodium oxalate','Merck','FP26-RG-027','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-027','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-027'),
 ('6','Sodium 1-heptanesulfonate','Sigma-Aldrich','FP26-RG-028','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-028','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-028'),
 ('6','Sodium hydroxide 10 N (pH adjustment)','Fisher Scientific','FP26-RG-029','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-029','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-029'),
 ('6','Sodium perchlorate','Merck','FP26-RG-030','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-030','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-030'),
 ('6','Sodium 1-hexanesulfonate','Sigma-Aldrich','FP26-RG-031','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-031','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-031'),
 ('6','n-Hexane (HPLC)','Fisher Scientific','FP26-RG-032','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-032','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-032'),
 ('6','1-Pentanol (n-amyl alcohol)','Merck','FP26-RG-033','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-033','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-033'),
 ('6','Toluene (HPLC)','Sigma-Aldrich','FP26-RG-034','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-034','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-034'),
 ('6','Benzoic acid','Fisher Scientific','FP26-RG-035','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-035','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-035'),
 ('6','Dibasic sodium phosphate (Na2HPO4)','Merck','FP26-RG-036','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-036','FP chemical store','500.000','500.000','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-036'),
 ('6','Diethyl phthalate','Sigma-Aldrich','FP26-RG-037','2026-09-01 03:00:00+03','2028-08-31 03:00:00+03','RG-037','FP chemical store','2.500','2.500','3',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL,NULL,'RG-037'),
 ('11','Acetaminophen RS','USP','R10360','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-001','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.800',NULL,'0.100','RS-001'),
 ('11','Caffeine RS','USP','R10340','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-002','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.900',NULL,'0.200','RS-002'),
 ('11','Amoxicillin RS','USP','R11020','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-003','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'86.900',NULL,'13.200','RS-003'),
 ('11','Diclofenac Sodium RS','USP','R08790','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-004','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.600',NULL,'0.300','RS-004'),
 ('11','Ibuprofen RS','USP','R09860','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-005','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.700',NULL,'0.100','RS-005'),
 ('11','Metformin Hydrochloride RS','USP','R10610','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-006','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.900',NULL,'0.100','RS-006'),
 ('11','Diclofenac Related Compound A RS','USP','R05920','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-007','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'98.500',NULL,'0.500','RS-007'),
 ('11','Valerophenone','USP','R02100','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-008','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.000',NULL,'0.000','RS-008'),
 ('11','Ascorbic Acid RS','USP','R12010','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-009','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.800',NULL,'0.100','RS-009'),
 ('11','Folic Acid RS','USP','R13050','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-010','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'97.500',NULL,'8.000','RS-010'),
 ('11','Cyanocobalamin RS','USP','R14020','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-011','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'97.000',NULL,'6.500','RS-011'),
 ('11','Cholecalciferol RS','USP','R15030','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-012','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.000',NULL,'0.100','RS-012'),
 ('11','Thiamine Hydrochloride RS','USP','R16040','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-013','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.600',NULL,'0.300','RS-013'),
 ('11','Riboflavin RS','USP','R17050','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-014','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'98.500',NULL,'1.200','RS-014'),
 ('11','Niacinamide RS','USP','R18060','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-015','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.900',NULL,'0.100','RS-015'),
 ('11','Pyridoxine Hydrochloride RS','USP','R19070','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-016','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'99.800',NULL,'0.100','RS-016'),
 ('11','Folic Acid Related Compound A RS','USP','R20080','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-017','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'96.000',NULL,'5.000','RS-017'),
 ('11','USP Vitamin D Assay System Suitability RS','USP','R21090','2026-09-01 03:00:00+03','2027-12-31 02:00:00+02','RS-018','RS fridge 2-8 C','0.200','0.200','0',NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'100.000',NULL,'0.000','RS-018'),
 ('11','VIT-C Ascorbic ACID','USP','5465656321','2026-09-19 03:00:00+03','2027-10-01 03:00:00+03','RS-vit C','Refrigerator — LG (COD-F-WL-F-01-000)','20.000','20.000','0','2.000','2026-09-19 09:50:16.841994+03','2026-09-25 18:23:07.495356+03',NULL,'99.900',NULL,NULL,NULL)
) AS v("MaterialType","MaterialName","ManufacturerName","BatchNumber","ReceivingDate","ExpiryDate","Code","Location","QuantityReceived","QuantityRemaining","Unit","MinimumStockLevel","CreatedAt","LastModifiedAt","AtccNumber","Purity","CustomType","MoisturePercent","_k_mme")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "Materials" m WHERE m."SectionId" = c.sec AND m."BatchNumber" = v."BatchNumber" AND m."Code" IS NOT DISTINCT FROM v."Code");

------------------------------------------------------------------------------
-- 3. Chromatography Columns
-- Natural key: Code (unique index). UspDesignation copied as stored locally.
------------------------------------------------------------------------------
INSERT INTO "ChromatographyColumns"
  ("Code","Name","SerialNumber","SectionId","IsActive","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt","UspDesignation")
SELECT v."Code",
       v."Name",
       v."SerialNumber",
       c.sec,
       v."IsActive",
       c.uid,
       v."CreatedAt",
       c.uid,
       v."LastModifiedAt",
       v."UspDesignation"
FROM (VALUES
 ('COL-C18-01'::character varying(50),'Hypersil BDS C18 5um 4.6x250mm'::character varying(150),'SN-3201256'::character varying(100),'true'::boolean,'2026-09-19 09:48:14.594526+03'::timestamp with time zone,'2026-09-19 09:48:14.594721+03'::timestamp with time zone,'L1'::character varying(10)),
 ('COL-L1-02','Zorbax Eclipse XDB-C18 5um 4.6x150mm (USP L1)','SN-L1-0002','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L1'),
 ('COL-L1-03','Inertsil ODS-3 5um 4.6x250mm (USP L1)','SN-L1-0003','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L1'),
 ('COL-L1-04','Waters Symmetry C18 5um 3.9x300mm (USP L1)','SN-L1-0004','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L1'),
 ('COL-L1-05','Phenomenex Luna C18(2) 5um 4.6x250mm (USP L1)','SN-L1-0005','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L1'),
 ('COL-L1-06','Zorbax Eclipse XDB-C18 5um 4.6x100mm (USP L1)','SN-L1-0006','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L1'),
 ('COL-L10-01','Zorbax SB-CN 5um 4.6x250mm (USP L10)','SN-L10-0001','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L10'),
 ('COL-L11-01','Phenomenex Luna Phenyl-Hexyl 5um 4.6x250mm (USP L11)','SN-L11-0001','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L11'),
 ('COL-L3-01','Zorbax Rx-SIL 5um 4.6x250mm (USP L3)','SN-L3-0001','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L3'),
 ('COL-L39-01','Polymeric L39 column 5um 6.0x150mm (USP L39, supplier model to be chosen)','SN-L39-0001','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L39'),
 ('COL-L7-01','Zorbax Eclipse XDB-C8 5um 4.6x150mm (USP L7)','SN-L7-0001','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L7'),
 ('COL-L7-02','Inertsil C8-3 5um 4.6x250mm (USP L7)','SN-L7-0002','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L7'),
 ('COL-L9-01','Partisil SCX 10um 4.6x250mm (USP L9)','SN-L9-0001','true','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','L9')
) AS v("Code","Name","SerialNumber","IsActive","CreatedAt","LastModifiedAt","UspDesignation")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "ChromatographyColumns" x WHERE x."Code" = v."Code");

------------------------------------------------------------------------------
-- 3b. Column <-> instrument compatibility links
------------------------------------------------------------------------------
INSERT INTO "ChromatographyColumnEquipment"
  ("CompatibleColumnsId","CompatibleEquipmentId")
SELECT (SELECT x."Id" FROM "ChromatographyColumns" x WHERE x."Code" = v."_k_col"),
       (SELECT e."Id" FROM "Equipment" e WHERE e."Code" = v."_k_eq")
FROM (VALUES
 ('COL-C18-01'::text,'HPC-F-IL-F-08-028'::text),
 ('COL-L1-02','HPC-F-IL-F-08-028'),
 ('COL-L1-03','HPC-F-IL-F-08-028'),
 ('COL-L1-04','HPC-F-IL-F-08-028'),
 ('COL-L1-05','HPC-F-IL-F-08-028'),
 ('COL-L1-06','HPC-F-IL-F-08-028'),
 ('COL-L10-01','HPC-F-IL-F-08-028'),
 ('COL-L11-01','HPC-F-IL-F-08-028'),
 ('COL-L3-01','HPC-F-IL-F-08-028'),
 ('COL-L39-01','HPC-F-IL-F-08-028'),
 ('COL-L7-01','HPC-F-IL-F-08-028'),
 ('COL-L7-02','HPC-F-IL-F-08-028'),
 ('COL-L9-01','HPC-F-IL-F-08-028')
) AS v("_k_col","_k_eq")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "ChromatographyColumnEquipment" l JOIN "ChromatographyColumns" x ON x."Id"=l."CompatibleColumnsId" JOIN "Equipment" e ON e."Id"=l."CompatibleEquipmentId" WHERE x."Code" = v."_k_col" AND e."Code" = v."_k_eq");

------------------------------------------------------------------------------
-- 4. Solutions: masters (mobile phases, diluents, titrants without a reference solution)
-- Natural key: section FP + Name (unique index).
------------------------------------------------------------------------------
INSERT INTO "SolutionMasters"
  ("SectionId","Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","PhTarget","PhTolerance","PhAdjustingEntryId","Instructions","IsActive","NominalStrength","StrengthUnit","StandardizationMode","StandardEntryId","EquivalenceMgPerMl","ReferenceSolutionId","BlankRequired","ReplicateCount","FactorMin","FactorMax","MaxRsdPercent","ValidityDays","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec,
       v."Name",
       v."Type",
       v."ShelfLifeValue",
       v."ShelfLifeUnit",
       v."StorageCondition",
       v."FinalVolumeMl",
       v."PhTarget",
       v."PhTolerance",
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v."_k_ph"),
       v."Instructions",
       v."IsActive",
       v."NominalStrength",
       v."StrengthUnit",
       v."StandardizationMode",
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v."_k_std"),
       v."EquivalenceMgPerMl",
       (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v."_k_ref"),
       v."BlankRequired",
       v."ReplicateCount",
       v."FactorMin",
       v."FactorMax",
       v."MaxRsdPercent",
       v."ValidityDays",
       c.uid,
       v."CreatedAt",
       c.uid,
       v."LastModifiedAt"
FROM (VALUES
 ('0.05 M Edetate disodium VS'::character varying(200),'2'::integer,'30'::integer,'1'::integer,'Tightly closed, room temperature'::character varying(200),'1000.000'::numeric(10,3),NULL::numeric(4,2),NULL::numeric(4,2),'Dissolve 18.6 g edetate disodium in water to 1000 mL. Standardize against calcium carbonate (hydroxy naphthol blue).'::character varying(4000),'true'::boolean,'0.05000'::numeric(10,5),'1'::integer,'0'::integer,'5.004'::numeric(10,3),'false'::boolean,'3'::integer,'0.97000'::numeric(8,5),'1.03000'::numeric(8,5),'0.200'::numeric(6,3),'30'::integer,'2026-09-30 19:56:56.029912+03'::timestamp with time zone,'2026-09-30 19:56:56.029912+03'::timestamp with time zone,NULL::text,'RG-024'::text,NULL::text),
 ('0.1 N Hydrochloric acid VS','2','30','1','Tightly closed, room temperature','1000.000',NULL,NULL,'Dilute 8.5 mL hydrochloric acid 37% with water to 1000 mL. Standardize against tromethamine (bromocresol green TS).','true','0.10000','0','0','12.114','false','3','0.97000','1.03000','0.200','30','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'RG-023',NULL),
 ('0.1 N Perchloric acid VS (in glacial acetic acid)','2','30','1','Tightly closed, room temperature','1000.000',NULL,NULL,'Mix 8.5 mL perchloric acid 70% with 500 mL glacial acetic acid and 21 mL acetic anhydride, cool, dilute to 1000 mL with glacial acetic acid. Standardize against potassium biphthalate (crystal violet TS).','true','0.10000','0','0','20.420','true','3','0.97000','1.03000','0.200','30','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'RG-022',NULL),
 ('0.1 N Potassium permanganate VS','2','14','1','Tightly closed, amber glass, room temperature','1000.000',NULL,NULL,'Dissolve 3.3 g potassium permanganate in water to 1000 mL, heat, filter through glass wool. Standardize against sodium oxalate.','true','0.10000','0','0','6.700','false','3','0.97000','1.03000','0.200','14','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'RG-027',NULL),
 ('0.1 N Silver nitrate VS','2','30','1','Tightly closed, amber glass, room temperature','1000.000',NULL,NULL,'Dissolve 17.5 g silver nitrate in water to 1000 mL. Standardize against sodium chloride (eosin Y / potassium chromate).','true','0.10000','0','0','5.844','false','3','0.97000','1.03000','0.200','30','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'RG-026',NULL),
 ('0.1 N Sodium hydroxide VS','2','30','1','Tightly closed, room temperature, protected from CO2','1000.000',NULL,NULL,'Dissolve 4.0 g sodium hydroxide in CO2-free water to 1000 mL. Standardize against potassium biphthalate dried at 120 C for 2 h (USP <Volumetric Solutions>).','true','0.10000','0','0','20.420','true','3','0.97000','1.03000','0.200','30','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'RG-022',NULL),
 ('0.1 N Sodium thiosulfate VS','2','30','1','Tightly closed, amber glass, room temperature','1000.000',NULL,NULL,'Dissolve 26 g sodium thiosulfate pentahydrate and 0.2 g sodium carbonate in CO2-free water to 1000 mL. Standardize against potassium dichromate (iodometric).','true','0.10000','0','0','4.903','true','3','0.97000','1.03000','0.200','30','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,'RG-025',NULL),
 ('APAP diluent (water-methanol 3:1)','1','3','1','Room temperature, tightly closed','1000.000',NULL,NULL,'Same composition as the APAP mobile phase: 750 mL HPLC water and 250 mL methanol.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('APAP mobile phase (water-methanol 3:1)','0','7','1','Room temperature, tightly closed','1000.000',NULL,NULL,'Mix 750 mL HPLC water with 250 mL methanol. Filter and degas.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)','0','7','1','Room temperature, tightly closed','1000.000',NULL,NULL,'Mix 690 mL HPLC water, 280 mL methanol and 30 mL glacial acetic acid. Filter and degas.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('APAP-CAF solvent mixture (methanol-acetic acid 95:5)','1','3','1','Room temperature, tightly closed','1000.000',NULL,NULL,'Mix 950 mL methanol with 50 mL glacial acetic acid. (USP internal standard solution, benzoic acid in methanol, is not modelled: no internal standard in the system.)','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('Amoxicillin buffer pH 5.0','1','3','1','Room temperature, tightly closed','1000.000','5.00','0.10','Dissolve 6.8 g monobasic potassium phosphate in 1000 mL HPLC water; adjust to pH 5.0 +/- 0.1 with 45% KOH. Standard solution to be used within 6 h.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','RG-007',NULL,NULL),
 ('Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)','0','3','1','Room temperature, tightly closed','1000.000','5.00','0.10','Dissolve 6.528 g monobasic potassium phosphate in 960 mL HPLC water, adjust to pH 5.0 +/- 0.1 with 45% KOH, add 40 mL acetonitrile. Filter and degas.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','RG-007',NULL,NULL),
 ('Ascorbic acid diluent (mobile phase)','1','24','0','2-8 C, protected from light','1000.000','2.50','0.05','Same composition as the ascorbic acid mobile phase. Keep refrigerated and protected from light; use within 3 h after removal from the refrigerator.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','RG-005',NULL,NULL),
 ('Ascorbic acid mobile phase (phosphate pH 2.5)','0','3','1','Room temperature, tightly closed','1000.000','2.50','0.05','Dissolve 7.8 g dibasic sodium phosphate and 6.1 g monobasic potassium phosphate in 1000 mL HPLC water; adjust to pH 2.5 +/- 0.05 with phosphoric acid. Filter and degas.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','RG-005',NULL,NULL),
 ('B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)','1','3','1','Room temperature, protected from light','1000.000',NULL,NULL,'Mix 940 mL HPLC water, 50 mL acetonitrile and 10 mL glacial acetic acid; heat to 65-70 C to dissolve analytes, cool.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)','0','3','1','Room temperature, tightly closed','1010.000',NULL,NULL,'Mix 730 mL HPLC water, 270 mL methanol and 10 mL glacial acetic acid containing 1.4 g sodium 1-hexanesulfonate. Filter and degas; adjust per SST if needed.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('Cholecalciferol diluent (toluene)','1','1','1','Room temperature, flammables cabinet, protected from light','1000.000',NULL,NULL,'Toluene for the standard/sample stock. Prepare fresh daily in low-actinic glassware.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('Cholecalciferol mobile phase (n-amyl alcohol in hexane 3:1000)','0','3','1','Room temperature, flammables cabinet, tightly closed','1000.000',NULL,NULL,'Mix 3 mL 1-pentanol with 997 mL dehydrated hexane.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('Cyanocobalamin diluent (water)','1','3','1','Room temperature, protected from light','1000.000',NULL,NULL,'HPLC water in low-actinic glassware.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('Cyanocobalamin mobile phase (water-methanol 65:35)','0','3','1','Room temperature, tightly closed','1000.000',NULL,NULL,'Mix 650 mL HPLC water with 350 mL methanol. Filter and degas.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('Diclofenac diluent (methanol-water 7:3)','1','3','1','Room temperature, tightly closed','1000.000',NULL,NULL,'Mix 700 mL methanol with 300 mL HPLC water.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)','0','3','1','Room temperature, tightly closed','1000.000','2.50','0.20','Solution A: equal volumes of 0.01 M phosphoric acid and 0.01 M monobasic sodium phosphate, pH 2.5 +/- 0.2. Mobile phase: 700 mL methanol and 300 mL Solution A. Filter and degas.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','RG-005',NULL,NULL),
 ('Folic acid diluent (ammonium hydroxide-perchlorate)','1','3','1','Room temperature, protected from light','100.000',NULL,NULL,'Mix 2 mL ammonium hydroxide and 1 g sodium perchlorate in HPLC water to 100 mL.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03',NULL,NULL,NULL),
 ('Folic acid mobile phase (perchlorate-phosphate pH 7.2)','0','3','1','Room temperature, tightly closed','1000.000','7.20','0.10','In a 1-L flask dissolve 35.1 g sodium perchlorate and 1.40 g monobasic potassium phosphate in about 900 mL HPLC water, add 7.0 mL 1 N KOH (0.873 mL of 45% KOH diluted) and 40 mL methanol, dilute to 1000 mL, adjust to pH 7.2 with 1 N KOH or phosphoric acid.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','RG-007',NULL,NULL),
 ('Ibuprofen internal standard diluent (valerophenone in mobile phase)','1','3','1','Room temperature, tightly closed','1000.000','3.00','0.10','Ibuprofen mobile phase containing valerophenone about 0.35 mg/mL (USP internal standard solution). The system has no internal standard: kept as the diluent only.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','RG-009',NULL,NULL),
 ('Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)','0','3','1','Room temperature, tightly closed','1000.000','3.00','0.10','Dissolve 4.0 g chloroacetic acid in 400 mL HPLC water, adjust to pH 3.0 with ammonium hydroxide, add 600 mL acetonitrile. Filter and degas.','true',NULL,NULL,NULL,NULL,'false',NULL,NULL,NULL,NULL,NULL,'2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','RG-009',NULL,NULL)
) AS v("Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","PhTarget","PhTolerance","Instructions","IsActive","NominalStrength","StrengthUnit","StandardizationMode","EquivalenceMgPerMl","BlankRequired","ReplicateCount","FactorMin","FactorMax","MaxRsdPercent","ValidityDays","CreatedAt","LastModifiedAt","_k_ph","_k_std","_k_ref")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v."Name");

------------------------------------------------------------------------------
-- 4b. Solutions: titrants standardised against another solution (must follow 4)
------------------------------------------------------------------------------
INSERT INTO "SolutionMasters"
  ("SectionId","Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","PhTarget","PhTolerance","PhAdjustingEntryId","Instructions","IsActive","NominalStrength","StrengthUnit","StandardizationMode","StandardEntryId","EquivalenceMgPerMl","ReferenceSolutionId","BlankRequired","ReplicateCount","FactorMin","FactorMax","MaxRsdPercent","ValidityDays","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec,
       v."Name",
       v."Type",
       v."ShelfLifeValue",
       v."ShelfLifeUnit",
       v."StorageCondition",
       v."FinalVolumeMl",
       v."PhTarget",
       v."PhTolerance",
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v."_k_ph"),
       v."Instructions",
       v."IsActive",
       v."NominalStrength",
       v."StrengthUnit",
       v."StandardizationMode",
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v."_k_std"),
       v."EquivalenceMgPerMl",
       (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v."_k_ref"),
       v."BlankRequired",
       v."ReplicateCount",
       v."FactorMin",
       v."FactorMax",
       v."MaxRsdPercent",
       v."ValidityDays",
       c.uid,
       v."CreatedAt",
       c.uid,
       v."LastModifiedAt"
FROM (VALUES
 ('0.1 N Iodine VS'::character varying(200),'2'::integer,'14'::integer,'1'::integer,'Tightly closed, amber glass, room temperature'::character varying(200),'1000.000'::numeric(10,3),NULL::numeric(4,2),NULL::numeric(4,2),'Dissolve 14 g iodine and 36 g potassium iodide in 100 mL water, add 3 drops hydrochloric acid, dilute with water to 1000 mL. Standardize against 0.1 N sodium thiosulfate VS (starch TS).'::character varying(4000),'true'::boolean,'0.10000'::numeric(10,5),'0'::integer,'1'::integer,NULL::numeric(10,3),'false'::boolean,'3'::integer,'0.97000'::numeric(8,5),'1.03000'::numeric(8,5),'0.200'::numeric(6,3),'14'::integer,'2026-09-30 19:56:56.029912+03'::timestamp with time zone,'2026-09-30 19:56:56.029912+03'::timestamp with time zone,NULL::text,NULL::text,'0.1 N Sodium thiosulfate VS'::text)
) AS v("Name","Type","ShelfLifeValue","ShelfLifeUnit","StorageCondition","FinalVolumeMl","PhTarget","PhTolerance","Instructions","IsActive","NominalStrength","StrengthUnit","StandardizationMode","EquivalenceMgPerMl","BlankRequired","ReplicateCount","FactorMin","FactorMax","MaxRsdPercent","ValidityDays","CreatedAt","LastModifiedAt","_k_ph","_k_std","_k_ref")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v."Name");

------------------------------------------------------------------------------
-- 4c. Solution components
-- Natural key: solution Name + component Order.
------------------------------------------------------------------------------
INSERT INTO "SolutionComponents"
  ("SolutionMasterId","Order","MaterialMasterEntryId","Quantity","Unit")
SELECT (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v."_k_sol"),
       v."Order",
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v."_k_mme"),
       v."Quantity",
       v."Unit"
FROM (VALUES
 ('1'::integer,'18.6000'::numeric(12,4),'0'::integer,'0.05 M Edetate disodium VS'::text,'RG-015'::text),
 ('2','1000.0000','2','0.05 M Edetate disodium VS','RG-003'),
 ('1','8.5000','2','0.1 N Hydrochloric acid VS','RG-012'),
 ('2','991.5000','2','0.1 N Hydrochloric acid VS','RG-003'),
 ('1','14.0000','0','0.1 N Iodine VS','RG-018'),
 ('2','36.0000','0','0.1 N Iodine VS','RG-019'),
 ('3','0.1500','2','0.1 N Iodine VS','RG-012'),
 ('4','1000.0000','2','0.1 N Iodine VS','RG-003'),
 ('1','8.5000','2','0.1 N Perchloric acid VS (in glacial acetic acid)','RG-013'),
 ('2','970.5000','2','0.1 N Perchloric acid VS (in glacial acetic acid)','RG-006'),
 ('3','21.0000','2','0.1 N Perchloric acid VS (in glacial acetic acid)','RG-014'),
 ('1','3.3000','0','0.1 N Potassium permanganate VS','RG-020'),
 ('2','1000.0000','2','0.1 N Potassium permanganate VS','RG-003'),
 ('1','17.5000','0','0.1 N Silver nitrate VS','RG-017'),
 ('2','1000.0000','2','0.1 N Silver nitrate VS','RG-003'),
 ('1','4.0000','0','0.1 N Sodium hydroxide VS','RG-011'),
 ('2','1000.0000','2','0.1 N Sodium hydroxide VS','RG-003'),
 ('1','26.0000','0','0.1 N Sodium thiosulfate VS','RG-016'),
 ('2','0.2000','0','0.1 N Sodium thiosulfate VS','RG-021'),
 ('3','1000.0000','2','0.1 N Sodium thiosulfate VS','RG-003'),
 ('1','750.0000','2','APAP diluent (water-methanol 3:1)','RG-003'),
 ('2','250.0000','2','APAP diluent (water-methanol 3:1)','RG-002'),
 ('1','750.0000','2','APAP mobile phase (water-methanol 3:1)','RG-003'),
 ('2','250.0000','2','APAP mobile phase (water-methanol 3:1)','RG-002'),
 ('1','690.0000','2','APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)','RG-003'),
 ('2','280.0000','2','APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)','RG-002'),
 ('3','30.0000','2','APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)','RG-006'),
 ('1','950.0000','2','APAP-CAF solvent mixture (methanol-acetic acid 95:5)','RG-002'),
 ('2','50.0000','2','APAP-CAF solvent mixture (methanol-acetic acid 95:5)','RG-006'),
 ('1','6.8000','0','Amoxicillin buffer pH 5.0','RG-004'),
 ('2','1000.0000','2','Amoxicillin buffer pH 5.0','RG-003'),
 ('1','40.0000','2','Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)','RG-001'),
 ('2','960.0000','2','Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)','RG-003'),
 ('3','6.5280','0','Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)','RG-004'),
 ('1','7.8000','0','Ascorbic acid diluent (mobile phase)','RG-036'),
 ('2','6.1000','0','Ascorbic acid diluent (mobile phase)','RG-004'),
 ('3','1000.0000','2','Ascorbic acid diluent (mobile phase)','RG-003'),
 ('1','7.8000','0','Ascorbic acid mobile phase (phosphate pH 2.5)','RG-036'),
 ('2','6.1000','0','Ascorbic acid mobile phase (phosphate pH 2.5)','RG-004'),
 ('3','1000.0000','2','Ascorbic acid mobile phase (phosphate pH 2.5)','RG-003'),
 ('1','940.0000','2','B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)','RG-003'),
 ('2','50.0000','2','B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)','RG-001'),
 ('3','10.0000','2','B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)','RG-006'),
 ('1','730.0000','2','B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)','RG-003'),
 ('2','270.0000','2','B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)','RG-002'),
 ('3','10.0000','2','B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)','RG-006'),
 ('4','1.4000','0','B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)','RG-031'),
 ('1','1000.0000','2','Cholecalciferol diluent (toluene)','RG-034'),
 ('1','3.0000','2','Cholecalciferol mobile phase (n-amyl alcohol in hexane 3:1000)','RG-033'),
 ('2','997.0000','2','Cholecalciferol mobile phase (n-amyl alcohol in hexane 3:1000)','RG-032'),
 ('1','1000.0000','2','Cyanocobalamin diluent (water)','RG-003'),
 ('1','650.0000','2','Cyanocobalamin mobile phase (water-methanol 65:35)','RG-003'),
 ('2','350.0000','2','Cyanocobalamin mobile phase (water-methanol 65:35)','RG-002'),
 ('1','700.0000','2','Diclofenac diluent (methanol-water 7:3)','RG-002'),
 ('2','300.0000','2','Diclofenac diluent (methanol-water 7:3)','RG-003'),
 ('1','700.0000','2','Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)','RG-002'),
 ('2','300.0000','2','Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)','RG-003'),
 ('3','0.1020','2','Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)','RG-005'),
 ('4','0.2070','0','Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)','RG-010'),
 ('1','2.0000','2','Folic acid diluent (ammonium hydroxide-perchlorate)','RG-009'),
 ('2','1.0000','0','Folic acid diluent (ammonium hydroxide-perchlorate)','RG-030'),
 ('3','98.0000','2','Folic acid diluent (ammonium hydroxide-perchlorate)','RG-003'),
 ('1','35.1000','0','Folic acid mobile phase (perchlorate-phosphate pH 7.2)','RG-030'),
 ('2','1.4000','0','Folic acid mobile phase (perchlorate-phosphate pH 7.2)','RG-004'),
 ('3','0.8730','2','Folic acid mobile phase (perchlorate-phosphate pH 7.2)','RG-007'),
 ('4','40.0000','2','Folic acid mobile phase (perchlorate-phosphate pH 7.2)','RG-002'),
 ('5','950.0000','2','Folic acid mobile phase (perchlorate-phosphate pH 7.2)','RG-003'),
 ('1','4.0000','0','Ibuprofen internal standard diluent (valerophenone in mobile phase)','RG-008'),
 ('2','400.0000','2','Ibuprofen internal standard diluent (valerophenone in mobile phase)','RG-003'),
 ('3','600.0000','2','Ibuprofen internal standard diluent (valerophenone in mobile phase)','RG-001'),
 ('4','0.3500','0','Ibuprofen internal standard diluent (valerophenone in mobile phase)','RS-008'),
 ('1','4.0000','0','Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)','RG-008'),
 ('2','400.0000','2','Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)','RG-003'),
 ('3','600.0000','2','Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)','RG-001')
) AS v("Order","Quantity","Unit","_k_sol","_k_mme")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SolutionComponents" sc JOIN "SolutionMasters" s ON s."Id"=sc."SolutionMasterId" WHERE s."SectionId" = c.sec AND s."Name" = v."_k_sol" AND sc."Order" = v."Order");

------------------------------------------------------------------------------
-- 5. HPLC Methods
-- Natural key: section FP + Abbreviation (unique index).
------------------------------------------------------------------------------
INSERT INTO "HplcMethods"
  ("SectionId","Name","Abbreviation","EffectiveDate","IsActive","ColumnDesignation","ColumnLengthMm","ColumnInternalDiameterMm","ParticleSizeUm","ColumnBrand","ColumnPartNumber","ColumnTemperatureC","ElutionMode","EquilibrationMin","FlowRateMlPerMin","DetectorType","InjectionVolumeUl","RunTimeMin","DiluentSolutionId","CreatedByUserId","CreatedAt","LastModifiedByUserId","LastModifiedAt")
SELECT c.sec,
       v."Name",
       v."Abbreviation",
       v."EffectiveDate",
       v."IsActive",
       v."ColumnDesignation",
       v."ColumnLengthMm",
       v."ColumnInternalDiameterMm",
       v."ParticleSizeUm",
       v."ColumnBrand",
       v."ColumnPartNumber",
       v."ColumnTemperatureC",
       v."ElutionMode",
       v."EquilibrationMin",
       v."FlowRateMlPerMin",
       v."DetectorType",
       v."InjectionVolumeUl",
       v."RunTimeMin",
       (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v."_k_dil"),
       c.uid,
       v."CreatedAt",
       c.uid,
       v."LastModifiedAt"
FROM (VALUES
 ('Amoxicillin Capsules Assay (HPLC)'::character varying(200),'AMOX'::character varying(20),'2026-09-01 03:00:00+03'::timestamp with time zone,'true'::boolean,'L1'::character varying(20),'250.000'::numeric(10,3),'4.600'::numeric(10,3),'5.000'::numeric(10,3),'Inertsil ODS-3'::character varying(100),NULL::character varying(100),'25.000'::numeric(10,3),'0'::integer,NULL::numeric(10,3),'1.500'::numeric(10,3),'0'::integer,'10.000'::numeric(10,3),'20.000'::numeric(10,3),'2026-09-30 19:56:56.029912+03'::timestamp with time zone,'2026-09-30 19:56:56.029912+03'::timestamp with time zone,'Amoxicillin buffer pH 5.0'::text),
 ('Acetaminophen Tablets Assay (HPLC)','APAP','2026-09-01 03:00:00+03','true','L1','300.000','3.900','5.000','Waters Symmetry C18',NULL,'25.000','0',NULL,'1.500','0','10.000','15.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','APAP diluent (water-methanol 3:1)'),
 ('Acetaminophen and Caffeine Tablets Assay (HPLC)','APAP-CAF','2026-09-01 03:00:00+03','true','L1','100.000','4.600','5.000','Zorbax Eclipse XDB-C18',NULL,'45.000','0',NULL,'2.000','0','10.000','15.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','APAP-CAF solvent mixture (methanol-acetic acid 95:5)'),
 ('Cyanocobalamin Assay (HPLC)','B12','2026-09-01 03:00:00+03','true','L1','150.000','4.600','5.000','Zorbax Eclipse XDB-C18',NULL,'25.000','0',NULL,'0.500','0','200.000','15.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','Cyanocobalamin diluent (water)'),
 ('Diclofenac Sodium DR Tablets Assay (HPLC)','DICLO','2026-09-01 03:00:00+03','true','L7','250.000','4.600','5.000','Inertsil C8-3',NULL,'25.000','0',NULL,'1.000','0','10.000','20.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','Diclofenac diluent (methanol-water 7:3)'),
 ('Folic Acid Tablets Assay (HPLC)','FOLIC','2026-09-01 03:00:00+03','true','L1','250.000','4.600','5.000','Hypersil BDS C18',NULL,'25.000','0',NULL,'1.000','0','25.000','25.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','Folic acid diluent (ammonium hydroxide-perchlorate)'),
 ('Ibuprofen Tablets Assay (HPLC)','IBU','2026-09-01 03:00:00+03','true','L1','250.000','4.600','5.000','Hypersil BDS C18',NULL,'25.000','0',NULL,'2.000','0','5.000','20.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','Ibuprofen internal standard diluent (valerophenone in mobile phase)'),
 ('B-complex Assay (HPLC)','VIT-B','2026-09-01 03:00:00+03','true','L1','300.000','3.900','5.000','Waters Symmetry C18',NULL,'25.000','0',NULL,'1.000','0','10.000','35.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','B-complex diluting solution (water-acetonitrile-acetic acid 94:5:1)'),
 ('Ascorbic Acid Assay (HPLC)','VIT-C','2026-09-01 03:00:00+03','true','L39','150.000','6.000','5.000','Polymeric L39',NULL,'25.000','0',NULL,'0.600','0','4.000','10.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','Ascorbic acid diluent (mobile phase)'),
 ('Cholecalciferol Assay (HPLC, normal phase)','VIT-D3','2026-09-01 03:00:00+03','true','L3','250.000','4.600','5.000','Zorbax Rx-SIL',NULL,'25.000','0',NULL,'2.000','0','10.000','25.000','2026-09-30 19:56:56.029912+03','2026-09-30 19:56:56.029912+03','Cholecalciferol diluent (toluene)')
) AS v("Name","Abbreviation","EffectiveDate","IsActive","ColumnDesignation","ColumnLengthMm","ColumnInternalDiameterMm","ParticleSizeUm","ColumnBrand","ColumnPartNumber","ColumnTemperatureC","ElutionMode","EquilibrationMin","FlowRateMlPerMin","DetectorType","InjectionVolumeUl","RunTimeMin","CreatedAt","LastModifiedAt","_k_dil")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethods" m WHERE m."SectionId" = c.sec AND m."Abbreviation" = v."Abbreviation");

------------------------------------------------------------------------------
-- 5b. HPLC method mobile phases
------------------------------------------------------------------------------
INSERT INTO "HplcMethodMobilePhases"
  ("HplcMethodId","Channel","SolutionMasterId","RatioPercent")
SELECT (SELECT m."Id" FROM "HplcMethods" m WHERE m."SectionId" = c.sec AND m."Abbreviation" = v."_k_m"),
       v."Channel",
       (SELECT s."Id" FROM "SolutionMasters" s WHERE s."SectionId" = c.sec AND s."Name" = v."_k_sol"),
       v."RatioPercent"
FROM (VALUES
 ('A'::character varying(1),'100.000'::numeric(10,3),'AMOX'::text,'Amoxicillin mobile phase (acetonitrile-pH 5.0 buffer 1:24)'::text),
 ('A','100.000','APAP','APAP mobile phase (water-methanol 3:1)'),
 ('A','100.000','APAP-CAF','APAP-CAF mobile phase (water-methanol-acetic acid 69:28:3)'),
 ('A','100.000','B12','Cyanocobalamin mobile phase (water-methanol 65:35)'),
 ('A','100.000','DICLO','Diclofenac mobile phase (methanol-pH 2.5 phosphate 7:3)'),
 ('A','100.000','FOLIC','Folic acid mobile phase (perchlorate-phosphate pH 7.2)'),
 ('A','100.000','IBU','Ibuprofen mobile phase (chloroacetate pH 3.0-acetonitrile 40:60)'),
 ('A','100.000','VIT-B','B-complex mobile phase (water-methanol-acetic acid 73:27:1, hexanesulfonate)'),
 ('A','100.000','VIT-C','Ascorbic acid mobile phase (phosphate pH 2.5)'),
 ('A','100.000','VIT-D3','Cholecalciferol mobile phase (n-amyl alcohol in hexane 3:1000)')
) AS v("Channel","RatioPercent","_k_m","_k_sol")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethodMobilePhases" p JOIN "HplcMethods" m ON m."Id"=p."HplcMethodId" WHERE m."SectionId" = c.sec AND m."Abbreviation" = v."_k_m" AND p."Channel" = v."Channel");

------------------------------------------------------------------------------
-- 5c. HPLC method gradient steps
------------------------------------------------------------------------------
-- (no rows in LIMSV2 for "HplcMethodGradientSteps")

------------------------------------------------------------------------------
-- 5d. HPLC method analytes (with SST criteria)
-- Natural key: method Abbreviation + analyte Name (unique index).
------------------------------------------------------------------------------
INSERT INTO "HplcMethodAnalytes"
  ("HplcMethodId","DisplayOrder","Name","WavelengthNm","StandardEntryId","TheoreticalWeightStdMg","TheoreticalWeightTestMg","StandardInjections","SstMaxRsdPercent","SstMinResolution","SstMaxTailingFactor","SstMinTheoreticalPlates","SstMinRetentionFactor","SstMinSignalToNoise","SstMinPeakToValley")
SELECT (SELECT m."Id" FROM "HplcMethods" m WHERE m."SectionId" = c.sec AND m."Abbreviation" = v."_k_m"),
       v."DisplayOrder",
       v."Name",
       v."WavelengthNm",
       (SELECT e."Id" FROM "MaterialMasterEntries" e WHERE e."SectionId" = c.sec AND e."Code" = v."_k_std"),
       v."TheoreticalWeightStdMg",
       v."TheoreticalWeightTestMg",
       v."StandardInjections",
       v."SstMaxRsdPercent",
       v."SstMinResolution",
       v."SstMaxTailingFactor",
       v."SstMinTheoreticalPlates",
       v."SstMinRetentionFactor",
       v."SstMinSignalToNoise",
       v."SstMinPeakToValley"
FROM (VALUES
 ('1'::integer,'Amoxicillin'::character varying(100),'230.000'::numeric(10,3),'120.0000'::numeric(12,4),'240.0000'::numeric(12,4),'5'::integer,'2.000'::numeric(10,3),NULL::numeric(10,3),'2.500'::numeric(10,3),NULL::numeric(10,3),NULL::numeric(10,3),NULL::numeric(10,3),NULL::numeric(10,3),'AMOX'::text,'RS-003'::text),
 ('1','Acetaminophen','243.000','100.0000','120.0000','5','2.000',NULL,'2.000','1000.000',NULL,NULL,NULL,'APAP','RS-001'),
 ('1','Acetaminophen','275.000','25.0000','325.0000','5','2.000','1.400','1.200',NULL,NULL,NULL,NULL,'APAP-CAF','RS-001'),
 ('2','Caffeine','275.000','3.2500','325.0000','5','2.000','1.400','1.200',NULL,NULL,NULL,NULL,'APAP-CAF','RS-002'),
 ('1','Cyanocobalamin','550.000','1.0000','50.0000','6','3.000',NULL,NULL,NULL,NULL,NULL,NULL,'B12','RS-011'),
 ('1','Diclofenac sodium','254.000','75.0000','375.0000','5','2.000','6.500',NULL,NULL,NULL,NULL,NULL,'DICLO','RS-004'),
 ('1','Folic acid','254.000','10.0000','240.0000','5','2.000','3.600',NULL,NULL,NULL,NULL,NULL,'FOLIC','RS-010'),
 ('1','Ibuprofen','254.000','1200.0000','1800.0000','5','2.000','2.500','2.500',NULL,NULL,NULL,NULL,'IBU','RS-005'),
 ('1','Niacinamide','280.000','80.0000','50.0000','6','3.000',NULL,NULL,NULL,NULL,NULL,NULL,'VIT-B','RS-015'),
 ('2','Pyridoxine HCl','280.000','20.0000','50.0000','6','3.000',NULL,NULL,NULL,NULL,NULL,NULL,'VIT-B','RS-016'),
 ('3','Riboflavin','280.000','20.0000','50.0000','6','3.000',NULL,NULL,NULL,NULL,NULL,NULL,'VIT-B','RS-014'),
 ('4','Thiamine HCl','280.000','20.0000','50.0000','6','3.000',NULL,NULL,NULL,NULL,NULL,NULL,'VIT-B','RS-013'),
 ('1','Ascorbic acid','245.000','50.0000','140.0000','6','1.500',NULL,'1.600','3500.000',NULL,NULL,NULL,'VIT-C','RS-009'),
 ('1','Cholecalciferol','254.000','6.0000','6.0000','5','2.000','1.000',NULL,NULL,NULL,NULL,NULL,'VIT-D3','RS-012')
) AS v("DisplayOrder","Name","WavelengthNm","TheoreticalWeightStdMg","TheoreticalWeightTestMg","StandardInjections","SstMaxRsdPercent","SstMinResolution","SstMaxTailingFactor","SstMinTheoreticalPlates","SstMinRetentionFactor","SstMinSignalToNoise","SstMinPeakToValley","_k_m","_k_std")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "HplcMethodAnalytes" a JOIN "HplcMethods" m ON m."Id"=a."HplcMethodId" WHERE m."SectionId" = c.sec AND m."Abbreviation" = v."_k_m" AND a."Name" = v."Name");

------------------------------------------------------------------------------
-- 6. Physicochemical Test Master (test definitions)
-- Natural key: Code (unique index).
------------------------------------------------------------------------------
INSERT INTO "TestDefinitions"
  ("Code","DisplayName","IsActive","WorkflowType","SectionId","EquationType","MethodAbbreviation","RequiresSystemSuitability","SstMaxRsdPercent","SstMaxTailingFactor","SstMinResolution","SstMinTheoreticalPlates","CalBlankMax","CalCheckRecoveryHighPercent","CalCheckRecoveryLowPercent","CalCorrelationType","CalIsRecoveryHighPercent","CalIsRecoveryLowPercent","CalMaxRunAgeHours","CalMinCorrelation","CalMinStandards","CalRequireBlank","CalRequireCcv","CalRequireIcv","CalRequireInternalStandard","CalibrationEntryMode","ReportedConcentrationBasis","EvaluationBasis","ReplicateCount","ConditionFields","UsesTare","DissolutionS1Offset","DissolutionS2MinOffset","DissolutionS3MaxBelowS2Min","DissolutionS3MinOffset","DisintegrationMaxStage1Failures","DisintegrationMinPassTotal","DisintegrationStage1Units","DisintegrationStage2Units","WvCapsuleInnerPercent","WvCapsuleOuterPercent","WvCapsuleS1MaxForRetest","WvCapsuleS1MaxOutside","WvCapsuleS2ExtraUnits","WvCapsuleS2MaxOutside","WvTabletBand1MaxMg","WvTabletBand1Percent","WvTabletBand2MaxMg","WvTabletBand2Percent","WvTabletBand3Percent","WvTabletMaxOutside","WvUnitCount","HplcMaxPreparationRsdPercent","ResponseMode","CalInstrumentType","CalStandardLevelsMgPerL","HplcMethodId")
SELECT v."Code",
       v."DisplayName",
       v."IsActive",
       v."WorkflowType",
       c.sec,
       v."EquationType",
       v."MethodAbbreviation",
       v."RequiresSystemSuitability",
       v."SstMaxRsdPercent",
       v."SstMaxTailingFactor",
       v."SstMinResolution",
       v."SstMinTheoreticalPlates",
       v."CalBlankMax",
       v."CalCheckRecoveryHighPercent",
       v."CalCheckRecoveryLowPercent",
       v."CalCorrelationType",
       v."CalIsRecoveryHighPercent",
       v."CalIsRecoveryLowPercent",
       v."CalMaxRunAgeHours",
       v."CalMinCorrelation",
       v."CalMinStandards",
       v."CalRequireBlank",
       v."CalRequireCcv",
       v."CalRequireIcv",
       v."CalRequireInternalStandard",
       v."CalibrationEntryMode",
       v."ReportedConcentrationBasis",
       v."EvaluationBasis",
       v."ReplicateCount",
       v."ConditionFields",
       v."UsesTare",
       v."DissolutionS1Offset",
       v."DissolutionS2MinOffset",
       v."DissolutionS3MaxBelowS2Min",
       v."DissolutionS3MinOffset",
       v."DisintegrationMaxStage1Failures",
       v."DisintegrationMinPassTotal",
       v."DisintegrationStage1Units",
       v."DisintegrationStage2Units",
       v."WvCapsuleInnerPercent",
       v."WvCapsuleOuterPercent",
       v."WvCapsuleS1MaxForRetest",
       v."WvCapsuleS1MaxOutside",
       v."WvCapsuleS2ExtraUnits",
       v."WvCapsuleS2MaxOutside",
       v."WvTabletBand1MaxMg",
       v."WvTabletBand1Percent",
       v."WvTabletBand2MaxMg",
       v."WvTabletBand2Percent",
       v."WvTabletBand3Percent",
       v."WvTabletMaxOutside",
       v."WvUnitCount",
       v."HplcMaxPreparationRsdPercent",
       v."ResponseMode",
       v."CalInstrumentType",
       v."CalStandardLevelsMgPerL",
       (SELECT m."Id" FROM "HplcMethods" m WHERE m."SectionId" = c.sec AND m."Abbreviation" = v."_k_m")
FROM (VALUES
 ('Apperance'::character varying(100),'Apperance'::character varying(200),'true'::boolean,'6'::integer,'7'::integer,NULL::character varying(20),'false'::boolean,NULL::numeric(18,4),NULL::numeric(18,4),NULL::numeric(18,4),NULL::numeric(18,4),NULL::numeric(18,6),NULL::numeric(18,4),NULL::numeric(18,4),NULL::integer,NULL::numeric(18,4),NULL::numeric(18,4),'24'::integer,NULL::numeric(10,6),NULL::integer,NULL::boolean,NULL::boolean,NULL::boolean,NULL::boolean,NULL::integer,NULL::integer,NULL::integer,NULL::integer,NULL::character varying(500),NULL::boolean,NULL::numeric(28,10),NULL::numeric(28,10),NULL::numeric(28,10),NULL::numeric(28,10),NULL::integer,NULL::integer,NULL::integer,NULL::integer,NULL::numeric(28,10),NULL::numeric(28,10),NULL::integer,NULL::integer,NULL::integer,NULL::integer,NULL::numeric(18,6),NULL::numeric(28,10),NULL::numeric(18,6),NULL::numeric(28,10),NULL::numeric(28,10),NULL::integer,NULL::integer,NULL::numeric(18,4),'0'::integer,NULL::integer,NULL::character varying(200),NULL::text),
 ('Average Weight','Average Weight','true','4','4',NULL,'false',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0','10',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,NULL),
 ('HPLC-AMOX','Amoxicillin Capsules Assay (HPLC)','true','12','13','AMOX','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'AMOX'),
 ('HPLC-APAP','Acetaminophen Tablets Assay (HPLC)','true','12','13','APAP','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'APAP'),
 ('HPLC-APAP-CAF','Acetaminophen and Caffeine Tablets Assay (HPLC)','true','12','13','APAP-CAF','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'APAP-CAF'),
 ('HPLC-B12','Cyanocobalamin Assay (HPLC)','true','12','13','B12','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'B12'),
 ('HPLC-DICLO','Diclofenac Sodium DR Tablets Assay (HPLC)','true','12','13','DICLO','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'DICLO'),
 ('HPLC-FOLIC','Folic Acid Tablets Assay (HPLC)','true','12','13','FOLIC','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'FOLIC'),
 ('HPLC-IBU','Ibuprofen Tablets Assay (HPLC)','true','12','13','IBU','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'IBU'),
 ('HPLC-VIT-B','B-complex Assay (HPLC)','true','12','13','VIT-B','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'VIT-B'),
 ('HPLC-VIT-C','Ascorbic Acid Assay (HPLC)','true','12','13','VIT-C','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'VIT-C'),
 ('HPLC-VIT-D3','Cholecalciferol Assay (HPLC, normal phase)','true','12','13','VIT-D3','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,'VIT-D3'),
 ('Identification','Identification','true','6','7',NULL,'false',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,NULL),
 ('Metals ICP','Metals ICP','true','3','3','FERO-METALS','false',NULL,NULL,NULL,NULL,NULL,'110.0000','90.0000','0','120.0000','80.0000','24','0.999000','5','true','true','true','false','0','2',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,NULL),
 ('PH','PH','true','4','4',NULL,'false',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'1','1',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'0',NULL,NULL,NULL),
 ('Vit-C','Vitamin C assay','true','4','12','VIT-C-TITRATION','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'5.0000','1',NULL,NULL,NULL),
 ('Water soluble','Water soluble','true','11','12','WATERSOLUBLE','true',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'24',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,'2.0000','0',NULL,NULL,NULL)
) AS v("Code","DisplayName","IsActive","WorkflowType","EquationType","MethodAbbreviation","RequiresSystemSuitability","SstMaxRsdPercent","SstMaxTailingFactor","SstMinResolution","SstMinTheoreticalPlates","CalBlankMax","CalCheckRecoveryHighPercent","CalCheckRecoveryLowPercent","CalCorrelationType","CalIsRecoveryHighPercent","CalIsRecoveryLowPercent","CalMaxRunAgeHours","CalMinCorrelation","CalMinStandards","CalRequireBlank","CalRequireCcv","CalRequireIcv","CalRequireInternalStandard","CalibrationEntryMode","ReportedConcentrationBasis","EvaluationBasis","ReplicateCount","ConditionFields","UsesTare","DissolutionS1Offset","DissolutionS2MinOffset","DissolutionS3MaxBelowS2Min","DissolutionS3MinOffset","DisintegrationMaxStage1Failures","DisintegrationMinPassTotal","DisintegrationStage1Units","DisintegrationStage2Units","WvCapsuleInnerPercent","WvCapsuleOuterPercent","WvCapsuleS1MaxForRetest","WvCapsuleS1MaxOutside","WvCapsuleS2ExtraUnits","WvCapsuleS2MaxOutside","WvTabletBand1MaxMg","WvTabletBand1Percent","WvTabletBand2MaxMg","WvTabletBand2Percent","WvTabletBand3Percent","WvTabletMaxOutside","WvUnitCount","HplcMaxPreparationRsdPercent","ResponseMode","CalInstrumentType","CalStandardLevelsMgPerL","_k_m")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "TestDefinitions" t WHERE t."Code" = v."Code");

------------------------------------------------------------------------------
-- 6b. Test analytes (elements / compounds per test)
------------------------------------------------------------------------------
INSERT INTO "TestAnalytes"
  ("TestDefinitionId","Element","WavelengthNm","View","LoqMgPerL","DisplayOrder","IsActive","SstMaxRsdPercent","SstMaxTailingFactor","SstMinResolution","SstMinTheoreticalPlates")
SELECT (SELECT t."Id" FROM "TestDefinitions" t WHERE t."Code" = v."_k_t"),
       v."Element",
       v."WavelengthNm",
       v."View",
       v."LoqMgPerL",
       v."DisplayOrder",
       v."IsActive",
       v."SstMaxRsdPercent",
       v."SstMaxTailingFactor",
       v."SstMinResolution",
       v."SstMinTheoreticalPlates"
FROM (VALUES
 ('thiamin'::character varying(100),'220.0000'::numeric(10,4),NULL::integer,NULL::numeric(18,6),'1'::integer,'true'::boolean,'2.0000'::numeric(18,4),'2.0000'::numeric(18,4),'1.5000'::numeric(18,4),'2000.0000'::numeric(18,4),'Water soluble'::text),
 ('Nicotinamide','220.0000',NULL,NULL,'2','true','2.0000','2.0000','1.5000','2000.0000','Water soluble')
) AS v("Element","WavelengthNm","View","LoqMgPerL","DisplayOrder","IsActive","SstMaxRsdPercent","SstMaxTailingFactor","SstMinResolution","SstMinTheoreticalPlates","_k_t")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "TestAnalytes" a JOIN "TestDefinitions" t ON t."Id"=a."TestDefinitionId" WHERE t."Code" = v."_k_t" AND a."Element" = v."Element" AND a."WavelengthNm" = v."WavelengthNm");

------------------------------------------------------------------------------
-- 6c. Test stage replicate counts
------------------------------------------------------------------------------
INSERT INTO "TestDefinitionStageReplicates"
  ("TestDefinitionId","Role","SampleReplicates")
SELECT (SELECT t."Id" FROM "TestDefinitions" t WHERE t."Code" = v."_k_t"),
       v."Role",
       v."SampleReplicates"
FROM (VALUES
 ('1'::integer,'3'::integer,'HPLC-AMOX'::text),
 ('3','2','HPLC-AMOX'),
 ('1','3','HPLC-APAP'),
 ('3','2','HPLC-APAP'),
 ('1','3','HPLC-APAP-CAF'),
 ('3','2','HPLC-APAP-CAF'),
 ('1','3','HPLC-B12'),
 ('3','2','HPLC-B12'),
 ('1','3','HPLC-DICLO'),
 ('3','2','HPLC-DICLO'),
 ('1','3','HPLC-FOLIC'),
 ('3','2','HPLC-FOLIC'),
 ('1','3','HPLC-IBU'),
 ('3','2','HPLC-IBU'),
 ('1','3','HPLC-VIT-B'),
 ('3','2','HPLC-VIT-B'),
 ('1','3','HPLC-VIT-C'),
 ('3','2','HPLC-VIT-C'),
 ('1','3','HPLC-VIT-D3'),
 ('3','2','HPLC-VIT-D3'),
 ('1','1','Water soluble'),
 ('3','2','Water soluble')
) AS v("Role","SampleReplicates","_k_t")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "TestDefinitionStageReplicates" x JOIN "TestDefinitions" t ON t."Id"=x."TestDefinitionId" WHERE t."Code" = v."_k_t" AND x."Role" = v."Role");

------------------------------------------------------------------------------
-- 7. Items (finished products) that carry physicochemical tests
-- Natural key: Code (unique index).
------------------------------------------------------------------------------
INSERT INTO "Items"
  ("Name","Code","Category","SopNumber","IsActive")
SELECT v."Name",
       v."Code",
       v."Category",
       v."SopNumber",
       v."IsActive"
FROM (VALUES
 ('Amoxicillin Capsules 500 mg'::character varying(200),'AMOX-500'::character varying(50),'0'::integer,'USP-NF'::text,'true'::boolean),
 ('Acetaminophen Tablets 500 mg','APAP-500','0','USP-NF','true'),
 ('Acetaminophen and Caffeine Tablets 500 mg/65 mg','APAP-CAF-500','0','USP-NF','true'),
 ('Cyanocobalamin Tablets 500 mcg','B12-500','0','USP-NF','true'),
 ('Diclofenac Sodium Delayed-Release Tablets 50 mg','DICLO-50','0','USP-NF','true'),
 ('Folic Acid Tablets 5 mg','FOLIC-5','0','USP-NF','true'),
 ('Ibuprofen Tablets 400 mg','IBU-400','0','USP-NF','true'),
 ('B-complex Tablets (B1/B2/B3/B6)','VITB-CPX','0','USP-NF','true'),
 ('Ascorbic Acid Tablets 500 mg','VITC-500','0','USP-NF','true'),
 ('Cholecalciferol (Vitamin D3) Drug Substance','VITD3-API','0','USP-NF','true')
) AS v("Name","Code","Category","SopNumber","IsActive")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "Items" i WHERE i."Code" = v."Code");

------------------------------------------------------------------------------
-- 7b. Item -> physicochemical test assignments
------------------------------------------------------------------------------
INSERT INTO "SampleTests"
  ("ItemId","TestCode","DisplayName")
SELECT (SELECT i."Id" FROM "Items" i WHERE i."Code" = v."_k_i"),
       v."TestCode",
       v."DisplayName"
FROM (VALUES
 ('HPLC-AMOX'::text,'Amoxicillin Capsules Assay (HPLC)'::text,'AMOX-500'::text),
 ('HPLC-APAP','Acetaminophen Tablets Assay (HPLC)','APAP-500'),
 ('HPLC-APAP-CAF','Acetaminophen and Caffeine Tablets Assay (HPLC)','APAP-CAF-500'),
 ('HPLC-B12','Cyanocobalamin Assay (HPLC)','B12-500'),
 ('HPLC-DICLO','Diclofenac Sodium DR Tablets Assay (HPLC)','DICLO-50'),
 ('HPLC-FOLIC','Folic Acid Tablets Assay (HPLC)','FOLIC-5'),
 ('HPLC-IBU','Ibuprofen Tablets Assay (HPLC)','IBU-400'),
 ('HPLC-VIT-B','B-complex Assay (HPLC)','VITB-CPX'),
 ('HPLC-VIT-C','Ascorbic Acid Assay (HPLC)','VITC-500'),
 ('HPLC-VIT-D3','Cholecalciferol Assay (HPLC, normal phase)','VITD3-API')
) AS v("TestCode","DisplayName","_k_i")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "SampleTests" st JOIN "Items" i ON i."Id"=st."ItemId" WHERE i."Code" = v."_k_i" AND st."TestCode" = v."TestCode");

------------------------------------------------------------------------------
-- 7c. Specifications
-- Natural key: item Code + TestCode + ParameterName (unique index). Analyte links resolved by method abbreviation + analyte name.
------------------------------------------------------------------------------
INSERT INTO "Specifications"
  ("ItemId","TestCode","AlertLimit","ActionLimit","SpecLimit","Unit","DilutionFactor","DisplayOrder","ExpectedResultText","ExpectedState","LimitType","LowerInclusive","LowerLimit","ParameterName","ReferenceStandard","SampleQuantity","SampleQuantityUnit","Target","Tolerance","ToleranceMode","UpperInclusive","UpperLimit","ConversionFactor","LabelClaim","LabelClaimUnit","ResultBasis","SampleMatrix","TestAnalyteId","DosageForm","HplcMethodAnalyteId")
SELECT (SELECT i."Id" FROM "Items" i WHERE i."Code" = v."_k_i"),
       v."TestCode",
       v."AlertLimit",
       v."ActionLimit",
       v."SpecLimit",
       v."Unit",
       v."DilutionFactor",
       v."DisplayOrder",
       v."ExpectedResultText",
       v."ExpectedState",
       v."LimitType",
       v."LowerInclusive",
       v."LowerLimit",
       v."ParameterName",
       v."ReferenceStandard",
       v."SampleQuantity",
       v."SampleQuantityUnit",
       v."Target",
       v."Tolerance",
       v."ToleranceMode",
       v."UpperInclusive",
       v."UpperLimit",
       v."ConversionFactor",
       v."LabelClaim",
       v."LabelClaimUnit",
       v."ResultBasis",
       v."SampleMatrix",
       (SELECT a."Id" FROM "TestAnalytes" a JOIN "TestDefinitions" t ON t."Id"=a."TestDefinitionId" WHERE t."Code" = v."TestCode" AND a."Element" = v."_k_ta" AND a."WavelengthNm" = v."_k_taw"::numeric),
       v."DosageForm",
       (SELECT a."Id" FROM "HplcMethodAnalytes" a JOIN "HplcMethods" m ON m."Id"=a."HplcMethodId" WHERE m."SectionId" = c.sec AND m."Abbreviation" = v."_k_m" AND a."Name" = v."_k_ha")
FROM (VALUES
 ('HPLC-AMOX'::text,''::text,''::text,'90-120'::text,'%'::text,NULL::numeric,'0'::integer,NULL::character varying(1000),NULL::integer,'0'::integer,'true'::boolean,'90.000000'::numeric(18,6),'Amoxicillin'::character varying(150),NULL::character varying(100),NULL::numeric(18,6),NULL::character varying(20),NULL::numeric(18,6),NULL::numeric(18,6),NULL::integer,'true'::boolean,'120.000000'::numeric(18,6),'1.000000'::numeric(18,6),NULL::numeric(18,6),NULL::character varying(20),'2'::integer,NULL::integer,NULL::integer,'AMOX-500'::text,'AMOX'::text,'Amoxicillin'::text,NULL::text,NULL::text),
 ('HPLC-AMOX','','','450-600','mg',NULL,'1',NULL,NULL,'0','true','450.000000','Amoxicillin (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','600.000000','1.000000','500.000000','mg','1',NULL,NULL,'AMOX-500','AMOX','Amoxicillin',NULL,NULL),
 ('HPLC-APAP','','','90-110','%',NULL,'0',NULL,NULL,'0','true','90.000000','Acetaminophen',NULL,NULL,NULL,NULL,NULL,NULL,'true','110.000000','1.000000',NULL,NULL,'2',NULL,NULL,'APAP-500','APAP','Acetaminophen',NULL,NULL),
 ('HPLC-APAP','','','450-550','mg',NULL,'1',NULL,NULL,'0','true','450.000000','Acetaminophen (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','550.000000','1.000000','500.000000','mg','1',NULL,NULL,'APAP-500','APAP','Acetaminophen',NULL,NULL),
 ('HPLC-APAP-CAF','','','90-110','%',NULL,'0',NULL,NULL,'0','true','90.000000','Acetaminophen',NULL,NULL,NULL,NULL,NULL,NULL,'true','110.000000','1.000000',NULL,NULL,'2',NULL,NULL,'APAP-CAF-500','APAP-CAF','Acetaminophen',NULL,NULL),
 ('HPLC-APAP-CAF','','','450-550','mg',NULL,'1',NULL,NULL,'0','true','450.000000','Acetaminophen (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','550.000000','1.000000','500.000000','mg','1',NULL,NULL,'APAP-CAF-500','APAP-CAF','Acetaminophen',NULL,NULL),
 ('HPLC-APAP-CAF','','','90-110','%',NULL,'2',NULL,NULL,'0','true','90.000000','Caffeine',NULL,NULL,NULL,NULL,NULL,NULL,'true','110.000000','1.000000',NULL,NULL,'2',NULL,NULL,'APAP-CAF-500','APAP-CAF','Caffeine',NULL,NULL),
 ('HPLC-APAP-CAF','','','58.5-71.5','mg',NULL,'3',NULL,NULL,'0','true','58.500000','Caffeine (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','71.500000','1.000000','65.000000','mg','1',NULL,NULL,'APAP-CAF-500','APAP-CAF','Caffeine',NULL,NULL),
 ('HPLC-B12','','','90-150','%',NULL,'0',NULL,NULL,'0','true','90.000000','Cyanocobalamin',NULL,NULL,NULL,NULL,NULL,NULL,'true','150.000000','1.000000',NULL,NULL,'2',NULL,NULL,'B12-500','B12','Cyanocobalamin',NULL,NULL),
 ('HPLC-B12','','','450-750','mcg',NULL,'1',NULL,NULL,'0','true','450.000000','Cyanocobalamin (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','750.000000','1.000000','500.000000','mcg','1',NULL,NULL,'B12-500','B12','Cyanocobalamin',NULL,NULL),
 ('HPLC-DICLO','','','90-110','%',NULL,'0',NULL,NULL,'0','true','90.000000','Diclofenac sodium',NULL,NULL,NULL,NULL,NULL,NULL,'true','110.000000','1.000000',NULL,NULL,'2',NULL,NULL,'DICLO-50','DICLO','Diclofenac sodium',NULL,NULL),
 ('HPLC-DICLO','','','45-55','mg',NULL,'1',NULL,NULL,'0','true','45.000000','Diclofenac sodium (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','55.000000','1.000000','50.000000','mg','1',NULL,NULL,'DICLO-50','DICLO','Diclofenac sodium',NULL,NULL),
 ('HPLC-FOLIC','','','90-115','%',NULL,'0',NULL,NULL,'0','true','90.000000','Folic acid',NULL,NULL,NULL,NULL,NULL,NULL,'true','115.000000','1.000000',NULL,NULL,'2',NULL,NULL,'FOLIC-5','FOLIC','Folic acid',NULL,NULL),
 ('HPLC-FOLIC','','','4.5-5.75','mg',NULL,'1',NULL,NULL,'0','true','4.500000','Folic acid (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','5.750000','1.000000','5.000000','mg','1',NULL,NULL,'FOLIC-5','FOLIC','Folic acid',NULL,NULL),
 ('HPLC-IBU','','','90-110','%',NULL,'0',NULL,NULL,'0','true','90.000000','Ibuprofen',NULL,NULL,NULL,NULL,NULL,NULL,'true','110.000000','1.000000',NULL,NULL,'2',NULL,NULL,'IBU-400','IBU','Ibuprofen',NULL,NULL),
 ('HPLC-IBU','','','360-440','mg',NULL,'1',NULL,NULL,'0','true','360.000000','Ibuprofen (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','440.000000','1.000000','400.000000','mg','1',NULL,NULL,'IBU-400','IBU','Ibuprofen',NULL,NULL),
 ('HPLC-VIT-B','','','90-150','%',NULL,'0',NULL,NULL,'0','true','90.000000','Niacinamide',NULL,NULL,NULL,NULL,NULL,NULL,'true','150.000000','1.000000',NULL,NULL,'2',NULL,NULL,'VITB-CPX','VIT-B','Niacinamide',NULL,NULL),
 ('HPLC-VIT-B','','','90-150','mg',NULL,'1',NULL,NULL,'0','true','90.000000','Niacinamide (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','150.000000','1.000000','100.000000','mg','1',NULL,NULL,'VITB-CPX','VIT-B','Niacinamide',NULL,NULL),
 ('HPLC-VIT-B','','','90-150','%',NULL,'2',NULL,NULL,'0','true','90.000000','Pyridoxine HCl',NULL,NULL,NULL,NULL,NULL,NULL,'true','150.000000','1.000000',NULL,NULL,'2',NULL,NULL,'VITB-CPX','VIT-B','Pyridoxine HCl',NULL,NULL),
 ('HPLC-VIT-B','','','22.5-37.5','mg',NULL,'3',NULL,NULL,'0','true','22.500000','Pyridoxine HCl (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','37.500000','1.000000','25.000000','mg','1',NULL,NULL,'VITB-CPX','VIT-B','Pyridoxine HCl',NULL,NULL),
 ('HPLC-VIT-B','','','90-150','%',NULL,'4',NULL,NULL,'0','true','90.000000','Riboflavin',NULL,NULL,NULL,NULL,NULL,NULL,'true','150.000000','1.000000',NULL,NULL,'2',NULL,NULL,'VITB-CPX','VIT-B','Riboflavin',NULL,NULL),
 ('HPLC-VIT-B','','','22.5-37.5','mg',NULL,'5',NULL,NULL,'0','true','22.500000','Riboflavin (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','37.500000','1.000000','25.000000','mg','1',NULL,NULL,'VITB-CPX','VIT-B','Riboflavin',NULL,NULL),
 ('HPLC-VIT-B','','','90-150','%',NULL,'6',NULL,NULL,'0','true','90.000000','Thiamine HCl',NULL,NULL,NULL,NULL,NULL,NULL,'true','150.000000','1.000000',NULL,NULL,'2',NULL,NULL,'VITB-CPX','VIT-B','Thiamine HCl',NULL,NULL),
 ('HPLC-VIT-B','','','22.5-37.5','mg',NULL,'7',NULL,NULL,'0','true','22.500000','Thiamine HCl (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','37.500000','1.000000','25.000000','mg','1',NULL,NULL,'VITB-CPX','VIT-B','Thiamine HCl',NULL,NULL),
 ('HPLC-VIT-C','','','90-110','%',NULL,'0',NULL,NULL,'0','true','90.000000','Ascorbic acid',NULL,NULL,NULL,NULL,NULL,NULL,'true','110.000000','1.000000',NULL,NULL,'2',NULL,NULL,'VITC-500','VIT-C','Ascorbic acid',NULL,NULL),
 ('HPLC-VIT-C','','','450-550','mg',NULL,'1',NULL,NULL,'0','true','450.000000','Ascorbic acid (per unit)',NULL,NULL,NULL,NULL,NULL,NULL,'true','550.000000','1.000000','500.000000','mg','1',NULL,NULL,'VITC-500','VIT-C','Ascorbic acid',NULL,NULL),
 ('HPLC-VIT-D3','','','97-103','%',NULL,'0',NULL,NULL,'0','true','97.000000','Cholecalciferol',NULL,NULL,NULL,NULL,NULL,NULL,'true','103.000000','1.000000',NULL,NULL,'2',NULL,NULL,'VITD3-API','VIT-D3','Cholecalciferol',NULL,NULL)
) AS v("TestCode","AlertLimit","ActionLimit","SpecLimit","Unit","DilutionFactor","DisplayOrder","ExpectedResultText","ExpectedState","LimitType","LowerInclusive","LowerLimit","ParameterName","ReferenceStandard","SampleQuantity","SampleQuantityUnit","Target","Tolerance","ToleranceMode","UpperInclusive","UpperLimit","ConversionFactor","LabelClaim","LabelClaimUnit","ResultBasis","SampleMatrix","DosageForm","_k_i","_k_m","_k_ha","_k_ta","_k_taw")
CROSS JOIN _ctx c
WHERE NOT EXISTS (SELECT 1 FROM "Specifications" x JOIN "Items" i ON i."Id"=x."ItemId" WHERE i."Code" = v."_k_i" AND x."TestCode" = v."TestCode" AND x."ParameterName" = v."ParameterName");

------------------------------------------------------------------------------
-- 7d. SpecificationStages: no rows exist in LIMSV2 for in-scope specifications (nothing to copy).
------------------------------------------------------------------------------
COMMIT;
