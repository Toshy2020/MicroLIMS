import { TestOrderSummaryDetail } from "./types/sampleSummaryTypes";

// The Certificate of Analysis is decided on the server
// (CertificateOfAnalysisBuilder) and arrives on the summary as
// `certificate`: which tests and locations it shows, whether each result
// conforms, and the conclusion. These types mirror it. Nothing here judges
// a result - only the two layout helpers below remain.

export interface CoaQuantitativeCell {
  kind: "quantitative";
  alert: string | null;
  action: string | null;
  spec: string | null;
  result: string;
  conform: boolean;
}

export interface CoaQualitativeCell {
  kind: "qualitative";
  result: string;
  conform: boolean;
}

// null = this test has no result row at this location - rendered as a dash.
export type CoaCell = CoaQuantitativeCell | CoaQualitativeCell | null;

export interface CoaColumn {
  testOrderId: number;
  testCode: string;
  testDisplayName: string;
  isQuantitative: boolean;
  unit: string | null;
}

export interface CoaRow {
  locationKey: string;
  locationName: string;
  cells: CoaCell[]; // aligned 1:1 with CoaMatrix.columns
}

export interface CoaTestConclusion {
  testOrderId: number;
  testCode: string;
  testDisplayName: string;
  conforms: boolean;
  failingLocationNames: string[];
  unconfiguredLocationNames: string[];
  missingResultLocationNames: string[];
}

export interface CoaMatrix {
  columns: CoaColumn[];
  rows: CoaRow[];
  testConclusions: CoaTestConclusion[];
  overallComplies: boolean;
  totalTests: number;
  totalLocations: number;
  units: string[];
}

export interface CoaSimpleRow {
  testOrderId: number;
  testCode: string;
  testDisplayName: string;
  specification: string | null;
  result: string;
  analystName: string | null;
  analystAt: string | null;
  conform: boolean;
  limitsNotConfigured: boolean;
  noResult: boolean;
}

export interface CoaSimpleResult {
  rows: CoaSimpleRow[];
  overallComplies: boolean;
}

export interface CoaScope {
  matrix: CoaMatrix | null;
  simple: CoaSimpleResult | null;
  complies: boolean;
  conclusionText: string;
  resultDate: string | null;
}

export interface CertificateOfAnalysis {
  sample: CoaScope;
  sections: { sectionId: number; sectionName: string; scope: CoaScope }[];
}

export function sectionScope(certificate: CertificateOfAnalysis, sectionId: number): CoaScope | null {
  return certificate.sections.find((s) => s.sectionId === sectionId)?.scope ?? null;
}

// Filters test orders for a single laboratory section.
export function filterTestOrdersBySection(
  testOrders: TestOrderSummaryDetail[],
  sectionId: number
): TestOrderSummaryDetail[] {
  return testOrders.filter((t) => t.sectionId === sectionId);
}

export interface SectionTestOrdersGroup {
  sectionId: number;
  sectionName: string;
  testOrders: TestOrderSummaryDetail[];
}

// Groups test orders by laboratory section, preserving the order of the
// provided sections list (or grouping by sectionId/sectionName on the test orders).
export function groupTestOrdersBySection(
  testOrders: TestOrderSummaryDetail[],
  sections?: { sectionId: number; sectionName: string }[]
): SectionTestOrdersGroup[] {
  if (sections && sections.length > 0) {
    return sections
      .map((sec) => ({
        sectionId: sec.sectionId,
        sectionName: sec.sectionName,
        testOrders: testOrders.filter((t) => t.sectionId === sec.sectionId)
      }))
      .filter((g) => g.testOrders.length > 0);
  }

  const map = new Map<number, SectionTestOrdersGroup>();
  for (const t of testOrders) {
    const secId = t.sectionId ?? 0;
    const secName = t.sectionName ?? "General";
    let group = map.get(secId);
    if (!group) {
      group = { sectionId: secId, sectionName: secName, testOrders: [] };
      map.set(secId, group);
    }
    group.testOrders.push(t);
  }
  return Array.from(map.values());
}

