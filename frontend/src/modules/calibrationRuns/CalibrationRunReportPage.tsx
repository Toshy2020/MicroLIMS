import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { reportStyles } from "../testingWorkspace/reportStyles";
import { CheckIcon, CrossIcon, dt, d, ReportFooter, PrintButton } from "../testingWorkspace/reportPrimitives";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { lightTheme } from "../../theme";
import {
  CalibrationRunService,
  CalibrationRunReportResponse
} from "./services/CalibrationRunService";

const CDS_LABELS: Record<string, string> = {
  PerkinElmerSyngistix: "PerkinElmer Syngistix",
  ShimadzuLabSolutions: "Shimadzu LabSolutions",
  AgilentOpenLab: "Agilent OpenLab",
  WatersEmpower3: "Waters Empower 3"
};

export function CalibrationRunReportPage() {
  const { id } = useParams();
  const [reportData, setReportData] = useState<CalibrationRunReportResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [downloading, setDownloading] = useState(false);

  useEffect(() => {
    if (!id) return;
    CalibrationRunService.getReport(Number(id))
      .then(setReportData)
      .catch((e) => setError(e?.response?.data?.message ?? "Failed to load the calibration run report."));
  }, [id]);

  useEffect(() => {
    if (reportData) {
      document.title = `Calibration Run Report - ${reportData.run.code}`;
    }
  }, [reportData]);

  if (error) {
    return (
      <PinnedLightTheme>
        <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: lightTheme.palette.error.main }}>
          {error}
        </div>
      </PinnedLightTheme>
    );
  }

  if (!reportData) {
    return (
      <PinnedLightTheme>
        <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: lightTheme.palette.text.secondary }}>
          Loading calibration run record…
        </div>
      </PinnedLightTheme>
    );
  }

  const { run: r, details: dtl } = reportData;

  const handleDownload = async () => {
    if (!r.id) return;
    setDownloading(true);
    try {
      await CalibrationRunService.downloadDocument(r.id, dtl.document?.originalFileName);
    } catch {
      alert("Failed to download attached report file.");
    } finally {
      setDownloading(false);
    }
  };

  const isWithdrawn = r.status === "Withdrawn";
  // AAS reuses this whole report (D-A4); null on the run's test means the legacy ICP-OES default.
  const isAas = dtl.calInstrumentType === "Aas";
  const instrumentTypeLabel = isAas ? "AAS" : "ICP-OES";
  const configuredStandardLevels = dtl.calStandardLevelsMgPerL;

  return (
    <PinnedLightTheme>
      <div className="report-root">
        <style>{reportStyles}</style>

        <div className="report-wrapper">
          {/* Header */}
          <div className="report-header">
            <div className="report-header-left">
              <div className="label">{instrumentTypeLabel} CALIBRATION RUN RECORD</div>
              <div className="sample-id">{r.code}</div>
              <div className="subtitle">
                {r.testDisplayName || r.testCode} ({r.methodAbbreviation || "—"}) · {r.sectionName}
              </div>
            </div>
            <div className="report-header-right">
              {isWithdrawn ? (
                <div className="status-badge is-danger">Withdrawn</div>
              ) : (
                <div className={`status-badge ${r.passed ? "" : "is-danger"}`}>
                  {r.passed ? <CheckIcon /> : <CrossIcon />}
                  {r.passed ? "Passed" : "Failed"}
                </div>
              )}
              <div className="header-date">
                Performed {dt(r.performedAt)} · Calibrated {dt(r.calibrationAt)}
              </div>
            </div>
          </div>

          {/* Instrument & Standards Cards */}
          <div className="two-col-grid">
            <div className="section-card">
              <div className="section-label">{isAas ? "instrument" : "instrument & cds software"}</div>
              <div className="data-grid">
                <span className="key">Instrument</span>
                <span className="value mono">{r.equipmentCode}</span>
                <span className="key">Name</span>
                <span className="value">{r.equipmentName ?? "—"}</span>
                <span className="key">Manufacturer</span>
                <span className="value">{dtl.equipmentVendor ?? (isAas ? "—" : "PerkinElmer")}</span>
                {!isAas && (
                  <>
                    <span className="key">CDS software</span>
                    <span className="value">
                      {dtl.cdsSoftware ? CDS_LABELS[dtl.cdsSoftware] ?? dtl.cdsSoftware : "PerkinElmer Syngistix"}
                    </span>
                  </>
                )}
                <span className="key">Max run age</span>
                <span className="value">{dtl.calMaxRunAgeHours ?? 24} hours</span>
                {configuredStandardLevels && (
                  <>
                    <span className="key">Standard levels</span>
                    <span className="value mono">{configuredStandardLevels} mg/L</span>
                  </>
                )}
                <span className="key">Reported basis</span>
                <span className="value">
                  {dtl.reportedConcentrationBasis ?? "SamplePpm"} (ppm in sample)
                </span>
              </div>
            </div>

            <div className="section-card">
              <div className="section-label">reference standards</div>
              <div className="data-grid">
                <span className="key">Calibration standard</span>
                <span className="value">{r.calibrationStandardMaterialName ?? "—"}</span>
                <span className="key">Lot / Batch</span>
                <span className="value mono">{r.calibrationStandardBatchNumber ?? "—"}</span>
                <span className="key">Expiry</span>
                <span className="value mono">{d(r.calibrationStandardExpiryDate)}</span>
                <span className="key">ICV standard</span>
                <span className="value">{r.icvStandardMaterialName ?? "None (Single Source)"}</span>
                <span className="key">ICV Lot / Batch</span>
                <span className="value mono">{r.icvStandardBatchNumber ?? "—"}</span>
                <span className="key">ICV Expiry</span>
                <span className="value mono">{r.icvStandardExpiryDate ? d(r.icvStandardExpiryDate) : "—"}</span>
              </div>
            </div>
          </div>

          {/* Method Acceptance Criteria */}
          <div className="section-card">
            <div className="section-label">method acceptance criteria (test master)</div>
            <div className="data-grid" style={{ gridTemplateColumns: "140px 1fr 140px 1fr" }}>
              <span className="key">Min correlation</span>
              <span className="value mono">
                {dtl.calMinCorrelation ?? "0.9995"} ({dtl.calCorrelationType === "RSquared" ? "r²" : "r"})
              </span>
              <span className="key">Min standards</span>
              <span className="value mono">{dtl.calMinStandards ?? 5}</span>
              <span className="key">ICV recovery %</span>
              <span className="value mono">
                {dtl.calCheckRecoveryLowPercent ?? 95.0}% – {dtl.calCheckRecoveryHighPercent ?? 105.0}%
              </span>
              <span className="key">CCV recovery %</span>
              <span className="value mono">
                {dtl.calCheckRecoveryLowPercent ?? 90.0}% – {dtl.calCheckRecoveryHighPercent ?? 110.0}%
              </span>
              <span className="key">Blank maximum</span>
              <span className="value mono">
                {dtl.calBlankMax != null ? `${dtl.calBlankMax} mg/L` : "LOQ of analyte"}
              </span>
              <span className="key">IS recovery %</span>
              <span className="value mono">
                {dtl.calIsRecoveryLowPercent != null && dtl.calIsRecoveryHighPercent != null
                  ? `${dtl.calIsRecoveryLowPercent}% – ${dtl.calIsRecoveryHighPercent}%`
                  : "Not checked"}
              </span>
            </div>
          </div>

          {/* Instrument Software Report Attachment Card (Syngistix for ICP-OES) */}
          {dtl.document && (
            <div className="section-card">
              <div className="section-label">
                attached {isAas ? "instrument software" : "syngistix"} report document
              </div>
              <div className="data-grid">
                <span className="key">File name</span>
                <span className="value mono">{dtl.document.originalFileName}</span>
                <span className="key">Size</span>
                <span className="value mono">{(dtl.document.sizeBytes / 1024).toFixed(1)} KB</span>
                <span className="key">SHA-256 checksum</span>
                <span className="value mono" style={{ fontSize: 11, wordBreak: "break-all" }}>
                  {dtl.document.contentSha256}
                </span>
                <span className="key">Uploaded</span>
                <span className="value">
                  {dt(dtl.document.uploadedAt)} by {dtl.document.uploadedByName ?? "Analyst"}
                </span>
              </div>
              <div style={{ marginTop: 12 }}>
                <button
                  type="button"
                  onClick={handleDownload}
                  disabled={downloading}
                  style={{
                    padding: "6px 14px",
                    background: "#0284c7",
                    color: "#fff",
                    border: "none",
                    borderRadius: 4,
                    cursor: "pointer",
                    fontWeight: 600,
                    fontSize: 12
                  }}
                >
                  {downloading ? "Downloading..." : "Download Original Report"}
                </button>
              </div>
            </div>
          )}

          {/* Analytes and Checks Table */}
          <div style={{ marginBottom: 24 }}>
            <div className="section-divider">
              <div className="section-label">analyte calibration curves &amp; checks ({dtl.analytes.length})</div>
              <div className="line" />
            </div>

            {dtl.analytes.map((analyte) => (
              <div className="section-card" key={analyte.id} style={{ marginBottom: 16 }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                  <div>
                    <strong style={{ fontSize: 15 }}>
                      {analyte.element}
                    </strong>{" "}
                    <span style={{ fontSize: 13, color: "var(--color-text-secondary)" }}>
                      ({analyte.wavelengthNm} nm · {analyte.view} view)
                    </span>
                  </div>
                  <div className={`status-badge ${analyte.passed ? "" : "is-danger"}`} style={{ height: 22, fontSize: 11 }}>
                    {analyte.passed ? <CheckIcon strokeWidth={2} /> : <CrossIcon />}
                    {analyte.passed ? "Passed" : "Failed"}
                  </div>
                </div>

                <div className="data-grid" style={{ gridTemplateColumns: "140px 1fr 140px 1fr", marginBottom: 12 }}>
                  <span className="key">Correlation</span>
                  <span className="value mono">
                    {analyte.correlationValue} ({analyte.correlationType === "RSquared" ? "r²" : "r"})
                  </span>
                  <span className="key">Standards count</span>
                  <span className="value mono">{analyte.numberOfStandards}</span>
                  <span className="key">Calibration range</span>
                  <span className="value mono">
                    {analyte.lowestStandardMgPerL} – {analyte.highestStandardMgPerL} mg/L
                  </span>
                  <span className="key">Usable for testing</span>
                  <span className="value mono">{analyte.isUsable ? "Yes" : "No"}</span>
                </div>

                {!analyte.passed && analyte.failureReasons && (
                  <div
                    style={{
                      padding: "8px 12px",
                      background: "rgba(211, 47, 47, 0.08)",
                      borderLeft: "3px solid #d32f2f",
                      color: "#b42318",
                      fontSize: 12,
                      marginBottom: 12
                    }}
                  >
                    <strong>Failure reasons:</strong> {analyte.failureReasons}
                  </div>
                )}

                <div className="section-label" style={{ fontSize: 11, marginBottom: 4 }}>
                  calibration checks
                </div>
                <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 12 }}>
                  <thead>
                    <tr style={{ textAlign: "left", color: "var(--color-text-tertiary)", borderBottom: "1px solid var(--color-border)" }}>
                      <th style={{ padding: "5px 4px" }}>Check Type</th>
                      <th style={{ padding: "5px 4px" }}>Seq</th>
                      <th style={{ padding: "5px 4px", textAlign: "right" }}>Nominal (mg/L)</th>
                      <th style={{ padding: "5px 4px", textAlign: "right" }}>Measured (mg/L)</th>
                      <th style={{ padding: "5px 4px", textAlign: "right" }}>Computed Recovery</th>
                      <th style={{ padding: "5px 4px", textAlign: "center" }}>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {analyte.checks.map((chk) => (
                      <tr key={chk.id} style={{ borderTop: "1px solid var(--color-border)" }}>
                        <td style={{ padding: "5px 4px" }}>{chk.checkType}</td>
                        <td style={{ padding: "5px 4px" }}>{chk.sequencePosition}</td>
                        <td style={{ padding: "5px 4px", textAlign: "right" }} className="mono">
                          {chk.nominalMgPerL != null ? chk.nominalMgPerL : "—"}
                        </td>
                        <td style={{ padding: "5px 4px", textAlign: "right" }} className="mono">
                          {chk.measuredMgPerL}
                        </td>
                        <td style={{ padding: "5px 4px", textAlign: "right", fontWeight: 600 }} className="mono">
                          {chk.recoveryPercent != null ? `${chk.recoveryPercent.toFixed(2)}%` : "—"}
                        </td>
                        <td style={{ padding: "5px 4px", textAlign: "center" }}>
                          <span
                            style={{
                              padding: "2px 6px",
                              borderRadius: 4,
                              fontSize: 11,
                              fontWeight: 600,
                              background: chk.passed ? "#ecfdf5" : "#fef2f2",
                              color: chk.passed ? "#065f46" : "#991b1b"
                            }}
                          >
                            {chk.passed ? "Pass" : "Fail"}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
          </div>

          {/* Withdrawal Notice If Withdrawn */}
          {isWithdrawn && (
            <div
              className="section-card"
              style={{
                border: "2px solid #f59e0b",
                background: "rgba(245, 158, 11, 0.05)",
                marginBottom: 24
              }}
            >
              <div className="section-label" style={{ color: "#b45309" }}>
                WITHDRAWAL AUDIT RECORD
              </div>
              <div className="data-grid">
                <span className="key">Withdrawn at</span>
                <span className="value mono">{dt(r.withdrawnAt)}</span>
                <span className="key">Withdrawn by</span>
                <span className="value">{r.withdrawnByName ?? dtl.withdrawnByName ?? "—"}</span>
                <span className="key">Reason</span>
                <span className="value">{r.withdrawalReason ?? dtl.withdrawalReason ?? "—"}</span>
              </div>
              {dtl.withdrawalSignature && (
                <div style={{ marginTop: 10, fontSize: 12, color: "#92400e" }}>
                  <strong>Withdrawal Signature:</strong> {dtl.withdrawalSignature.userFullNameSnapshot} ·{" "}
                  {dt(dtl.withdrawalSignature.signedAt)} · Meaning: {dtl.withdrawalSignature.meaning}
                </div>
              )}
            </div>
          )}

          {/* Performed By Electronic Signature Card */}
          <div style={{ marginBottom: 24 }}>
            <div className="section-divider">
              <div className="section-label">electronic signature</div>
              <div className="line" />
            </div>
            <div className="signature-grid">
              <div className="signature-card">
                <div className="sig-header">
                  <div className="sig-icon">
                    <CheckIcon />
                  </div>
                  <div>
                    <div className="sig-name">{dtl.signature?.userFullNameSnapshot ?? r.performedByName}</div>
                    <div className="sig-role">Analyst ({instrumentTypeLabel} Operator)</div>
                  </div>
                </div>
                <div className="sig-time">{dt(dtl.signature?.signedAt ?? r.performedAt)}</div>
                <div className="sig-meaning">
                  Meaning:{" "}
                  {dtl.signature?.meaning ??
                    "I have performed this calibration run and verified that the entered values match the instrument report."}
                </div>
                {r.comment && <div className="sig-comment">&ldquo;{r.comment}&rdquo;</div>}
              </div>
            </div>
          </div>

          <ReportFooter documentId={r.code} />
        </div>

        <PrintButton />
      </div>
    </PinnedLightTheme>
  );
}
