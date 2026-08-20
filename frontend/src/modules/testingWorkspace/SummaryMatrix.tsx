import { TestOrderSummaryDetail } from "./types/sampleSummaryTypes";
import { humanize } from "./SampleReportPage";

// Binary conform/exception, not the full 4-status ladder
// (WithinLimits/Alert/Action/OOS) - the app's own status colors fail the
// dataviz palette validator for adjacent-pair distinguishability
// (Action vs OOS, ΔE 8.7, below the 15 floor for normal vision), so this
// compact overview only ever needs "does this need a closer look," never
// color-coded severity. The exact status is still shown as text for
// qualitative cells, and as a title tooltip for quantitative ones -
// color is never the only signal.
const CONFORMING_STATUSES = new Set(["WithinLimits", "Absent"]);

function isQuantitative(test: TestOrderSummaryDetail): boolean {
  return test.countTestReadings.length > 0 || test.locations.some((l) => l.cfuResult !== null);
}

interface MatrixCell {
  text: string;
  conform: boolean;
  title?: string;
}

interface MatrixRow {
  locationKey: string;
  locationName: string;
  cells: MatrixCell[];
}

// Rows = distinct physical sampling locations (keyed by the stable
// locationKey - never locationName, which is free text and can collide,
// e.g. multiple water sampling points sharing the label "WTU").
// Columns = every test on this sample that has per-location results.
// Tests without locations (single-value CountTest/pathogen chain/plain
// result) have no location dimension to plot and are intentionally left
// out of the matrix - they're still fully shown in the test cards below.
export function SummaryMatrix({ testOrders }: { testOrders: TestOrderSummaryDetail[] }) {
  const locatedTests = testOrders.filter((t) => t.locations.length > 0 && !t.isSuperseded);
  if (locatedTests.length === 0) return null;

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

  let totalCount = 0;
  let exceptionCount = 0;

  const rows: MatrixRow[] = keysInOrder.map((locationKey) => {
    const cells = locatedTests.map((t): MatrixCell => {
      const loc = t.locations.find((l) => l.locationKey === locationKey);
      if (!loc) return { text: "—", conform: true };

      totalCount++;
      const conform = loc.status ? CONFORMING_STATUSES.has(loc.status) : true;
      if (!conform) exceptionCount++;

      if (isQuantitative(t)) {
        const value = loc.cfuResult ?? loc.calculatedResult;
        return { text: value != null ? String(value) : "—", conform, title: !conform ? humanize(loc.status) : undefined };
      }
      return { text: conform ? "✓ ND" : `✗ ${humanize(loc.status)}`, conform };
    });
    return { locationKey, locationName: namesByKey.get(locationKey) ?? locationKey, cells };
  });

  return (
    <div className="matrix-section">
      <div className="matrix-header">
        <div>
          <h3>Results Summary — All Tests × All Locations</h3>
          <div className="matrix-sub">
            {locatedTests.length} test{locatedTests.length === 1 ? "" : "s"} · {rows.length} sampling location{rows.length === 1 ? "" : "s"} · {totalCount} result{totalCount === 1 ? "" : "s"}
            {exceptionCount > 0 ? `, ${exceptionCount} exception${exceptionCount === 1 ? "" : "s"}` : ""}
          </div>
        </div>
        <span className={`status-badge ${exceptionCount > 0 ? "is-danger" : ""}`}>
          {exceptionCount > 0 ? `${exceptionCount} exception${exceptionCount === 1 ? "" : "s"}` : "All Conform"}
        </span>
      </div>
      <div className="matrix-wrap">
        <table className="result-matrix">
          <thead>
            <tr>
              <th>Location</th>
              {locatedTests.map((t) => (
                <th key={t.testOrderId}>{t.testCode}{isQuantitative(t) ? " (CFU)" : ""}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.locationKey}>
                <td>{r.locationName}</td>
                {r.cells.map((c, i) => (
                  <td
                    key={locatedTests[i].testOrderId}
                    className="matrix-cell"
                    title={c.title}
                    style={{ color: c.conform ? "var(--color-positive)" : "var(--color-danger)" }}
                  >
                    {c.text}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="matrix-legend">
        <span><span className="legend-dot" style={{ background: "var(--color-positive)" }} />ND = Not Detected / Conform</span>
        <span>Detail per test available below — expand only if investigating a result</span>
      </div>
    </div>
  );
}
