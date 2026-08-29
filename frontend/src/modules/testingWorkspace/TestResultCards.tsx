import { CollapsibleTestCard, SecondaryToggle } from "./CollapsibleTestCard";
import { CheckIcon, CrossIcon, DotIcon, dt, humanize, LOCATION_STATUS_COLOR } from "./SampleReportPage";
import { IncubationDetail, SampleLocationDetail, TestOrderSummaryDetail } from "./types/sampleSummaryTypes";
import { pathogenObservationLabel } from "./utils/pathogenObservationLabel";

const CONFORMING_STATUSES = new Set(["WithinLimits", "Absent"]);
const isConforming = (status: string | null) => !status || CONFORMING_STATUSES.has(status);

// A CFU count is a whole-number concept - a raw average can carry a long
// repeating decimal (e.g. readings that don't divide evenly), but there's
// no such thing as 0.67 of a colony. Round for display only; the
// stored/reported value (reportedResult) is the actual reported result.
function formatCfu(value: number | null | undefined): string {
  if (value === null || value === undefined) return "—";
  return Math.round(value).toString();
}

// Same rule SummaryMatrix uses: a test is quantitative if it produced a
// CFU number anywhere (a single reading, or per-location counts),
// otherwise it's a qualitative detection call. Kept as its own copy here
// rather than importing SummaryMatrix's - that component is already
// reviewed and working, and this is a small, pure, one-line check.
function isQuantitative(test: TestOrderSummaryDetail): boolean {
  return test.countTestReadings.length > 0 || test.locations.some((l) => l.cfuResult !== null);
}

// Every card this dispatcher can't classify (neither locations, a count
// reading, nor a pathogen chain - a plain single Result) still needs a
// home: DetectionTestCard's bare-fallback branch covers it, since a bare
// result is conceptually a qualitative call too ("the recorded value"),
// never a computed CFU.
export function TestResultCard({ test }: { test: TestOrderSummaryDetail }) {
  return isQuantitative(test) ? <CountTestCard test={test} /> : <DetectionTestCard test={test} />;
}

function subtitle(test: TestOrderSummaryDetail, incubation: IncubationDetail | undefined) {
  return (
    <>
      {test.isSuperseded && <strong>Superseded by retest · </strong>}
      {incubation ? `Step: ${humanize(incubation.stepName)}` : `Step: ${humanize(test.currentStep)}`}
      {incubation?.mediaLotNumber ? ` · Media lot: ${incubation.mediaLotNumber}` : ""}
    </>
  );
}

// Shared by both card types - identical markup/data to the original
// always-expanded incubation loop, just relocated behind the "Show
// incubation stage details" toggle instead of always being on screen.
function IncubationStages({ incubations }: { incubations: IncubationDetail[] }) {
  return (
    <>
      {incubations.map((inc, i) => {
        const isStage1Transferred = inc.stageNumber === 1 && (incubations.length > 1 || !!inc.transferredAt || !!inc.transferredByName);
        const isStage2 = inc.stageNumber === 2;
        const stageTitle = isStage1Transferred
          ? `Stage 1 Incubation · ${humanize(inc.stepName)}`
          : isStage2
          ? `Stage 2 Incubation · ${humanize(inc.stepName)}`
          : `Incubation · ${humanize(inc.stepName)}`;

        return (
          <div key={i} style={{ borderTop: i > 0 ? "1px solid var(--color-border)" : undefined, marginTop: i > 0 ? 8 : 0 }}>
            {incubations.length > 1 && (
              <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", color: "var(--color-text-tertiary)", marginBottom: 6 }}>
                {stageTitle}
              </div>
            )}
            <div className="incubation-row" style={{ border: "1px solid var(--color-border)", borderRadius: 8 }}>
              <div className="incubation-item">
                <div className="inc-label">Media Lot</div>
                <div className="inc-value">{inc.mediaLotNumber ? `${inc.mediaLotNumber} (${inc.mediaMaterialName ?? "—"})` : "—"}</div>
              </div>
              <div className="incubation-item">
                <div className="inc-label">Incubator</div>
                <div className="inc-value">{inc.incubatorName ?? "—"}</div>
              </div>
              <div className="incubation-item">
                <div className="inc-label">Temperature</div>
                <div className="inc-value">{inc.temperature ?? "—"}</div>
              </div>
              <div className="incubation-item">
                <div className="inc-label">Duration</div>
                <div className="inc-value">{inc.duration ?? "—"}</div>
              </div>
              <div className="incubation-item">
                <div className="inc-label">Started At</div>
                <div className="inc-value mono">{dt(inc.startedAt)}{inc.startedByName ? ` · ${inc.startedByName}` : ""}</div>
              </div>
              {isStage1Transferred ? (
                <>
                  <div className="incubation-item">
                    <div className="inc-label">Transferred At</div>
                    <div className="inc-value mono">{dt(inc.transferredAt ?? inc.completedAt)}</div>
                  </div>
                  <div className="incubation-item">
                    <div className="inc-label">Transferred By</div>
                    <div className="inc-value">{inc.transferredByName ?? "—"}</div>
                  </div>
                </>
              ) : (
                <>
                  <div className="incubation-item">
                    <div className="inc-label">Completed At</div>
                    <div className="inc-value mono">{dt(inc.completedAt)}</div>
                  </div>
                  <div className="incubation-item">
                    <div className="inc-label">Completed By</div>
                    <div className="inc-value">{inc.completedByName ?? "—"}</div>
                  </div>
                </>
              )}
              {inc.outcome && (
                <div className="incubation-item" style={{ gridColumn: "span 2" }}>
                  <div className="inc-label">Outcome</div>
                  <div className="inc-value">{inc.outcome}</div>
                </div>
              )}
            </div>
          </div>
        );
      })}
    </>
  );
}

// Unchanged from the original location-table markup - just relocated
// behind the "Show full location table" toggle (decision A) instead of
// always rendering, so every column (Limits, CFU, Reported Result,
// Status, Entered By) stays reachable.
function FullLocationTable({ locations }: { locations: SampleLocationDetail[] }) {
  // Unit comes from the location data (set at result-entry time) - EM/
  // After Cleaning/Water mix CFU/plate/4 hours, CFU/25 cm2, and CFU/mL
  // depending on sampling method, never a single assumed "CFU".
  const unit = locations.find((l) => l.unit)?.unit ?? "CFU";
  return (
    <div className="location-table-wrap" style={{ border: "1px solid var(--color-border)", borderRadius: 8 }}>
      <table className="location-table">
        <thead>
          <tr>
            <th>Location</th>
            <th>Limits (Alert/Action/Spec)</th>
            <th>{unit}</th>
            <th>Reported Result</th>
            <th>Status</th>
            <th>Entered By</th>
          </tr>
        </thead>
        <tbody>
          {locations.map((l, i) => (
            <tr key={i}>
              <td className="loc-name">{l.locationName}{l.gradeClassification ? ` (${l.gradeClassification})` : ""}</td>
              <td className="loc-limits">{l.alertLimit ?? "—"} / {l.actionLimit ?? "—"} / {l.specLimit ?? "—"}</td>
              <td className="loc-cfu">{formatCfu(l.cfuResult)}</td>
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
  );
}

function CardFooter({ enteredBy, enteredAt }: { enteredBy: string | null | undefined; enteredAt: string | null | undefined }) {
  return (
    <div style={{ fontSize: 11, color: "var(--color-text-tertiary)", marginTop: 12, paddingTop: 10, borderTop: "1px solid var(--color-border)" }}>
      {enteredBy ? <>Entered by <strong style={{ color: "var(--color-text-primary)" }}>{enteredBy}</strong> · {dt(enteredAt)}</> : "No result recorded yet"}
    </div>
  );
}

// Qualitative tests: a per-location Detected/Absent batch (BCC, E.coli,
// ...), a single pathogen observation chain (no locations), or a bare
// single Result with neither - covers every non-quantitative shape the
// original TestCard handled.
function DetectionTestCard({ test }: { test: TestOrderSummaryDetail }) {
  const incubation = test.incubations[test.incubations.length - 1];
  const lastResult = test.results[test.results.length - 1];
  const hasLocations = test.locations.length > 0;

  const nonConformingLocation = test.locations.some((l) => !isConforming(l.status));
  // Biochemical identification's explicit Detected/Not-Detected call is
  // authoritative when present - it overrides the selective-plating
  // morphology alone, the same override applied on the backend (see
  // ReportDocumentMapper.ToTestCard). A chain can show GrowthConforming at
  // selective plating and still resolve to absent once biochemical testing
  // rules the organism out.
  const lastBiochemical = test.biochemicalResults[test.biochemicalResults.length - 1];
  const detected = lastBiochemical?.organismDetected
    ?? test.pathogenObservations.some((p) => p.observation === "GrowthConforming");
  const hasException = !test.isSuperseded && (detected || nonConformingLocation);
  const tone = test.isSuperseded ? "is-neutral" : hasException ? "is-danger" : "";

  const exceptionCount = test.locations.filter((l) => !isConforming(l.status)).length;
  const badgeText = test.isSuperseded
    ? "Superseded"
    : hasLocations
    ? (exceptionCount > 0 ? `${exceptionCount} exception${exceptionCount === 1 ? "" : "s"}` : "Within limits")
    : humanize(test.pathogenObservations.length > 0 ? (detected ? "Detected" : "Absent") : test.status);

  const enteredBy = lastResult?.enteredByName
    ?? test.pathogenObservations[test.pathogenObservations.length - 1]?.observedByName
    ?? test.locations[test.locations.length - 1]?.enteredByName;
  const enteredAt = lastResult?.enteredAt
    ?? test.pathogenObservations[test.pathogenObservations.length - 1]?.observedAt
    ?? test.locations[test.locations.length - 1]?.enteredAt;

  return (
    <CollapsibleTestCard
      icon={test.isSuperseded ? <DotIcon /> : hasException ? <CrossIcon /> : <CheckIcon />}
      iconTone={tone}
      title={`${test.testCode} — ${test.testDisplayName}`}
      subtitle={subtitle(test, incubation)}
      locationCount={hasLocations ? test.locations.length : undefined}
      badgeText={badgeText}
      badgeTone={tone}
      defaultOpen={hasException}
      isSuperseded={test.isSuperseded}
    >
      {/* Independent of hasLocations - mirrors the original markup,
          which rendered this whenever a pathogen chain existed, whether
          or not per-location results were also present. */}
      {test.pathogenObservations.length > 0 && (
        <div style={{ marginBottom: 12 }}>
          {test.pathogenObservations
            .slice()
            .sort((a, b) => a.stepOrder - b.stepOrder)
            .map((p, i) => (
              <div key={i} style={{ fontSize: 12, display: "flex", justifyContent: "space-between", padding: "4px 0" }}>
                <span style={{ color: "var(--color-text-tertiary)" }}>{p.stepName}</span>
                <span>
                  <strong>{pathogenObservationLabel(p.observation)}</strong>
                  <span style={{ color: "var(--color-text-quaternary)" }}> · {p.observedByName} · {dt(p.observedAt)}</span>
                </span>
              </div>
            ))}
        </div>
      )}

      {test.biochemicalResults.length > 0 && (
        <div style={{ marginBottom: 12 }}>
          {test.biochemicalResults.map((b, i) => (
            <div key={i} style={{ fontSize: 12, padding: "4px 0" }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "var(--color-text-tertiary)" }}>{b.stepName}</span>
                <span>
                  <strong>{b.organismDetected === true ? "Detected" : b.organismDetected === false ? "Not Detected" : "Undetermined"}</strong>
                  <span style={{ color: "var(--color-text-quaternary)" }}> · {b.submittedByName} · {dt(b.submittedAt)}</span>
                </span>
              </div>
              <div style={{ color: "var(--color-text-tertiary)", marginTop: 2 }}>{b.biochemicalResultText}</div>
            </div>
          ))}
        </div>
      )}

      {!hasLocations && test.pathogenObservations.length === 0 && lastResult && (
        <div style={{ fontSize: 13, marginBottom: 12 }}>
          <strong>{lastResult.interpretedValue ?? lastResult.rawValue}</strong>
        </div>
      )}

      {test.incubations.length > 0 && (
        <SecondaryToggle label={`Show incubation stage details${test.incubations.length > 1 ? ` (${test.incubations.length} stages)` : ""}`}>
          <IncubationStages incubations={test.incubations} />
        </SecondaryToggle>
      )}
      {hasLocations && (
        <SecondaryToggle
          label="Show full location table"
          collapsedContent={
            <div className="result-pills">
              {test.locations.map((l, i) => {
                const conform = isConforming(l.status);
                return (
                  <span key={i} className={`result-pill ${conform ? "" : "is-danger"}`}>
                    {l.locationName} {conform ? "✓" : "✗"}
                  </span>
                );
              })}
            </div>
          }
        >
          <FullLocationTable locations={test.locations} />
        </SecondaryToggle>
      )}

      <CardFooter enteredBy={enteredBy} enteredAt={enteredAt} />
    </CollapsibleTestCard>
  );
}

// Quantitative tests: a per-location CFU batch (TAMC-Water) or a single
// CountTestReading with plate-count/dilution/average detail (plain
// TAMC/TYMC, no locations).
function CountTestCard({ test }: { test: TestOrderSummaryDetail }) {
  const reading = test.countTestReadings[test.countTestReadings.length - 1];
  const incubation = test.incubations[test.incubations.length - 1];
  const hasLocations = test.locations.length > 0;

  const outOfSpec = reading?.status === "OutOfSpecification";
  const nonConformingLocation = test.locations.some((l) => !isConforming(l.status));
  const hasException = !test.isSuperseded && (outOfSpec || nonConformingLocation);
  const tone = test.isSuperseded ? "is-neutral" : hasException ? "is-danger" : "";

  const exceptionCount = test.locations.filter((l) => !isConforming(l.status)).length;
  const badgeText = test.isSuperseded
    ? "Superseded"
    : hasLocations
    ? (exceptionCount > 0 ? `${exceptionCount} exception${exceptionCount === 1 ? "" : "s"}` : "Within limits")
    : humanize(reading?.status ?? test.status);

  const enteredBy = reading?.enteredByName ?? test.locations[test.locations.length - 1]?.enteredByName;
  const enteredAt = reading?.enteredAt ?? test.locations[test.locations.length - 1]?.enteredAt;

  return (
    <CollapsibleTestCard
      icon={test.isSuperseded ? <DotIcon /> : hasException ? <CrossIcon /> : <CheckIcon />}
      iconTone={tone}
      title={`${test.testCode} — ${test.testDisplayName}`}
      subtitle={subtitle(test, incubation)}
      locationCount={hasLocations ? test.locations.length : undefined}
      badgeText={badgeText}
      badgeTone={tone}
      defaultOpen={hasException}
      isSuperseded={test.isSuperseded}
    >
      {test.incubations.length > 0 && (
        <SecondaryToggle label={`Show incubation stage details${test.incubations.length > 1 ? ` (${test.incubations.length} stages)` : ""}`}>
          <IncubationStages incubations={test.incubations} />
          {reading && (
            <div className="plate-readings" style={{ border: "1px solid var(--color-border)", borderRadius: 8, marginTop: 8 }}>
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
                  <div className="stat-value">{formatCfu(reading.average)}</div>
                </div>
                <div className="plate-stat">
                  <div className="stat-label">Calculated</div>
                  <div className="stat-value">{formatCfu(reading.calculatedResult)}</div>
                </div>
              </div>
              <div className="plate-meta">
                <span>Reported: <strong>{reading.reportedResult}</strong></span>
                <span>Limits (alert/action/spec): <strong>{reading.alertLimit ?? "—"} / {reading.actionLimit ?? "—"} / {reading.specLimit ?? "—"}</strong></span>
                <span>Entered by: <strong>{reading.enteredByName}</strong></span>
                <span>Entered at: <strong className="mono">{dt(reading.enteredAt)}</strong></span>
              </div>
            </div>
          )}
        </SecondaryToggle>
      )}
      {hasLocations && (
        <SecondaryToggle
          label="Show full location table"
          collapsedContent={
            <div className="result-pills">
              {test.locations.map((l, i) => {
                const conform = isConforming(l.status);
                const value = formatCfu(l.cfuResult ?? l.calculatedResult);
                return (
                  <span key={i} className={`result-pill ${conform ? "" : "is-danger"}`}>
                    {l.locationName}: {value} {l.unit ?? "CFU"}
                  </span>
                );
              })}
            </div>
          }
        >
          <FullLocationTable locations={test.locations} />
        </SecondaryToggle>
      )}

      <CardFooter enteredBy={enteredBy} enteredAt={enteredAt} />
    </CollapsibleTestCard>
  );
}
