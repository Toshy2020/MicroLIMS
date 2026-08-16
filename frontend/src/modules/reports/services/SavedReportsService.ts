import { GeneratedReport, SavedReportConfiguration } from "../types/reportingTypes";

/*
 * MOCK REPOSITORY - Saved Reports & Configurations Service
 * Clear separation between:
 *  - Section A: Generated Reports (Controlled generated report artifacts)
 *  - Section B: Saved Report Configurations (Reusable report criteria templates)
 * To be replaced by backend persistence endpoints when deployed.
 */

let mockGeneratedReports: GeneratedReport[] = [
  {
    id: "REP-2026-00125",
    name: "Monthly Microbiology Results - Jul 2026",
    type: "Microbiology",
    purpose: "Controlled Report",
    period: "01-Jul-2026 — 31-Jul-2026",
    generatedOn: "15 Aug 2026 10:30",
    generatedBy: "Amal Hamdy",
    generatedByUserId: 101,
    status: "Final",
    format: "PDF",
    criteriaJson: JSON.stringify({ category: "FinishedProduct", reportType: "Microbiology Results Report" }),
    sourceRecordIds: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
    templateId: "tmpl-micro-std",
    version: "v1.0",
    auditTrailRef: "AUD-LOG-9921",
    sizeBytes: 142850
  },
  {
    id: "REP-2026-00124",
    name: "Water Monitoring Report - Jul 2026",
    type: "Water",
    purpose: "Controlled Report",
    period: "01-Jul-2026 — 31-Jul-2026",
    generatedOn: "14 Aug 2026 16:20",
    generatedBy: "Ahmed Ali",
    generatedByUserId: 102,
    status: "Final",
    format: "PDF",
    criteriaJson: JSON.stringify({ category: "Water", reportType: "Water Monitoring Report" }),
    sourceRecordIds: [13, 14, 15, 16, 17, 18],
    templateId: "tmpl-water-sum",
    version: "v1.0",
    auditTrailRef: "AUD-LOG-9918",
    sizeBytes: 98400
  },
  {
    id: "REP-2026-00123",
    name: "Environmental Monitoring - Jul 2026",
    type: "Environmental",
    purpose: "Operational Report",
    period: "01-Jul-2026 — 31-Jul-2026",
    generatedOn: "13 Aug 2026 09:15",
    generatedBy: "Amal Hamdy",
    generatedByUserId: 101,
    status: "Final",
    format: "PDF",
    criteriaJson: JSON.stringify({ category: "EnvironmentalMonitoring", reportType: "Environmental Monitoring Report" }),
    sourceRecordIds: [19, 20, 21, 22],
    templateId: "tmpl-em-sum",
    version: "v1.0",
    auditTrailRef: "AUD-LOG-9910",
    sizeBytes: 112300
  },
  {
    id: "REP-2026-00122",
    name: "After Cleaning Report - Jul 2026",
    type: "After Cleaning",
    purpose: "Operational Report",
    period: "01-Jul-2026 — 31-Jul-2026",
    generatedOn: "12 Aug 2026 14:40",
    generatedBy: "Sara Mohamed",
    generatedByUserId: 103,
    status: "Final",
    format: "PDF",
    criteriaJson: JSON.stringify({ category: "AfterCleaning", reportType: "After Cleaning Report" }),
    sourceRecordIds: [23, 24, 25],
    templateId: "tmpl-micro-std",
    version: "v1.0",
    auditTrailRef: "AUD-LOG-9904",
    sizeBytes: 84600
  }
];

let mockConfigurations: SavedReportConfiguration[] = [
  {
    id: "CFG-001",
    name: "Monthly Finished Product Microbiology",
    reportType: "Microbiology Results Report",
    purpose: "Controlled Report",
    categories: ["FinishedProduct"],
    criteria: { reportType: "Microbiology Results Report", category: "FinishedProduct", groupBy: "Product" },
    options: { includeSpecifications: true, includeLimits: true, includeSignatures: true },
    lastModified: "10 Aug 2026",
    modifiedBy: "Amal Hamdy",
    modifiedByUserId: 101,
    status: "Active"
  },
  {
    id: "CFG-002",
    name: "Water Monitoring Monthly",
    reportType: "Water Monitoring Report",
    purpose: "Operational Report",
    categories: ["Water"],
    criteria: { reportType: "Water Monitoring Report", category: "Water", groupBy: "Location" },
    options: { includeSpecifications: true, includeLimits: true },
    lastModified: "08 Aug 2026",
    modifiedBy: "Ahmed Ali",
    modifiedByUserId: 102,
    status: "Active"
  },
  {
    id: "CFG-003",
    name: "Environmental Monitoring - Area D",
    reportType: "Environmental Monitoring Report",
    purpose: "Operational Report",
    categories: ["EnvironmentalMonitoring"],
    criteria: { reportType: "Environmental Monitoring Report", category: "EnvironmentalMonitoring", groupBy: "Location" },
    options: { includeSpecifications: true },
    lastModified: "07 Aug 2026",
    modifiedBy: "Amal Hamdy",
    modifiedByUserId: 101,
    status: "Active"
  },
  {
    id: "CFG-004",
    name: "After Cleaning - Production Areas",
    reportType: "After Cleaning Report",
    purpose: "Operational Report",
    categories: ["AfterCleaning"],
    criteria: { reportType: "After Cleaning Report", category: "AfterCleaning", groupBy: "Location" },
    options: { includeSpecifications: true },
    lastModified: "05 Aug 2026",
    modifiedBy: "Sara Mohamed",
    modifiedByUserId: 103,
    status: "Active"
  }
];

export const SavedReportsService = {
  async getGeneratedReports(): Promise<GeneratedReport[]> {
    return [...mockGeneratedReports];
  },

  async getReportMetadata(id: string): Promise<GeneratedReport | null> {
    return mockGeneratedReports.find((r) => r.id === id) ?? null;
  },

  async getConfigurations(): Promise<SavedReportConfiguration[]> {
    return [...mockConfigurations];
  },

  async saveConfiguration(config: Omit<SavedReportConfiguration, "id" | "lastModified">): Promise<SavedReportConfiguration> {
    const newConfig: SavedReportConfiguration = {
      ...config,
      id: `CFG-${String(mockConfigurations.length + 1).padStart(3, "0")}`,
      lastModified: new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" })
    };
    mockConfigurations = [newConfig, ...mockConfigurations];
    return newConfig;
  },

  async duplicateConfiguration(id: string, currentUserName: string, currentUserId: number): Promise<SavedReportConfiguration | null> {
    const source = mockConfigurations.find((c) => c.id === id);
    if (!source) return null;
    const clone: SavedReportConfiguration = {
      ...source,
      id: `CFG-${String(mockConfigurations.length + 1).padStart(3, "0")}`,
      name: `${source.name} (Copy)`,
      modifiedBy: currentUserName,
      modifiedByUserId: currentUserId,
      lastModified: new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" })
    };
    mockConfigurations = [clone, ...mockConfigurations];
    return clone;
  },

  async toggleConfigurationStatus(id: string): Promise<SavedReportConfiguration | null> {
    const cfg = mockConfigurations.find((c) => c.id === id);
    if (!cfg) return null;
    cfg.status = cfg.status === "Active" ? "Inactive" : "Active";
    return { ...cfg };
  },

  async deleteConfiguration(id: string): Promise<boolean> {
    mockConfigurations = mockConfigurations.filter((c) => c.id !== id);
    return true;
  }
};
