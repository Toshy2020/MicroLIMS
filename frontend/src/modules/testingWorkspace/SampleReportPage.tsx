import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { ArchivedRecordsService, ArchivedRecordSummary } from "./services/ArchivedRecordsService";
import { ArchivedCopiesSection } from "./reportPrimitives";
import { SampleSummary, TestOrderSummaryDetail } from "./types/sampleSummaryTypes";
import { reportStyles } from "./reportStyles";

const CheckIcon = ({ strokeWidth = 2.5 }: { strokeWidth?: number }) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12" />
  </svg>
);
const CrossIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
    <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
  </svg>
);
const DotIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="4" />
  </svg>
);

// Enum names reach the client in PascalCase ("FinishedProduct",
// "WithinLimits"). On a controlled document they need to read as English.
// Consecutive capitals are preserved so acronyms survive: "TAMC" stays
// "TAMC", "OOSInvestigation" becomes "OOS investigation".
const humanize = (v: string | null | undefined): string => {
  if (!v) return "—";
  return v
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2")
    .replace(/\s+(.)/g, (_, c: string) => " " + c.toLowerCase());
};

// 11.50(b) requires the *meaning* of a signature, not just its type -
// the enum name alone ("Reviewed") does not state what was attested to.
const MEANING_TEXT: Record<string, string> = {
  Reviewed: "I have reviewed the test data and confirm it is complete and accurate.",
  Approved: "I approve the release of this sample for its intended use.",
  Rejected: "I reject this sample; the results do not conform to specification.",
  RetestRequested: "I am ordering a retest of the retained sample.",
  InvestigationOrdered: "I am ordering an investigation into these results."
};

const dt = (v: string | null | undefined) =>
  v ? new Date(v).toLocaleString("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }).replace(",", "") : "—";
const d = (v: string | null | undefined) =>
  v ? new Date(v).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }) : "—";
const hhmm = (v: string | null | undefined) =>
  v ? new Date(v).toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" }) : "";

// Terminal states render red; anything mid-flight renders neutral.
function statusTone(status: string): "" | "is-danger" | "is-warning" | "is-neutral" {
  if (status === "Approved") return "";
  if (status === "Rejected") return "is-danger";
  if (status === "UnderReview" || status === "UnderApproval") return "is-warning";
  return "is-neutral";
}

// Same palette as StatusBadge.tsx's color map, for the location table's
// status chip - both the CFU/count severity ladder and the pathogen
// Detected/Absent call.
const LOCATION_STATUS_COLOR: Record<string, string> = {
  WithinLimits: "#16a34a",
  AlertLimitExceeded: "#f59e0b",
  ActionLimitExceeded: "#ea580c",
  OutOfSpecification: "#dc2626",
  Detected: "#dc2626",
  Absent: "#16a34a"
};

const STATUS_LABEL: Record<string, string> = {
  Received: "Received", InTesting: "Under testing", UnderReview: "Under review",
  UnderApproval: "Under approval", Approved: "Approved", Rejected: "Rejected"
};

// The sample-level lifecycle track. Per-TestOrder step history stays
// inside each test card - with several tests running in parallel there is
// no single step sequence to draw, so this shows only the states the
// Sample itself passes through.
function buildTimeline(s: SampleSummary) {
  const eventAt = (type: string) => s.timeline.find((e) => e.eventType === type)?.timestamp ?? null;
  const firstIncubation = s.testOrders
    .flatMap((t) => t.incubations.map((i) => i.startedAt))
    .sort()[0] ?? null;

  const decided = s.status === "Approved" || s.status === "Rejected";
  return [
    { label: "Received", at: s.receivedAt },
    { label: "In testing", at: firstIncubation },
    { label: "Under review", at: eventAt("SubmittedForReview") },
    { label: "Under approval", at: s.reviewedAt },
    { label: s.status === "Rejected" ? "Rejected" : "Approved", at: decided ? s.approvedAt ?? eventAt("ApprovalDecisionMade") : null, danger: s.status === "Rejected" }
  ];
}

export function SampleReportPage() {
  const { id } = useParams();
  const [summary, setSummary] = useState<SampleSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [archivedCopies, setArchivedCopies] = useState<ArchivedRecordSummary[]>([]);

  useEffect(() => {
    if (!id) return;
    SampleSummaryService.getSummary(Number(id))
      .then(setSummary)
      .catch((e) => setError(e?.response?.data?.message ?? "Failed to load the sample report."));
    ArchivedRecordsService.getForEntity("Sample", Number(id)).then(setArchivedCopies).catch(() => setArchivedCopies([]));
  }, [id]);

  useEffect(() => {
    if (summary) document.title = `Sample Summary - ${summary.referenceNumber}`;
  }, [summary]);

  if (error) return <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#dc2626" }}>{error}</div>;
  if (!summary) return <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#666" }}>Loading report…</div>;

  const s = summary;
  const steps = buildTimeline(s);
  const generatedAt = dt(new Date().toISOString());

  return (
    <div className="report-root">
      <style>{reportStyles}</style>

      <div className="report-wrapper">
        <div className="report-header">
          <div className="report-header-left">
            <div className="label">sample summary report</div>
            <div className="sample-id">{s.referenceNumber}</div>
            <div className="subtitle">
              {humanize(s.category)}
              {s.displayName ? <> · <strong>{s.displayName}</strong></> : ""}
              {s.batchNumber ? ` · Batch: ${s.batchNumber}` : ""}
            </div>
          </div>
          <div className="report-header-right">
            <div className={`status-badge ${statusTone(s.status)}`}>
              {s.status === "Rejected" ? <CrossIcon /> : s.status === "Approved" ? <CheckIcon /> : <DotIcon />}
              {STATUS_LABEL[s.status] ?? s.status}
            </div>
            <div className="header-date">Received {dt(s.receivedAt)}</div>
          </div>
        </div>

        <div className="two-col-grid">
          <div className="section-card">
            <div className="section-label">sample identity</div>
            <div className="data-grid">
              <span className="key">Reference</span><span className="value mono">{s.referenceNumber}</span>
              <span className="key">Category</span><span className="value">{humanize(s.category)}</span>
              <span className="key">Item</span><span className="value">{s.displayName || "—"}</span>
              {s.productionStage && (<><span className="key">Stage</span><span className="value">{s.productionStage}</span></>)}
              <span className="key">Batch</span><span className="value mono">{s.batchNumber ?? "—"}</span>
              <span className="key">Control</span><span className="value mono">{s.controlNumber}</span>
              <span className="key">Cause</span><span className="value">{s.causeOfTesting}</span>
              <span className="key">Quantity</span><span className="value">{s.sampleQuantity ?? "—"}</span>
              {s.waterSamplingPointCode && (
                <><span className="key">Sampling point</span>
                <span className="value">{s.waterSamplingPointCode} — {s.waterSamplingPointLocation}</span></>
              )}
              {s.storageCondition && (
                <><span className="key">Storage</span>
                <span className="value">{s.storageCondition === "Refrigerator" ? `Refrigerator (${s.storageTimeHours ?? "?"}h)` : s.storageCondition}</span></>
              )}
            </div>
          </div>
          <div className="section-card">
            <div className="section-label">dates &amp; personnel</div>
            <div className="data-grid">
              <span className="key">Manufactured</span><span className="value">{d(s.mfgDate)}</span>
              <span className="key">Expires</span><span className="value">{d(s.expDate)}</span>
              <span className="key">Received by</span><span className="value">{s.receivedByName}</span>
              <span className="key">Received at</span><span className="value mono">{dt(s.receivedAt)}</span>
              <span className="key">Sampled by</span><span className="value">{s.sampledBy}</span>
              {s.reviewedByName && (<><span className="key">Reviewed by</span><span className="value">{s.reviewedByName}</span></>)}
              {s.approvedByName && (<><span className="key">Approved by</span><span className="value">{s.approvedByName}</span></>)}
            </div>
          </div>
        </div>

        {s.preparation && (
          <div className="section-card">
            <div className="section-label">sample preparation</div>
            <div className="prep-grid">
              <div className="prep-item">
                <div className="prep-label">Amount</div>
                <div className="prep-value">{s.preparation.amount} {s.preparation.unit}</div>
              </div>
              <div className="prep-item">
                <div className="prep-label">Technique</div>
                <div className="prep-value">{humanize(s.preparation.technique)}</div>
                {s.preparation.technique === "Filtration" && (
                  <div className="prep-sub">Filtration {s.preparation.filtrationVolume ?? "—"} · Washing {s.preparation.washingVolume ?? "—"}</div>
                )}
              </div>
              <div className="prep-item">
                <div className="prep-label">Neutralizer</div>
                <div className="prep-value">{s.preparation.neutralizerName || "—"}</div>
              </div>
              <div className="prep-item">
                <div className="prep-label">Prepared by</div>
                <div className="prep-value">{s.preparation.preparedByName}</div>
                <div className="prep-sub">{dt(s.preparation.preparedAt)}</div>
              </div>
            </div>
          </div>
        )}

        <div style={{ marginBottom: 24 }}>
          <div className="section-divider">
            <div className="section-label">test results</div>
            <div className="line" />
          </div>
          {s.testOrders.map((t) => <TestCard key={t.testOrderId} test={t} />)}
          {s.testOrders.length === 0 && (
            <div className="section-card"><span style={{ fontSize: 13, color: "#888" }}>No tests recorded.</span></div>
          )}
        </div>

        <div style={{ marginBottom: 24 }}>
          <div className="section-divider">
            <div className="section-label">workflow timeline</div>
            <div className="line" />
          </div>
          <div className="timeline-wrap">
            <div className="timeline-track">
              {steps.map((step, i) => (
                <div key={step.label} style={{ display: "contents" }}>
                  {i > 0 && <div className={`timeline-connector ${step.at ? "" : "is-pending"}`} />}
                  <div className="timeline-step">
                    <div className={`step-dot ${step.at ? (step.danger ? "is-danger" : "") : "is-pending"}`}>
                      {step.at ? (step.danger ? <CrossIcon /> : <CheckIcon strokeWidth={3} />) : <DotIcon />}
                    </div>
                    <div className={`step-label ${step.at ? "" : "is-pending"}`}>{step.label}</div>
                    <div className="step-time">{hhmm(step.at)}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {s.signatures.length > 0 && (
          <div style={{ marginBottom: 24 }}>
            <div className="section-divider">
              <div className="section-label">electronic signatures</div>
              <div className="line" />
            </div>
            <div className="signature-grid">
              {s.signatures.map((sig, i) => (
                <div className="signature-card" key={i}>
                  <div className="sig-header">
                    <div className="sig-icon"><CheckIcon /></div>
                    <div>
                      <div className="sig-name">{sig.printedName}</div>
                      {/* Full names collide across accounts in this lab, so the
                          username is what actually proves reviewer and approver
                          were different people. */}
                      <div className="sig-username">@{sig.username}</div>
                      <div className="sig-role">{humanize(sig.role)}</div>
                    </div>
                  </div>
                  <div className="sig-time">{dt(sig.signedAt)}</div>
                  <div className="sig-meaning">Meaning: {MEANING_TEXT[sig.meaning] ?? humanize(sig.meaning)}</div>
                  {sig.comment && <div className="sig-comment">“{sig.comment}”</div>}
                </div>
              ))}
            </div>
          </div>
        )}

        <ArchivedCopiesSection
          copies={archivedCopies}
          onDownload={(archiveId, fileName) => ArchivedRecordsService.download(archiveId, fileName)}
        />

        <div className="report-footer">
          <div><strong>This is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled.</strong></div>
          <div>Document ID: {s.referenceNumber} · Generated: {generatedAt}</div>
        </div>
      </div>

      <button className="print-btn" onClick={() => window.print()}>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
          <polyline points="6 9 6 2 18 2 18 9" />
          <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
          <rect x="6" y="14" width="12" height="8" />
        </svg>
        Print / Save PDF
      </button>
    </div>
  );
}

// One card per TestOrder. The body varies by what the test actually
// produced - count readings, a pathogen observation chain, a per-location
// EM/After Cleaning batch breakdown, or a plain result - rather than
// forcing every test into the plate-stat shape.
function TestCard({ test }: { test: TestOrderSummaryDetail }) {
  const reading = test.countTestReadings[test.countTestReadings.length - 1];
  const incubation = test.incubations[test.incubations.length - 1];
  const lastResult = test.results[test.results.length - 1];
  const hasLocations = test.locations.length > 0;
  const nonConformingLocation = test.locations.some((l) => l.status && l.status !== "WithinLimits" && l.status !== "Absent");

  const outOfSpec = reading?.status === "OutOfSpecification";
  const detected = test.pathogenObservations.some((p) => p.growthObserved);
  const tone = test.isSuperseded ? "is-neutral" : outOfSpec || detected || nonConformingLocation ? "is-danger" : "";

  // Worst status across all locations, for the compact headline slot -
  // the full "X locations: Y conform..." sentence belongs in the table
  // summary below, not this 22px single-value stat (it would starve the
  // title column of width and wrap it word-by-word).
  const locationSeverity = ["WithinLimits", "Absent", "AlertLimitExceeded", "ActionLimitExceeded", "OutOfSpecification", "Detected"];
  const worstLocationStatus = hasLocations
    ? test.locations.reduce<string>((worst, l) => {
        if (!l.status) return worst;
        return locationSeverity.indexOf(l.status) > locationSeverity.indexOf(worst) ? l.status : worst;
      }, "WithinLimits")
    : null;

  const headline = hasLocations ? humanize(worstLocationStatus) : reading?.reportedResult
    ?? (test.pathogenObservations.length > 0 ? (detected ? "Detected" : "Absent") : null)
    ?? lastResult?.interpretedValue ?? lastResult?.rawValue ?? "—";

  const unitLine = hasLocations
    ? `${test.locations.length} location${test.locations.length === 1 ? "" : "s"}`
    : reading ? `CFU · ${humanize(reading.status)}` : test.pathogenObservations.length > 0 ? "Pathogen" : humanize(test.status);

  const enteredBy = reading?.enteredByName ?? lastResult?.enteredByName
    ?? test.pathogenObservations[test.pathogenObservations.length - 1]?.observedByName
    ?? test.locations[test.locations.length - 1]?.enteredByName;
  const enteredAt = reading?.enteredAt ?? lastResult?.enteredAt
    ?? test.pathogenObservations[test.pathogenObservations.length - 1]?.observedAt
    ?? test.locations[test.locations.length - 1]?.enteredAt;

  return (
    <div className={`test-card ${test.isSuperseded ? "is-superseded" : ""}`}>
      <div className="test-header">
        <div className="test-header-left">
          <div className={`test-icon ${tone}`}>
            {test.isSuperseded ? <DotIcon /> : outOfSpec || detected ? <CrossIcon /> : <CheckIcon />}
          </div>
          <div>
            <div className="test-title">{test.testCode} — {test.testDisplayName}</div>
            <div className="test-subtitle">
              {test.isSuperseded && <strong>Superseded by retest · </strong>}
              {incubation ? `Step: ${humanize(incubation.stepName)}` : `Step: ${humanize(test.currentStep)}`}
              {incubation?.mediaLotNumber ? ` · Media lot: ${incubation.mediaLotNumber}` : ""}
            </div>
          </div>
        </div>
        <div>
          <div className={`test-result-value ${tone}`}>{headline}</div>
          <div className="test-result-unit">{unitLine}</div>
        </div>
      </div>

      {incubation && (
        <div className="incubation-row">
          <div className="incubation-item">
            <div className="inc-label">Incubator</div>
            <div className="inc-value">{incubation.incubatorName ?? "—"}</div>
          </div>
          <div className="incubation-item">
            <div className="inc-label">Temperature</div>
            <div className="inc-value">{incubation.temperature ?? "—"}</div>
          </div>
          <div className="incubation-item">
            <div className="inc-label">Duration</div>
            <div className="inc-value">{incubation.duration ?? "—"}</div>
          </div>
          <div className="incubation-item">
            <div className="inc-label">Expected reading</div>
            <div className="inc-value mono">{dt(incubation.expectedReadingAt)}</div>
          </div>
        </div>
      )}

      {reading && (
        <div className="plate-readings">
          <div className="plate-readings-label">plate readings</div>
          <div className="plate-stats">
            <div className="plate-stat">
              <div className="stat-label">Plate count</div>
              <div className="stat-value">{reading.plateReadings}</div>
            </div>
            <div className="plate-stat">
              <div className="stat-label">Dilution</div>
              <div className="stat-value">{reading.dilutionFactor}</div>
            </div>
            <div className="plate-stat">
              <div className="stat-label">Average</div>
              <div className="stat-value">{reading.average}</div>
            </div>
            <div className="plate-stat">
              <div className="stat-label">Calculated</div>
              <div className="stat-value">{reading.calculatedResult}</div>
            </div>
          </div>
          <div className="plate-meta">
            <span>Reported: <strong>{reading.reportedResult}</strong></span>
            <span>Limits (alert/action/spec): <strong>{reading.alertLimit ?? "—"} / {reading.actionLimit ?? "—"} / {reading.specLimit ?? "—"}</strong></span>
            <span>Actual reading: <strong className="mono">{dt(reading.enteredAt)}</strong></span>
          </div>
        </div>
      )}

      {test.pathogenObservations.length > 0 && (
        <div className="observation-row">
          {test.pathogenObservations
            .slice()
            .sort((a, b) => a.stepOrder - b.stepOrder)
            .map((p, i) => (
              <div className="observation-item" key={i}>
                <span className="obs-step">{p.stepName}</span>
                <span>
                  <strong>{p.growthObserved ? "Growth observed" : "No growth"}</strong>
                  <span className="obs-meta"> · {p.observedByName} · {dt(p.observedAt)}</span>
                </span>
              </div>
            ))}
        </div>
      )}

      {hasLocations && (
        <div className="location-table-wrap">
          <table className="location-table">
            <thead>
              <tr>
                <th>Location</th>
                <th>Limits (Alert/Action/Spec)</th>
                <th>CFU</th>
                <th>Reported Result</th>
                <th>Status</th>
                <th>Entered By</th>
              </tr>
            </thead>
            <tbody>
              {test.locations.map((l, i) => (
                <tr key={i}>
                  <td className="loc-name">{l.locationName}{l.gradeClassification ? ` (${l.gradeClassification})` : ""}</td>
                  <td className="loc-limits">{l.alertLimit ?? "—"} / {l.actionLimit ?? "—"} / {l.specLimit ?? "—"}</td>
                  <td className="loc-cfu">{l.cfuResult ?? "—"}</td>
                  <td className="loc-reported">{l.reportedResult ?? "—"}</td>
                  <td>
                    {l.status && (
                      <span className="location-status-chip" style={{ background: LOCATION_STATUS_COLOR[l.status] ?? "#6b7280" }}>
                        {humanize(l.status)}
                      </span>
                    )}
                  </td>
                  <td>{l.enteredByName ? <>{l.enteredByName}<br /><span style={{ color: "var(--color-text-quaternary)", fontSize: 11 }}>{dt(l.enteredAt)}</span></> : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!reading && !hasLocations && test.pathogenObservations.length === 0 && lastResult && (
        <div className="observation-row">
          <div className="observation-item">
            <span className="obs-step">Result</span>
            <span><strong>{lastResult.interpretedValue ?? lastResult.rawValue}</strong></span>
          </div>
        </div>
      )}

      <div className="test-footer">
        <span>
          {enteredBy ? <>Entered by <strong>{enteredBy}</strong> · {dt(enteredAt)}</> : <>No result recorded yet</>}
        </span>
        <span className={`pass-tag ${tone}`}>
          {test.isSuperseded ? <DotIcon /> : outOfSpec || detected || nonConformingLocation ? <CrossIcon /> : <CheckIcon />}
          {test.isSuperseded ? "Superseded"
            : hasLocations ? (nonConformingLocation ? "Non-conforming location(s)" : "All locations conform")
            : humanize(reading?.status ?? (test.pathogenObservations.length > 0 ? (detected ? "Detected" : "Absent") : test.status))}
        </span>
      </div>
    </div>
  );
}
