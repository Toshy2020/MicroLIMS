export interface WorkbookSheetSummary { sheet: string; created: number; unchanged: number; errors: number }
export interface WorkbookRowError { sheet: string; row: number; column: string | null; message: string }
export interface WorkbookReport { sheets: WorkbookSheetSummary[]; errors: WorkbookRowError[]; hasErrors: boolean }

export interface MasterDataImport {
  id: number;
  fileName: string;
  fileSha256: string;
  importedBy: string;
  importedAt: string;
  comment: string | null;
  status: "Loaded" | "Verified";
  verifiedBy: string | null;
  verifiedAt: string | null;
  sheets: WorkbookSheetSummary[];
  itemsWithoutSpecifications: string[];
}

export interface MasterDataImportResult { committed: boolean; report: WorkbookReport; import: MasterDataImport | null }

// Load is offered only after a dry run with no errors; the server checks
// again on load, so this is convenience, not a rule.
export const canLoad = (report: WorkbookReport | null): boolean => !!report && !report.hasErrors;
