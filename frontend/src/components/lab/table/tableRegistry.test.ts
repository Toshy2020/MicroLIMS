import { describe, expect, it } from "vitest";
import registryJson from "../../../tables/tableRegistry.json";
import { TABLES, getTable, type TableDefinition } from "./tableRegistry";

const pageSources = import.meta.glob<string>(
  ["/src/**/*.tsx", "!/src/**/*.test.tsx", "!/src/components/lab/RegisterTable.tsx"],
  { query: "?raw", eager: true, import: "default" }
);

const FILE_TO_TABLE_KEYS: Record<string, string[]> = {
  "/src/modules/auditReview/AuditTrailReviewDetailPage.tsx": ["auditReview.reviewEntries"],
  "/src/modules/auditReview/AuditTrailReviewsPage.tsx": ["auditReview.reviews"],
  "/src/modules/hplcWorkspace/history/HplcRunHistoryTable.tsx": ["hplc.runHistory"],
  "/src/modules/hplcWorkspace/run/HplcInstrumentWorkspace.tsx": ["hplc.mobilePhases"],
  "/src/modules/hplcWorkspace/samples/SampleAssignmentPanel.tsx": ["hplc.runSamples"],
  "/src/modules/hplcWorkspace/sst/SstValuesTable.tsx": ["hplc.sstValues"],
  "/src/modules/icpWorkspace/history/IcpRunHistoryTable.tsx": ["icp.runHistory"],
  "/src/modules/inventory/approvedLists/ApprovedMediaListPage.tsx": ["inventory.approvedMedia"],
  "/src/modules/inventory/equipment/EquipmentInventoryPage.tsx": ["inventory.equipment"],
  "/src/modules/inventory/equipment/components/ActiveEquipmentView.tsx": [
    "inventory.equipmentTraceability",
    "inventory.equipmentActivities",
    "inventory.equipmentActivityHistory",
  ],
  "/src/modules/inventory/materials/MaterialsPage.tsx": ["inventory.materials"],
  "/src/modules/laboratoryConfiguration/cryovials/components/CryovialReviewTable.tsx": ["labConfig.cryovials"],
  "/src/modules/laboratoryConfiguration/labelFormats/LabelFormatsPage.tsx": ["labConfig.labelFormats"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/ChromatographyColumnsPage.tsx": ["labConfig.chromatographyColumns"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/EquationTypesPage.tsx": ["labConfig.equationTypes"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/FpInstrumentsPage.tsx": ["labConfig.fpInstruments"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/MaterialCategoriesDialog.tsx": ["labConfig.materialCategories"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/MaterialMasterPage.tsx": ["labConfig.materialMaster"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/OrganismsPage.tsx": ["labConfig.organisms"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/ReferenceOrganismsSection.tsx": ["labConfig.referenceOrganisms"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/SolutionMasterPage.tsx": ["labConfig.solutionMaster"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/TestMasterPage.tsx": ["labConfig.testMaster"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/WorkingStandardCatalogSection.tsx": ["labConfig.workingStandardCatalog"],
  "/src/modules/laboratoryConfiguration/masterDataSimple/hplcMethod/HplcMethodTable.tsx": ["labConfig.hplcMethods"],
  "/src/modules/laboratoryConfiguration/masterDataWorkbook/MasterDataWorkbookPage.tsx": ["labConfig.workbookImports"],
  "/src/modules/laboratoryConfiguration/media/components/MediaLotRegisterTable.tsx": ["labConfig.mediaLots"],
  "/src/modules/reports/components/MediaGptResultsTable.tsx": ["reports.mediaGpt"],
  "/src/modules/reports/components/ReferenceStrainResultsTable.tsx": ["reports.referenceStrains"],
  "/src/modules/reports/components/ReportResultsTable.tsx": ["reports.results"],
  "/src/modules/receiving/components/SampleRegisterTable.tsx": ["samples.workspace.micro", "samples.workspace.fp", "samples.receiving"],
  "/src/modules/solutionPreparation/PreparationListPage.tsx": ["solutionPrep.preparations"],
  "/src/modules/testingWorkspace/GravimetricPanel.tsx": ["testing.gravimetricWeights"],
  "/src/modules/testingWorkspace/TitrationPanel.tsx": ["testing.titrationReadings"],
};

describe("tableRegistry", () => {
  it("registry: every key is unique, has an idColumn that exists in columns and is defaultVisible", () => {
    expect(Array.isArray(registryJson)).toBe(true);
    const seenKeys = new Set<string>();
    for (const entry of registryJson as TableDefinition[]) {
      expect(entry.key).toMatch(/^[a-zA-Z0-9]+(\.[a-zA-Z0-9]+)+$/);
      expect(seenKeys.has(entry.key)).toBe(false);
      seenKeys.add(entry.key);

      expect(entry.columns.length).toBeGreaterThan(0);
      const idCol = entry.columns.find((c) => c.key === entry.idColumn);
      expect(idCol, `Table ${entry.key} idColumn "${entry.idColumn}" must exist in columns`).toBeDefined();
      expect(idCol?.defaultVisible, `Table ${entry.key} idColumn "${entry.idColumn}" must be defaultVisible`).toBe(true);

      expect(TABLES[entry.key]).toEqual(entry);
      expect(getTable(entry.key)).toEqual(entry);
    }
  });

  it("getTable throws on an unknown key", () => {
    expect(() => getTable("nonexistent.table")).toThrow(/unknown table/i);
  });

  it("every page rendering <RegisterTable has a registry entry", () => {
    const registerFiles = Object.entries(pageSources)
      .filter(([, source]) => /<RegisterTable\b/.test(source))
      .map(([path]) => path)
      .sort();

    expect(registerFiles.length).toBe(Object.keys(FILE_TO_TABLE_KEYS).length);
    for (const file of registerFiles) {
      const keys = FILE_TO_TABLE_KEYS[file];
      expect(keys, `Missing registry mapping for ${file}`).toBeDefined();
      for (const key of keys) {
        expect(TABLES[key], `Missing TABLES entry "${key}" for ${file}`).toBeDefined();
      }
    }

    const mappedKeys = new Set(Object.values(FILE_TO_TABLE_KEYS).flat());
    for (const key of Object.keys(TABLES)) {
      expect(mappedKeys.has(key), `Unmapped registry key "${key}"`).toBe(true);
    }
  });
});
