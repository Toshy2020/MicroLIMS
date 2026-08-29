import { Fragment, useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { SampleSummaryService } from "./services/SampleSummaryService";
import { buildCoaMatrix, buildOverallConclusionText, buildCoaSimpleRows, buildSimpleConclusionText, computeResultDate } from "./coaAggregation";
import { SampleSummary, SignatureTrailItem } from "./types/sampleSummaryTypes";
import { CoaColumn } from "./coaAggregation";
import { reportStyles } from "./reportStyles";
import { dt, d, humanize } from "./SampleReportPage";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";

// Duplicated in SampleReportPage.MEANING_TEXT and SampleSummaryDialog.
// SIGNATURE_STATEMENTS too - same small map, three places, matching how
// the rest of this module already keeps local copies of one-line rules
// rather than importing across files for a two-entry lookup.
const MEANING_TEXT: Record<string, string> = {
  Reviewed: "I have reviewed the test data and confirm it is complete and accurate.",
  Approved: "I approve the release of this sample for its intended use.",
  Rejected: "I reject the release of this sample; it does not conform to specification."
};

// Every qualitative (pathogen) test on a Water sample shares the same
// absence requirement - no per-test variation exists anywhere in the
// schema (see coaAggregation.ts). Repeating "Spec: Absent / 10 mL" under
// every qualitative test's own column is pure duplication, so adjacent
// qualitative columns share one spanning header cell instead.
const QUALITATIVE_SPEC_LABEL = "Absent / 10 mL";

// Second header row: quantitative tests keep their own four sub-columns;
// consecutive qualitative tests collapse into a single spanning cell so
// the shared spec text isn't repeated once per organism.
function renderSubHeaderRow(columns: CoaColumn[]) {
  const cells: JSX.Element[] = [];
  let i = 0;
  while (i < columns.length) {
    const c = columns[i];
    if (c.isQuantitative) {
      cells.push(
        <Fragment key={c.testOrderId}>
          <th className="sub">Alert</th>
          <th className="sub">Action</th>
          <th className="sub">Spec</th>
          <th className="sub">
            Result
            {c.unit && <div className="coa-unit-sub">{c.unit}</div>}
          </th>
        </Fragment>
      );
      i++;
      continue;
    }
    const groupStart = i;
    while (i < columns.length && !columns[i].isQuantitative) i++;
    cells.push(
      <th key={`qual-${columns[groupStart].testOrderId}`} className="sub spec-req" colSpan={i - groupStart}>
        Spec: {QUALITATIVE_SPEC_LABEL}
      </th>
    );
  }
  return cells;
}

function lastSignatureByMeaning(signatures: SignatureTrailItem[], meaning: string): SignatureTrailItem | undefined {
  for (let i = signatures.length - 1; i >= 0; i--) {
    if (signatures[i].meaning === meaning) return signatures[i];
  }
  return undefined;
}

function SignatureBlock({
  fallbackRole,
  sig,
  fallbackName,
  fallbackAt,
  fallbackMeaning
}: {
  fallbackRole: string;
  sig: SignatureTrailItem | undefined;
  fallbackName: string | null;
  fallbackAt: string | null;
  fallbackMeaning?: string;
}) {
  const name = sig?.printedName ?? fallbackName;
  const meaning = sig?.meaning ?? fallbackMeaning ?? (fallbackRole === "Reviewer" ? "Reviewed" : "Approved");
  const at = sig?.signedAt ?? fallbackAt;
  return (
    <div className="coa-sig-block">
      <div className="sn">{name ?? "—"}</div>
      <div className="sr">{sig ? humanize(sig.role) : fallbackRole}</div>
      <div className="sm">"{MEANING_TEXT[meaning] ?? humanize(meaning)}"</div>
      <div className="st">{dt(at)}</div>
    </div>
  );
}

export function SampleCoaPage() {
  const { id } = useParams();
  const [summary, setSummary] = useState<SampleSummary | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    SampleSummaryService.getSummary(Number(id))
      .then(setSummary)
      .catch((e) => setError(e?.response?.data?.message ?? "Failed to load the certificate of analysis."));
  }, [id]);

  useEffect(() => {
    if (summary) document.title = `Certificate of Analysis - ${summary.referenceNumber}`;
  }, [summary]);

  if (error) return <PinnedLightTheme><div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#dc2626" }}>{error}</div></PinnedLightTheme>;
  if (!summary) return <PinnedLightTheme><div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#666" }}>Loading certificate…</div></PinnedLightTheme>;

  const s = summary;
  const matrix = buildCoaMatrix(s.testOrders);
  // Product/RM/PM branch - only computed when the sample has no located
  // tests at all, mirroring buildCoaMatrix's own discriminator so the two
  // branches are mutually exclusive by construction.
  const simple = matrix ? null : buildCoaSimpleRows(s.testOrders);

  if (s.status !== "Approved" && s.status !== "Rejected") {
    return (
      <PinnedLightTheme>
        <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#666" }}>
          A Certificate of Analysis is only available once this sample has been approved or rejected. Current status: {humanize(s.status)}.
        </div>
      </PinnedLightTheme>
    );
  }

  if (!matrix && !simple) {
    return (
      <PinnedLightTheme>
        <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#666" }}>
          A Certificate of Analysis is not available for this sample - none of its tests have recorded results yet.
        </div>
      </PinnedLightTheme>
    );
  }

  const reviewerSig = lastSignatureByMeaning(s.signatures, "Reviewed");
  // A sample resolved via an OOS retest chain (RetestRetainedSample /
  // NewSampleRequest) never gets its own Approved/Rejected signature for
  // the mirrored outcome - its own signature is from the earlier decision
  // that sent it to retest in the first place (e.g. NewSampleRequest
  // always signs "Rejected", even when the chain later resolves Approved),
  // so looking it up here would show the wrong decision. Only a sample
  // that was itself directly Approved/Rejected has a signature worth
  // showing; the propagated case falls back to approvedByName/approvedAt.
  const isPropagatedOutcome = s.approvalDecision === "RetestRetainedSample" || s.approvalDecision === "NewSampleRequest";
  const approverFallbackMeaning = s.status === "Rejected" ? "Rejected" : "Approved";
  const approverSig = isPropagatedOutcome ? undefined : lastSignatureByMeaning(s.signatures, approverFallbackMeaning);
  const generatedAt = dt(new Date().toISOString());
  const conclusionText = matrix ? buildOverallConclusionText(matrix) : buildSimpleConclusionText(simple!);
  const overallComplies = matrix ? matrix.overallComplies : simple!.overallComplies;
  const resultDate = matrix ? null : computeResultDate(s.testOrders);
  const remarksText = s.certificateRemarks?.trim() || null;

  return (
    <PinnedLightTheme>
      <div className="coa-root">
        <style>{reportStyles}</style>

        <div className="coa-page">
          <div className="coa-head">
            <div>
              <div className="coa-title">Certificate of Analysis</div>
              <div className="coa-sub">
                {humanize(s.category)}
                {s.displayName ? <> · {s.displayName}</> : ""}
                {s.batchNumber ? ` · Batch: ${s.batchNumber}` : ""}
              </div>
            </div>
            <div className="coa-doc-id">
              {s.referenceNumber}<br />
              Generated {generatedAt}
            </div>
          </div>

          {matrix ? (
            <>
              <div className="coa-id-strip">
                <div><div className="il">Reference</div><div className="iv">{s.referenceNumber}</div></div>
                <div><div className="il">Category</div><div className="iv">{humanize(s.category)}</div></div>
                <div><div className="il">Batch / Control</div><div className="iv">{s.batchNumber ?? s.controlNumber}</div></div>
                <div><div className="il">Received</div><div className="iv">{d(s.receivedAt)}</div></div>
              </div>

              <div className="coa-section-h">Test Results by Location</div>
              <div style={{ overflowX: "auto" }}>
                <table className="coa-matrix">
                  <thead>
                    <tr>
                      <th className="loc-col" rowSpan={2}>Location</th>
                      {matrix.columns.map((c) => (
                        <th key={c.testOrderId} className="grp" colSpan={c.isQuantitative ? 4 : 1}>{c.testCode}</th>
                      ))}
                    </tr>
                    <tr>{renderSubHeaderRow(matrix.columns)}</tr>
                  </thead>
                  <tbody>
                    {matrix.rows.map((r) => (
                      <tr key={r.locationKey}>
                        <td className="loc-col">{r.locationName}</td>
                        {r.cells.map((cell, i) => {
                          const col = matrix.columns[i];
                          if (!cell) {
                            return col.isQuantitative ? (
                              <Fragment key={col.testOrderId}>
                                <td>—</td>
                                <td>—</td>
                                <td>—</td>
                                <td>—</td>
                              </Fragment>
                            ) : (
                              <td key={col.testOrderId}>—</td>
                            );
                          }
                          if (cell.kind === "quantitative") {
                            return (
                              <Fragment key={col.testOrderId}>
                                <td className="lim-dim">{cell.alert ?? "—"}</td>
                                <td className="lim-dim">{cell.action ?? "—"}</td>
                                <td className="lim-dim">{cell.spec ?? "—"}</td>
                                <td className={cell.conform ? "r-pass" : "r-fail"}>{cell.result}</td>
                              </Fragment>
                            );
                          }
                          return (
                            <td key={col.testOrderId} className={cell.conform ? "r-pass" : "r-fail"}>{cell.result}</td>
                          );
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="coa-footnote">
                {matrix.units.length > 0 && <>Alert / Action / Spec shown in {matrix.units.join(", ")}, configured per sampling location. </>}
                "Spec: {QUALITATIVE_SPEC_LABEL}" indicates absence is required by method in a 10 mL sample; the Result column states the actual finding for that location.
              </div>
            </>
          ) : (
            <>
              <div className="coa-item-strip">
                <div className="coa-item-name-row">
                  <div>
                    <div className="coa-item-name">{s.displayName}</div>
                    <div className="coa-item-sub">{humanize(s.category)}</div>
                  </div>
                  {s.sampleQuantity && <div className="coa-item-qty">Qty: {s.sampleQuantity}</div>}
                </div>
                <div className="coa-dates-grid">
                  <div><div className="il">Batch No.</div><div className="iv">{s.batchNumber ?? "—"}</div></div>
                  <div><div className="il">Mfg. Date</div><div className="iv">{d(s.mfgDate)}</div></div>
                  <div><div className="il">Exp. Date</div><div className="iv">{d(s.expDate)}</div></div>
                  <div><div className="il">Sampling / Arrival</div><div className="iv">{d(s.receivedAt)}</div></div>
                  <div><div className="il">Test Date</div><div className="iv">{d(s.preparation?.preparedAt ?? null)}</div></div>
                  <div><div className="il">Result Date</div><div className="iv">{d(resultDate)}</div></div>
                  <div><div className="il">QC No.</div><div className="iv">{s.controlNumber}</div></div>
                  <div><div className="il">Certificate Date</div><div className="iv">{d(s.approvedAt)}</div></div>
                </div>
              </div>

              <div className="coa-section-h">Test Results</div>
              <div style={{ overflowX: "auto" }}>
                <table className="coa-simple">
                  <thead>
                    <tr>
                      <th>Test</th>
                      <th>Specification</th>
                      <th>Sample Result</th>
                      <th>Analyst</th>
                    </tr>
                  </thead>
                  <tbody>
                    {simple!.rows.map((r) => {
                      const sourceRef = s.testOrders.find((t) => t.testOrderId === r.testOrderId)?.sourceSampleReferenceNumber;
                      return (
                      <tr key={r.testOrderId}>
                        <td>
                          {r.testCode} — {r.testDisplayName}
                          {sourceRef && <div style={{ fontSize: 10, color: "var(--coa-ink3)" }}>via retest {sourceRef}</div>}
                        </td>
                        <td>{r.specification ?? "—"}</td>
                        <td className={r.conform ? "r-pass" : "r-fail"}>{r.result}</td>
                        <td>{r.analystName ? <>{r.analystName}<br /><span style={{ color: "var(--coa-ink3)", fontSize: 11 }}>{dt(r.analystAt)}</span></> : "—"}</td>
                      </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              <div className="coa-section-h">Remarks</div>
              <div className={`coa-remarks-box ${remarksText ? "" : "is-empty"}`}>{remarksText ?? "No remarks."}</div>
            </>
          )}

          <div className={`coa-overall ${overallComplies ? "" : "is-fail"}`}>
            <div className="ot">Overall Conclusion</div>
            <div className="od">{conclusionText}</div>
          </div>

          <div className="coa-sig-strip">
            <SignatureBlock fallbackRole="Reviewer" sig={reviewerSig} fallbackName={s.reviewedByName} fallbackAt={s.reviewedAt} />
            <SignatureBlock fallbackRole="Approver" sig={approverSig} fallbackName={s.approvedByName} fallbackAt={s.approvedAt} fallbackMeaning={approverFallbackMeaning} />
          </div>

          <div className="coa-footer-note">
            This Certificate of Analysis is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled.<br />
            Full test detail, per-location results, and incubation records are retained in the Sample Summary Report — Document ID: {s.referenceNumber}.
          </div>
        </div>

        <button className="print-btn no-print" onClick={() => window.print()}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
            <polyline points="6 9 6 2 18 2 18 9" />
            <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
            <rect x="6" y="14" width="12" height="8" />
          </svg>
          Print / Save PDF
        </button>
      </div>
    </PinnedLightTheme>
  );
}
