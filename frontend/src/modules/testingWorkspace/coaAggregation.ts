import { TestOrderSummaryDetail } from "./types/sampleSummaryTypes";
import { isQuantitative, unitFor } from "./SummaryMatrix";

// Same convention as SummaryMatrix/TestResultCards/SampleSummaryDialog -
// each file keeps its own copy of this rather than importing one, since
// it's a one-line, unambiguous rule and every other conform check in the
// app already does the same.
const CONFORMING_STATUSES = new Set(["WithinLimits", "Absent"]);
const isConforming = (status: string | null) => !status || CONFORMING_STATUSES.has(status);

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

// null = this test has no recorded result at this location (e.g. a
// location that only some of the sample's tests were run at) - rendered
// as a dash, excluded from conclusion math, same as SummaryMatrix's
// missing-location handling.
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
  conforms: boolean;
  failingLocationNames: string[];
  unconfiguredLocationNames: string[];
}

export interface CoaMatrix {
  columns: CoaColumn[];
  rows: CoaRow[];
  testConclusions: CoaTestConclusion[];
  overallComplies: boolean;
  totalTests: number;
  totalLocations: number;
  units: string[]; // distinct units in use among quantitative columns, for the footnote
}

// Reduces a sample's full test/location data down to the location x test
// grid the COA needs. Returns null when the sample has no located tests
// (Finished Product / plain single-value tests) - COA v1 is scoped to
// Water/EM/After Cleaning samples, where every test order carries a
// locations[] array. This same null check is what the "View COA" entry
// point uses to decide whether to show itself.
export function buildCoaMatrix(testOrders: TestOrderSummaryDetail[]): CoaMatrix | null {
  // A TestCode can appear more than once here - most commonly when a
  // NewSampleRequest OOS retest sends the same test to two different
  // samples and both get pulled through into this sample's effective
  // results (SampleSummaryService.ResolveEffectiveTestOrdersAsync). The
  // COA is a single controlled document, not an internal audit trail, so
  // it shows exactly one column per TestCode: whichever carries an
  // out-of-specification location result (the failure that actually
  // matters), or the first one otherwise.
  const dedupedByTestCode = new Map<string, TestOrderSummaryDetail>();
  for (const t of testOrders) {
    if (t.locations.length === 0 || t.isSuperseded) continue;
    const existing = dedupedByTestCode.get(t.testCode);
    if (!existing) {
      dedupedByTestCode.set(t.testCode, t);
      continue;
    }
    const existingFails = existing.locations.some((l) => !isConforming(l.status));
    const candidateFails = t.locations.some((l) => !isConforming(l.status));
    if (candidateFails && !existingFails) dedupedByTestCode.set(t.testCode, t);
  }

  // TAMC always leads the matrix, right after Location - a stable sort
  // (guaranteed by the spec since ES2019) so every other test keeps its
  // original relative order.
  const locatedTests = Array.from(dedupedByTestCode.values())
    .sort((a, b) => Number(!a.testCode.toUpperCase().startsWith("TAMC")) - Number(!b.testCode.toUpperCase().startsWith("TAMC")));
  if (locatedTests.length === 0) return null;

  const columns: CoaColumn[] = locatedTests.map((t) => ({
    testOrderId: t.testOrderId,
    testCode: t.testCode,
    testDisplayName: t.testDisplayName,
    isQuantitative: isQuantitative(t),
    unit: isQuantitative(t) ? unitFor(t) : null
  }));

  // Rows = distinct physical locations, first-seen order across tests -
  // same rule SummaryMatrix uses, so the two views never disagree on row
  // identity or ordering.
  const keysInOrder: string[] = [];
  const namesByKey = new Map<string, string>();
  for (const t of locatedTests) {
    for (const l of t.locations) {
      if (!namesByKey.has(l.locationKey)) {
        namesByKey.set(l.locationKey, l.locationName);
        keysInOrder.push(l.locationKey);
      }
    }
  }

  let overallComplies = true;
  let totalLocations = 0;
  const failingLocationsByTest = new Map<number, string[]>();
  const unconfiguredLocationsByTest = new Map<number, string[]>();
  const sawAnyResultByTest = new Map<number, boolean>();

  const rows: CoaRow[] = keysInOrder.map((locationKey) => {
    totalLocations++;
    const cells: CoaCell[] = locatedTests.map((t, i): CoaCell => {
      const loc = t.locations.find((l) => l.locationKey === locationKey);
      if (!loc) return null;

      sawAnyResultByTest.set(t.testOrderId, true);
      const conform = isConforming(loc.status);
      if (!conform) {
        overallComplies = false;
        const locName = namesByKey.get(locationKey) ?? locationKey;
        if (loc.status === "LimitsNotConfigured") {
          const existingUnconf = unconfiguredLocationsByTest.get(t.testOrderId) ?? [];
          existingUnconf.push(locName);
          unconfiguredLocationsByTest.set(t.testOrderId, existingUnconf);
        } else {
          const existing = failingLocationsByTest.get(t.testOrderId) ?? [];
          existing.push(locName);
          failingLocationsByTest.set(t.testOrderId, existing);
        }
      }

      if (columns[i].isQuantitative) {
        const value = loc.cfuResult ?? loc.calculatedResult;
        return {
          kind: "quantitative",
          alert: loc.alertLimit,
          action: loc.actionLimit,
          spec: loc.specLimit,
          result: value != null ? String(value) : "—",
          conform
        };
      }
      return {
        kind: "qualitative",
        result: conform ? "Absent" : loc.reportedResult ?? "—",
        conform
      };
    });
    return { locationKey, locationName: namesByKey.get(locationKey) ?? locationKey, cells };
  });

  const testConclusions: CoaTestConclusion[] = locatedTests.map((t) => {
    const fails = failingLocationsByTest.get(t.testOrderId) ?? [];
    const unconfigured = unconfiguredLocationsByTest.get(t.testOrderId) ?? [];
    return {
      testOrderId: t.testOrderId,
      testCode: t.testCode,
      conforms: fails.length === 0 && unconfigured.length === 0,
      failingLocationNames: fails,
      unconfiguredLocationNames: unconfigured
    };
  });

  const units = Array.from(
    new Set(columns.filter((c) => c.isQuantitative && c.unit).map((c) => c.unit as string))
  );

  return {
    columns,
    rows,
    testConclusions,
    overallComplies,
    totalTests: locatedTests.length,
    totalLocations,
    units
  };
}

// The single sentence for the mockup's green "Overall Conclusion" box.
export function buildOverallConclusionText(matrix: CoaMatrix): string {
  if (matrix.overallComplies) {
    return `This sample complies with the specified requirements. All ${matrix.totalTests} test${matrix.totalTests === 1 ? "" : "s"} conform across all ${matrix.totalLocations} sampling location${matrix.totalLocations === 1 ? "" : "s"}.`;
  }
  const fails = matrix.testConclusions
    .filter((c) => c.failingLocationNames.length > 0)
    .map((c) => `${c.testCode} at ${c.failingLocationNames.join(", ")}`);
  const unconfigured = matrix.testConclusions
    .filter((c) => c.unconfiguredLocationNames.length > 0)
    .map((c) => `${c.testCode} at ${c.unconfiguredLocationNames.join(", ")}`);

  if (fails.length > 0 && unconfigured.length > 0) {
    return `This sample does not comply with the specified requirements. Exceptions: ${fails.join("; ")}. Additionally, cannot certify — limits are not configured for: ${unconfigured.join("; ")}.`;
  }
  if (fails.length > 0) {
    return `This sample does not comply with the specified requirements. Exceptions: ${fails.join("; ")}.`;
  }
  return `Cannot certify — one or more results has no configured limit. Locations without configured limits: ${unconfigured.join("; ")}.`;
}

// --- Product / Raw Material / Packaging Material COA (non-located) ---
//
// Plain one-row-per-test table - no location dimension, so this is
// deliberately not a reduced/degenerate case of CoaMatrix (which is built
// around the location x test grid and its per-cell Alert/Action/Spec
// columns). Test/Specification/Sample Result/Analyst only.

export interface CoaSimpleRow {
  testOrderId: number;
  testCode: string;
  testDisplayName: string;
  // From TestOrderSummaryDetail.specificationText - Specifications table
  // keyed by (ItemId, TestCode), now populated for every test code
  // (quantitative and qualitative alike) since Part 0 removed the
  // CountTest-only restriction on configuring one. Null when nobody has
  // configured a Specification for this Item/TestCode pair yet.
  specification: string | null;
  result: string;
  analystName: string | null;
  analystAt: string | null;
  conform: boolean;
  limitsNotConfigured?: boolean;
}

export interface CoaSimpleResult {
  rows: CoaSimpleRow[];
  overallComplies: boolean;
}

// Mirrors buildCoaMatrix's null convention: null means this sample has no
// eligible (non-located, non-superseded) test orders to show, which
// SampleCoaPage treats as "COA not available yet" the same way it already
// does for a null CoaMatrix.
// TAMC, then TYMC, then E.coli, then everything else in its original order -
// a stable sort (guaranteed by the spec since ES2019) keeps the "any other"
// tests in their original relative order.
function coaSimpleRowRank(testCode: string): number {
  const code = testCode.toUpperCase();
  if (code.startsWith("TAMC")) return 0;
  if (code.startsWith("TYMC")) return 1;
  if (code.startsWith("E.COLI") || code.startsWith("ECOLI")) return 2;
  return 3;
}

export function buildCoaSimpleRows(testOrders: TestOrderSummaryDetail[]): CoaSimpleResult | null {
  const plainTests = testOrders
    .filter((t) => t.locations.length === 0 && !t.isSuperseded)
    .slice()
    .sort((a, b) => coaSimpleRowRank(a.testCode) - coaSimpleRowRank(b.testCode));
  if (plainTests.length === 0) return null;

  let overallComplies = true;

  const rows: CoaSimpleRow[] = plainTests.map((t) => {
    let result: string;
    let analystName: string | null;
    let analystAt: string | null;
    let conform: boolean;
    let isUnconfigured = false;

    if (isQuantitative(t)) {
      // Single-value CountTest reading (TAMC/TYMC with no location split) -
      // same source CountTestCard uses for its always-visible reported
      // value and entered-by footer.
      const reading = t.countTestReadings[t.countTestReadings.length - 1];
      result = reading?.reportedResult ?? "—";
      analystName = reading?.enteredByName ?? null;
      analystAt = reading?.enteredAt ?? null;
      isUnconfigured = reading?.status === "LimitsNotConfigured";
      conform = isConforming(reading?.status ?? null);
    } else {
      // Same precedence DetectionTestCard uses for a bare/pathogen
      // qualitative call, minus the locations fallback (none exist here
      // by construction - this branch only ever sees locations.length === 0).
      const lastResult = t.results[t.results.length - 1];
      const lastBiochemical = t.biochemicalResults[t.biochemicalResults.length - 1];
      const hasPathogenChain = t.pathogenObservations.length > 0;
      const detected = lastBiochemical?.organismDetected ?? t.pathogenObservations.some((p) => p.observation === "GrowthConforming");

      result = hasPathogenChain ? (detected ? "Detected" : "Absent") : lastResult?.interpretedValue ?? lastResult?.rawValue ?? "—";
      isUnconfigured = t.status === "LimitsNotConfigured";
      conform = hasPathogenChain ? !detected : isConforming(t.status);
      analystName = lastResult?.enteredByName ?? t.pathogenObservations[t.pathogenObservations.length - 1]?.observedByName ?? null;
      analystAt = lastResult?.enteredAt ?? t.pathogenObservations[t.pathogenObservations.length - 1]?.observedAt ?? null;
    }

    if (!conform) overallComplies = false;

    return {
      testOrderId: t.testOrderId,
      testCode: t.testCode,
      testDisplayName: t.testDisplayName,
      specification: t.specificationText,
      result,
      analystName,
      analystAt,
      conform,
      limitsNotConfigured: isUnconfigured
    };
  });

  // A TestCode can appear more than once here - most commonly when a
  // NewSampleRequest OOS retest sends the same test to two different
  // samples and both get pulled through into this sample's effective
  // results (SampleSummaryService.ResolveEffectiveTestOrdersAsync). The
  // COA is a single controlled document, not an internal audit trail, so
  // it shows exactly one row per TestCode: the first out-of-specification
  // result if any exists for that TestCode (the failure that actually
  // matters), otherwise the first conforming one. overallComplies above
  // already accounts for every duplicate, hidden or not.
  const dedupedRows: CoaSimpleRow[] = [];
  for (const row of rows) {
    const existingIndex = dedupedRows.findIndex((r) => r.testCode === row.testCode);
    if (existingIndex === -1) {
      dedupedRows.push(row);
    } else if (!row.conform && dedupedRows[existingIndex].conform) {
      dedupedRows[existingIndex] = row;
    }
  }

  return { rows: dedupedRows, overallComplies };
}

export function buildSimpleConclusionText(result: CoaSimpleResult): string {
  if (result.overallComplies) {
    return "Test results of the sample are Conform according to specification and decision rule.";
  }
  const hasFails = result.rows.some((r) => !r.conform && !r.limitsNotConfigured);
  const hasUnconfigured = result.rows.some((r) => r.limitsNotConfigured);
  if (!hasFails && hasUnconfigured) {
    return "Cannot certify — one or more results has no configured limit.";
  }
  return "Test results of the sample are Non-Conform according to specification and decision rule.";
}

// Result Date = the latest of every entered/observed/submitted timestamp
// across the sample's non-superseded test orders, located or not - the
// single "when was the last result recorded" moment for the certificate's
// date grid, independent of which specific test happened to finish last.
// ISO 8601 timestamps compare correctly as strings.
export function computeResultDate(testOrders: TestOrderSummaryDetail[]): string | null {
  let max: string | null = null;
  const consider = (ts: string | null | undefined) => {
    if (ts && (!max || ts > max)) max = ts;
  };
  for (const t of testOrders) {
    if (t.isSuperseded) continue;
    t.results.forEach((r) => consider(r.enteredAt));
    t.countTestReadings.forEach((r) => consider(r.enteredAt));
    t.locations.forEach((l) => consider(l.enteredAt));
    t.pathogenObservations.forEach((p) => consider(p.observedAt));
    t.biochemicalResults.forEach((b) => consider(b.submittedAt));
  }
  return max;
}
