import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { reportStyles } from "../testingWorkspace/reportStyles";
import { CheckIcon, CrossIcon, dt, SignatureSection, ReportFooter, PrintButton } from "../testingWorkspace/reportPrimitives";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { lightTheme } from "../../theme";
import { SystemSuitabilityService, SuitabilityRunReport } from "./services/SystemSuitabilityService";

const v = (n?: number | null) => (n === null || n === undefined ? "—" : String(n));

const CDS_LABELS: Record<string, string> = {
  ShimadzuLabSolutions: "Shimadzu LabSolutions",
  AgilentOpenLab: "Agilent OpenLab",
  WatersEmpower3: "Waters Empower 3"
};

// Printable record of one System Suitability run: what was injected (standard
// weight, dilution, purity, mean area), the CDS-reported criteria against the
// method's acceptance limits, the signature, and every test linked to it.
// Pass/Fail is the server's decision recorded at signing - shown, not recalculated.
export function SuitabilityRunReportPage() {
  const { id } = useParams();
  const [report, setReport] = useState<SuitabilityRunReport | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    SystemSuitabilityService.getReport(Number(id))
      .then(setReport)
      .catch((e) => setError(e?.response?.data?.message ?? "Failed to load the suitability run."));
  }, [id]);

  useEffect(() => {
    if (report) document.title = `System Suitability Run - ${report.run.code}`;
  }, [report]);

  if (error) return <PinnedLightTheme><div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: lightTheme.palette.error.main }}>{error}</div></PinnedLightTheme>;
  if (!report) return <PinnedLightTheme><div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: lightTheme.palette.text.secondary }}>Loading record…</div></PinnedLightTheme>;

  const { run: r, details: d } = report;
  const criteria: { label: string; value?: number | null; limit?: number | null; rule: "NMT" | "NLT" }[] = [
    { label: "%RSD (replicate standard injections)", value: r.rsdPercent, limit: d.sstMaxRsdPercent, rule: "NMT" },
    { label: "Resolution", value: r.resolution, limit: d.sstMinResolution, rule: "NLT" },
    { label: "Tailing factor", value: r.tailingFactor, limit: d.sstMaxTailingFactor, rule: "NMT" },
    { label: "Theoretical plates", value: r.theoreticalPlates, limit: d.sstMinTheoreticalPlates, rule: "NLT" }
  ];
  const analytes = d.analytes ?? [];
  const isMulti = analytes.length > 0;
  // Titration runs (SC-5a) never have a chromatography column.
  const isTitration = isMulti && r.chromatographyColumnId == null;
  const criterionCell = (value: number | null | undefined, limit: number | null | undefined, rule: "NMT" | "NLT") =>
    `${v(value)} (${limit == null ? "not checked" : `${rule} ${limit}`})`;
  const weighInCell = (a: { theoreticalWeightMg?: number | null; standardWeighInDeviationPercent?: number | null; standardWeighInOutOfWindow?: boolean }) =>
    a.theoreticalWeightMg == null
      ? "—"
      : `${v(a.theoreticalWeightMg)} mg (dev ${v(a.standardWeighInDeviationPercent)}%${a.standardWeighInOutOfWindow ? ", OUT OF WINDOW" : ""})`;

  return (
    <PinnedLightTheme>
    <div className="report-root">
      <style>{reportStyles}</style>

      <div className="report-wrapper">
        <div className="report-header">
          <div className="report-header-left">
            <div className="label">system suitability run</div>
            <div className="sample-id">{r.code}</div>
            <div className="subtitle">{r.testName} ({r.testCode}) · {r.sectionName}</div>
          </div>
          <div className="report-header-right">
            <div className={`status-badge ${r.passed ? "" : "is-danger"}`}>
              {r.passed ? <CheckIcon /> : <CrossIcon />}
              {r.passed ? "Passed" : "Failed"}
            </div>
            <div className="header-date">Performed {dt(r.performedAt)}</div>
          </div>
        </div>

        {isMulti ? (
          <div className="section-card">
            <div className="section-label">instrument &amp; column</div>
            <div className="data-grid">
              <span className="key">{isTitration ? "Titrator" : "Instrument"}</span><span className="value mono">{r.equipmentCode}</span>
              <span className="key">Name</span><span className="value">{r.equipmentName ?? "—"}</span>
              <span className="key">Manufacturer</span><span className="value">{d.equipmentVendor ?? "—"}</span>
              {!isTitration && <span className="key">CDS software</span>}
              {!isTitration && <span className="value">{d.cdsSoftware ? (CDS_LABELS[d.cdsSoftware] ?? d.cdsSoftware) : "—"}</span>}
              <span className="key">Column</span><span className="value mono">{isTitration ? "—" : (r.columnCode ?? "—")}</span>
              {!isTitration && <span className="key">Column name</span>}
              {!isTitration && <span className="value">{r.columnName ?? "—"}</span>}
              {!isTitration && <span className="key">Column serial</span>}
              {!isTitration && <span className="value mono">{d.columnSerialNumber ?? "—"}</span>}
            </div>
          </div>
        ) : (
          <div className="two-col-grid">
            <div className="section-card">
              <div className="section-label">instrument &amp; column</div>
              <div className="data-grid">
                <span className="key">Instrument</span><span className="value mono">{r.equipmentCode}</span>
                <span className="key">Name</span><span className="value">{r.equipmentName ?? "—"}</span>
                <span className="key">Manufacturer</span><span className="value">{d.equipmentVendor ?? "—"}</span>
                <span className="key">CDS software</span><span className="value">{d.cdsSoftware ? (CDS_LABELS[d.cdsSoftware] ?? d.cdsSoftware) : "—"}</span>
                <span className="key">Column</span><span className="value mono">{r.columnCode}</span>
                <span className="key">Column name</span><span className="value">{r.columnName ?? "—"}</span>
                <span className="key">Column serial</span><span className="value mono">{d.columnSerialNumber ?? "—"}</span>
              </div>
            </div>
            <div className="section-card">
              <div className="section-label">reference standard</div>
              <div className="data-grid">
                <span className="key">Standard</span><span className="value">{r.referenceStandardName ?? "—"}</span>
                <span className="key">Batch</span><span className="value mono">{r.referenceStandardBatch ?? "—"}</span>
                <span className="key">Purity</span><span className="value mono">{v(r.standardPurityPercent)} %</span>
                <span className="key">Standard weight</span><span className="value mono">{v(r.standardWeightMg)} mg</span>
                <span className="key">Standard dilution</span><span className="value mono">{v(r.standardDilution)}</span>
                <span className="key">Mean peak area</span><span className="value mono">{v(r.standardMeanArea)}</span>
              </div>
            </div>
          </div>
        )}

        {isMulti ? (
          <div className="section-card">
            <div className="section-label">per-vitamin standards &amp; suitability criteria (entered from the CDS report)</div>
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
                <thead>
                  <tr style={{ textAlign: "left", color: "var(--color-text-tertiary)" }}>
                    <th style={{ padding: "6px 4px" }}>Vitamin / Analyte</th>
                    <th style={{ padding: "6px 4px" }}>Standard</th>
                    <th style={{ padding: "6px 4px" }}>Batch</th>
                    <th style={{ padding: "6px 4px" }}>Purity %</th>
                    <th style={{ padding: "6px 4px" }}>Th.Wt.std (dev)</th>
                    <th style={{ padding: "6px 4px" }}>Act. weight (mg)</th>
                    <th style={{ padding: "6px 4px" }}>MC %</th>
                    <th style={{ padding: "6px 4px" }}>Responses</th>
                    <th style={{ padding: "6px 4px" }}>Computed %RSD (limit)</th>
                    {isTitration && <th style={{ padding: "6px 4px" }}>Blank titre (mL)</th>}
                    {!isTitration && <th style={{ padding: "6px 4px" }}>Resolution (limit)</th>}
                    {!isTitration && <th style={{ padding: "6px 4px" }}>Tailing (limit)</th>}
                    {!isTitration && <th style={{ padding: "6px 4px" }}>Plates (limit)</th>}
                    <th style={{ padding: "6px 4px" }}>Result</th>
                  </tr>
                </thead>
                <tbody>
                  {analytes.map((a) => (
                    <tr key={a.testAnalyteId} style={{ borderTop: "1px solid var(--color-border)" }}>
                      <td style={{ padding: "6px 4px" }}>
                        {a.analyteName}
                        <div className="mono" style={{ fontSize: 11, color: "var(--color-text-tertiary)" }}>{a.wavelengthNm} nm</div>
                      </td>
                      <td style={{ padding: "6px 4px" }}>{a.referenceStandardName ?? "—"}</td>
                      <td style={{ padding: "6px 4px" }} className="mono">{a.referenceStandardBatch ?? "—"}</td>
                      <td style={{ padding: "6px 4px" }} className="mono">{v(a.standardPurityPercent)}</td>
                      <td style={{ padding: "6px 4px" }} className="mono">
                        {weighInCell(a)}
                        {a.standardWeighInOutOfWindow && a.weighInJustification && (
                          <div style={{ fontSize: 11, color: "var(--color-text-tertiary)" }}>Justification: {a.weighInJustification}</div>
                        )}
                      </td>
                      <td style={{ padding: "6px 4px" }} className="mono">{v(a.standardWeightMg)}</td>
                      <td style={{ padding: "6px 4px" }} className="mono">{v(a.moisturePercent)}</td>
                      <td style={{ padding: "6px 4px" }} className="mono">
                        {a.responses && a.responses.length > 0 ? a.responses.map((resp) => resp.response).join(", ") : v(a.standardMeanArea)}
                      </td>
                      <td style={{ padding: "6px 4px" }} className="mono">
                        {criterionCell(a.computedRsdPercent ?? a.rsdPercent, a.sstMaxRsdPercent, "NMT")}
                      </td>
                      {isTitration && <td style={{ padding: "6px 4px" }} className="mono">{v(a.blankTitreMl)}</td>}
                      {!isTitration && <td style={{ padding: "6px 4px" }} className="mono">{criterionCell(a.resolution, a.sstMinResolution, "NLT")}</td>}
                      {!isTitration && <td style={{ padding: "6px 4px" }} className="mono">{criterionCell(a.tailingFactor, a.sstMaxTailingFactor, "NMT")}</td>}
                      {!isTitration && <td style={{ padding: "6px 4px" }} className="mono">{criterionCell(a.theoreticalPlates, a.sstMinTheoreticalPlates, "NLT")}</td>}
                      <td style={{ padding: "6px 4px" }}>
                        {a.passed ? <CheckIcon /> : <CrossIcon />}
                        {a.failureReasons && (
                          <div style={{ fontSize: 11, color: "var(--color-danger, #b42318)" }}>{a.failureReasons}</div>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {!r.passed && r.failureReasons && (
              <div style={{ marginTop: 10, fontSize: 13, color: "var(--color-danger, #b42318)" }}>
                <strong>Overall failure reasons:</strong> {r.failureReasons}
              </div>
            )}
            <div style={{ marginTop: 10, fontSize: 12, color: "var(--color-text-tertiary)" }}>
              The run passes only if every vitamin passes its own criteria. Pass/Fail was decided when the run was signed.
            </div>
          </div>
        ) : (
          <div className="section-card">
            <div className="section-label">suitability criteria (entered from the CDS report)</div>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
              <thead>
                <tr style={{ textAlign: "left", color: "var(--color-text-tertiary)" }}>
                  <th style={{ padding: "6px 4px" }}>Criterion</th>
                  <th style={{ padding: "6px 4px" }}>Measured</th>
                  <th style={{ padding: "6px 4px" }}>Acceptance (Test Master)</th>
                </tr>
              </thead>
              <tbody>
                {criteria.map((c) => (
                  <tr key={c.label} style={{ borderTop: "1px solid var(--color-border)" }}>
                    <td style={{ padding: "6px 4px" }}>{c.label}</td>
                    <td style={{ padding: "6px 4px" }} className="mono">{v(c.value)}</td>
                    <td style={{ padding: "6px 4px" }}>{c.limit === null || c.limit === undefined ? "Not checked" : `${c.rule} ${c.limit}`}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            {!r.passed && r.failureReasons && (
              <div style={{ marginTop: 10, fontSize: 13, color: "var(--color-danger, #b42318)" }}>
                <strong>Failure reasons:</strong> {r.failureReasons}
              </div>
            )}
            <div style={{ marginTop: 10, fontSize: 12, color: "var(--color-text-tertiary)" }}>
              Pass/Fail was decided when the run was signed. Acceptance limits shown are the current Test Master values.
            </div>
          </div>
        )}

        {r.comment && (
          <div className="section-card">
            <div className="section-label">comment</div>
            <div style={{ fontSize: 14 }}>{r.comment}</div>
          </div>
        )}

        <div style={{ marginBottom: 24 }}>
          <div className="section-divider">
            <div className="section-label">samples tested with this run ({d.linkedTests.length})</div>
            <div className="line" />
          </div>
          <div className="section-card">
            {d.linkedTests.length === 0 ? (
              <span style={{ fontSize: 13, color: "var(--color-text-quaternary)" }}>No tests linked to this run yet.</span>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 13 }}>
                <thead>
                  <tr style={{ textAlign: "left", color: "var(--color-text-tertiary)" }}>
                    <th style={{ padding: "6px 4px" }}>Sample</th>
                    <th style={{ padding: "6px 4px" }}>Item / Batch</th>
                    <th style={{ padding: "6px 4px" }}>Test</th>
                    <th style={{ padding: "6px 4px" }}>Result</th>
                    <th style={{ padding: "6px 4px" }}>Entered</th>
                  </tr>
                </thead>
                <tbody>
                  {d.linkedTests.map((t) => (
                    <tr key={t.testOrderId} style={{ borderTop: "1px solid var(--color-border)" }}>
                      <td style={{ padding: "6px 4px" }} className="mono">{t.sampleReferenceNumber}</td>
                      <td style={{ padding: "6px 4px" }}>{t.itemName ?? "—"}{t.batchNumber ? ` · ${t.batchNumber}` : ""}</td>
                      <td style={{ padding: "6px 4px" }}>{t.testCode}</td>
                      <td style={{ padding: "6px 4px" }}>{t.reportedResult ? `${t.reportedResult} (${t.resultStatus})` : "Not entered yet"}</td>
                      <td style={{ padding: "6px 4px" }} className="mono">{t.resultEnteredAt ? dt(t.resultEnteredAt) : "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

        <SignatureSection signatures={d.signature ? [d.signature] : []} />
        <ReportFooter documentId={r.code} />
      </div>

      <PrintButton />
    </div>
    </PinnedLightTheme>
  );
}
