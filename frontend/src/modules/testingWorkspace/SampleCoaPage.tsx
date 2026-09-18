import { Fragment, useEffect, useState, type JSX } from "react";
import { useParams } from "react-router-dom";
import { Box, ToggleButtonGroup, ToggleButton, Alert, Typography } from "@mui/material";
import { SampleSummaryService } from "./services/SampleSummaryService";
import {
  buildCoaMatrix,
  buildOverallConclusionText,
  buildCoaSimpleRows,
  buildSimpleConclusionText,
  computeResultDate,
  filterTestOrdersBySection,
  groupTestOrdersBySection,
  CoaMatrix,
  CoaSimpleResult,
  CoaColumn
} from "./coaAggregation";
import {
  SampleSummary,
  SignatureTrailItem,
  TestOrderSummaryDetail,
  SampleSectionSummaryDetail
} from "./types/sampleSummaryTypes";
import { reportStyles } from "./reportStyles";
import { dt, d, humanize } from "./SampleReportPage";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";

// Certificate-only print overrides, deliberately kept out of reportStyles:
// that stylesheet is shared with the media and cryovial reports, and this
// route is the only one that should lose its @page margins.
//
// A certificate is a controlled document, so the browser's own header and
// footer do not belong on it - they stamp the print time, the tab title, the
// page number and the full URL over a record that already carries its own
// "Generated" line and Document ID. Chromium only omits them at margin: 0.
//
// A named @page was tried first and was wrong: changing the page name on an
// element forces a page break before it, so the certificate opened with a
// blank sheet. Because this component mounts alone on its route and injects
// its own styles, overriding the default @page here reaches nothing else.
//
// The margins come back as padding. That applies once rather than per page,
// so a certificate long enough to break onto a second page would start it at
// the paper edge; certificates are a single page today. The reader can also
// re-enable headers from the print dialog - this sets the default, it cannot
// override the browser.
const coaPrintStyles = `
@media print {
  @page { size: A4 portrait; margin: 0; }
  .coa-page { padding: 18mm 16mm 22mm 16mm !important; }
  .no-print { display: none !important; }
}
`;

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

function CoaMatrixTable({ matrix }: { matrix: CoaMatrix }) {
  return (
    <>
      <div style={{ overflowX: "auto" }}>
        <table className="coa-matrix">
          <thead>
            <tr>
              <th className="loc-col" rowSpan={2}>Location</th>
              {matrix.columns.map((c) => (
                <th key={c.testOrderId} className="grp" colSpan={c.isQuantitative ? 4 : 1}>
                  {c.testDisplayName || c.testCode}
                </th>
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
                    <td key={col.testOrderId} className={cell.conform ? "r-pass" : "r-fail"}>
                      {cell.result}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="coa-footnote">
        {matrix.units.length > 0 && (
          <>Alert / Action / Spec shown in {matrix.units.join(", ")}, configured per sampling location. </>
        )}
        "Spec: {QUALITATIVE_SPEC_LABEL}" indicates absence is required by method in a 10 mL sample; the Result column states the actual finding for that location.
      </div>
    </>
  );
}

function CoaSimpleTable({ simple, testOrders }: { simple: CoaSimpleResult; testOrders: TestOrderSummaryDetail[] }) {
  return (
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
          {simple.rows.map((r) => {
            const sourceRef = testOrders.find((t) => t.testOrderId === r.testOrderId)?.sourceSampleReferenceNumber;
            return (
              <tr key={r.testOrderId}>
                <td>
                  {r.testDisplayName || r.testCode}
                  {sourceRef && (
                    <div style={{ fontSize: 10, color: "var(--coa-ink3)" }}>
                      via retest {sourceRef}
                    </div>
                  )}
                </td>
                <td>{r.specification ?? "—"}</td>
                <td className={r.conform ? "r-pass" : "r-fail"}>{r.result}</td>
                <td>
                  {r.analystName ? (
                    <>
                      {r.analystName}
                      <br />
                      <span style={{ color: "var(--coa-ink3)", fontSize: 11 }}>
                        {dt(r.analystAt)}
                      </span>
                    </>
                  ) : (
                    "—"
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

export function SampleCoaPage() {
  const { id } = useParams();
  const [summary, setSummary] = useState<SampleSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectedVariant, setSelectedVariant] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    SampleSummaryService.getSummary(Number(id))
      .then(setSummary)
      .catch((e) => setError(e?.response?.data?.message ?? "Failed to load the certificate of analysis."));
  }, [id]);

  const isMultiSection = Boolean(summary?.sections && summary.sections.length > 1);

  const isCombinedEligible = Boolean(
    isMultiSection &&
    summary?.allSectionsVisible &&
    summary?.sections &&
    summary.sections.length > 0 &&
    summary.sections.every((sec) => sec.status === "Approved")
  );

  const eligibleSections: SampleSectionSummaryDetail[] = isMultiSection && summary?.sections
    ? summary.sections.filter((sec) => sec.canView && sec.status === "Approved")
    : [];

  const defaultVariant = isCombinedEligible
    ? "combined"
    : eligibleSections.length > 0
    ? String(eligibleSections[0].sectionId)
    : null;

  const activeVariant =
    selectedVariant &&
    (selectedVariant === "combined"
      ? isCombinedEligible
      : eligibleSections.some((sec) => String(sec.sectionId) === selectedVariant))
      ? selectedVariant
      : defaultVariant;

  const activeSection =
    isMultiSection && activeVariant && activeVariant !== "combined"
      ? eligibleSections.find((sec) => String(sec.sectionId) === activeVariant) ?? null
      : null;

  useEffect(() => {
    if (!summary) return;
    if (activeSection) {
      document.title = `Certificate of Analysis - ${activeSection.sectionName} - ${summary.referenceNumber}`;
    } else {
      document.title = `Certificate of Analysis - ${summary.referenceNumber}`;
    }
  }, [summary, activeSection]);

  if (error) {
    return (
      <PinnedLightTheme>
        <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#dc2626" }}>
          {error}
        </div>
      </PinnedLightTheme>
    );
  }

  if (!summary) {
    return (
      <PinnedLightTheme>
        <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#666" }}>
          Loading certificate…
        </div>
      </PinnedLightTheme>
    );
  }

  const s = summary;
  const matrix = buildCoaMatrix(s.testOrders);
  const simple = matrix ? null : buildCoaSimpleRows(s.testOrders);

  if (!isMultiSection) {
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
  }

  if (isMultiSection && !activeVariant) {
    const unapprovedSections = (s.sections ?? []).filter((sec) => sec.status !== "Approved");
    return (
      <PinnedLightTheme>
        <Box sx={{ maxWidth: 800, margin: "40px auto", px: 3 }}>
          <Alert severity="info">
            <Typography sx={{ fontWeight: 600, mb: 1 }}>
              Certificate of Analysis Not Available
            </Typography>
            <Typography variant="body2" sx={{ mb: unapprovedSections.length > 0 ? 1 : 0 }}>
              {unapprovedSections.length > 0
                ? "A Certificate of Analysis is not available yet. The following sections are not yet approved:"
                : "No approved laboratory sections are currently accessible."}
            </Typography>
            {unapprovedSections.length > 0 && (
              <ul style={{ margin: "8px 0 0 0", paddingLeft: 20 }}>
                {unapprovedSections.map((sec) => (
                  <li key={sec.sectionId} style={{ fontSize: 13, marginTop: 4 }}>
                    <strong>{sec.sectionName}</strong>: {humanize(sec.status)}
                  </li>
                ))}
              </ul>
            )}
          </Alert>
        </Box>
      </PinnedLightTheme>
    );
  }

  const reviewerSig = lastSignatureByMeaning(s.signatures, "Reviewed");
  const isPropagatedOutcome = s.approvalDecision === "RetestRetainedSample" || s.approvalDecision === "NewSampleRequest";
  const approverFallbackMeaning = s.status === "Rejected" ? "Rejected" : "Approved";
  const approverSig = isPropagatedOutcome ? undefined : lastSignatureByMeaning(s.signatures, approverFallbackMeaning);
  const generatedAt = dt(new Date().toISOString());

  const coaTitle = activeSection
    ? `Certificate of Analysis - ${activeSection.sectionName}`
    : "Certificate of Analysis";

  return (
    <PinnedLightTheme>
      <div className="coa-root">
        <style>{reportStyles}</style>
        <style>{coaPrintStyles}</style>

        {isMultiSection && (
          <Box
            className="no-print"
            sx={{
              display: "flex",
              flexDirection: "row",
              alignItems: "center",
              justifyContent: "center",
              gap: 2,
              pt: 2.5,
              pb: 1
            }}
          >
            <Typography variant="body2" sx={{ fontWeight: 600, color: "var(--coa-ink2)" }}>
              Certificate Variant:
            </Typography>
            <ToggleButtonGroup
              value={activeVariant}
              exclusive
              size="small"
              onChange={(_, val) => {
                if (val) setSelectedVariant(val);
              }}
              aria-label="Certificate variant"
              sx={{ bgcolor: "#fff" }}
            >
              {isCombinedEligible && (
                <ToggleButton value="combined" sx={{ px: 2, py: 0.5, fontWeight: 600, textTransform: "none" }}>
                  Combined
                </ToggleButton>
              )}
              {eligibleSections.map((sec) => (
                <ToggleButton
                  key={sec.sectionId}
                  value={String(sec.sectionId)}
                  sx={{ px: 2, py: 0.5, fontWeight: 600, textTransform: "none" }}
                >
                  {sec.sectionName}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          </Box>
        )}

        <div className="coa-page">
          <div className="coa-head">
            <div>
              <div className="coa-title">{coaTitle}</div>
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

          {!isMultiSection ? (
            /* Single-section sample (sections.length <= 1) - EXACTLY as today */
            <>
              {matrix ? (
                <>
                  <div className="coa-id-strip">
                    <div><div className="il">Reference</div><div className="iv">{s.referenceNumber}</div></div>
                    <div><div className="il">Category</div><div className="iv">{humanize(s.category)}</div></div>
                    <div><div className="il">Batch / Control</div><div className="iv">{s.batchNumber ?? s.controlNumber}</div></div>
                    <div><div className="il">Received</div><div className="iv">{d(s.receivedAt)}</div></div>
                  </div>

                  <div className="coa-section-h">Test Results by Location</div>
                  <CoaMatrixTable matrix={matrix} />
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
                      <div><div className="il">Result Date</div><div className="iv">{d(computeResultDate(s.testOrders))}</div></div>
                      <div><div className="il">QC No.</div><div className="iv">{s.controlNumber}</div></div>
                      <div><div className="il">Certificate Date</div><div className="iv">{d(s.approvedAt)}</div></div>
                    </div>
                  </div>

                  <div className="coa-section-h">Test Results</div>
                  <CoaSimpleTable simple={simple!} testOrders={s.testOrders} />

                  <div className="coa-section-h">Remarks</div>
                  {(() => {
                    const remarks = s.sections?.[0]?.certificateRemarks?.trim() || s.certificateRemarks?.trim() || null;
                    return (
                      <div className={`coa-remarks-box ${remarks ? "" : "is-empty"}`}>
                        {remarks ?? "No remarks."}
                      </div>
                    );
                  })()}
                </>
              )}

              <div className={`coa-overall ${(matrix ? matrix.overallComplies : simple!.overallComplies) ? "" : "is-fail"}`}>
                <div className="ot">Overall Conclusion</div>
                <div className="od">
                  {matrix ? buildOverallConclusionText(matrix) : buildSimpleConclusionText(simple!)}
                </div>
              </div>

              <div className="coa-sig-strip">
                <SignatureBlock fallbackRole="Reviewer" sig={reviewerSig} fallbackName={s.reviewedByName} fallbackAt={s.reviewedAt} />
                <SignatureBlock fallbackRole="Approver" sig={approverSig} fallbackName={s.approvedByName} fallbackAt={s.approvedAt} fallbackMeaning={approverFallbackMeaning} />
              </div>
            </>
          ) : activeVariant === "combined" ? (
            /* Multi-section combined variant */
            <>
              {s.testOrders.some((t) => t.locations.length > 0 && !t.isSuperseded) ? (
                <div className="coa-id-strip">
                  <div><div className="il">Reference</div><div className="iv">{s.referenceNumber}</div></div>
                  <div><div className="il">Category</div><div className="iv">{humanize(s.category)}</div></div>
                  <div><div className="il">Batch / Control</div><div className="iv">{s.batchNumber ?? s.controlNumber}</div></div>
                  <div><div className="il">Received</div><div className="iv">{d(s.receivedAt)}</div></div>
                </div>
              ) : (
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
                    <div><div className="il">Result Date</div><div className="iv">{d(computeResultDate(s.testOrders))}</div></div>
                    <div><div className="il">QC No.</div><div className="iv">{s.controlNumber}</div></div>
                    <div><div className="il">Certificate Date</div><div className="iv">{d(s.approvedAt)}</div></div>
                  </div>
                </div>
              )}

              {(() => {
                const sections = s.sections ?? [];
                return (
                  <>
                    {groupTestOrdersBySection(s.testOrders, sections).map((group) => {
                      const groupMatrix = buildCoaMatrix(group.testOrders);
                      const groupSimple = groupMatrix ? null : buildCoaSimpleRows(group.testOrders);
                      const secDetail = sections.find((sec) => sec.sectionId === group.sectionId);
                      const secRemarks = secDetail?.certificateRemarks?.trim() || null;

                      return (
                        <div key={group.sectionId} style={{ marginBottom: 24 }}>
                          <div className="coa-section-h" style={{ fontSize: 13, color: "var(--coa-ink)", marginBottom: 8 }}>
                            {group.sectionName} — Test Results
                          </div>
                          {groupMatrix && <CoaMatrixTable matrix={groupMatrix} />}
                          {groupSimple && <CoaSimpleTable simple={groupSimple} testOrders={group.testOrders} />}
                          {!groupMatrix && !groupSimple && (
                            <div style={{ color: "var(--coa-ink3)", fontStyle: "italic", marginBottom: 12, fontSize: 12 }}>
                              No recorded results for this section.
                            </div>
                          )}
                          <div style={{ marginTop: 10, marginBottom: 16 }}>
                            <div className="coa-section-h" style={{ fontSize: 11, marginBottom: 4 }}>
                              Remarks ({group.sectionName})
                            </div>
                            <div className={`coa-remarks-box ${secRemarks ? "" : "is-empty"}`} style={{ marginBottom: 12 }}>
                              {secRemarks ?? "No remarks."}
                            </div>
                          </div>
                        </div>
                      );
                    })}

                    {(() => {
                      const cMatrix = buildCoaMatrix(s.testOrders);
                      const cSimple = cMatrix ? null : buildCoaSimpleRows(s.testOrders);
                      const complies = cMatrix ? cMatrix.overallComplies : (cSimple ? cSimple.overallComplies : true);
                      const conclusion = cMatrix
                        ? buildOverallConclusionText(cMatrix)
                        : cSimple
                        ? buildSimpleConclusionText(cSimple)
                        : "This sample complies with the specified requirements.";

                      return (
                        <div className={`coa-overall ${complies ? "" : "is-fail"}`}>
                          <div className="ot">Overall Conclusion</div>
                          <div className="od">{conclusion}</div>
                        </div>
                      );
                    })()}

                    <div
                      className="coa-sig-strip"
                      style={{
                        gridTemplateColumns: sections.length > 2 ? "repeat(auto-fit, minmax(200px, 1fr))" : "1fr 1fr"
                      }}
                    >
                      {sections.map((sec) => (
                        <div className="coa-sig-block" key={sec.sectionId}>
                          <div className="sn">{sec.approvedByName ?? "—"}</div>
                          <div className="sr">Approver — {sec.sectionName}</div>
                          <div className="sm">"{MEANING_TEXT["Approved"]}"</div>
                          <div className="st">{dt(sec.approvedAt)}</div>
                        </div>
                      ))}
                    </div>
                  </>
                );
              })()}
            </>
          ) : activeSection ? (
            /* Multi-section single-section variant */
            (() => {
              const secTests = filterTestOrdersBySection(s.testOrders, activeSection.sectionId);
              const secMatrix = buildCoaMatrix(secTests);
              const secSimple = secMatrix ? null : buildCoaSimpleRows(secTests);
              const secRemarks = activeSection.certificateRemarks?.trim() || null;
              const complies = secMatrix ? secMatrix.overallComplies : (secSimple ? secSimple.overallComplies : true);
              const conclusion = secMatrix
                ? buildOverallConclusionText(secMatrix)
                : secSimple
                ? buildSimpleConclusionText(secSimple)
                : "This section complies with the specified requirements.";

              return (
                <>
                  {secMatrix ? (
                    <>
                      <div className="coa-id-strip">
                        <div><div className="il">Reference</div><div className="iv">{s.referenceNumber}</div></div>
                        <div><div className="il">Category</div><div className="iv">{humanize(s.category)}</div></div>
                        <div><div className="il">Batch / Control</div><div className="iv">{s.batchNumber ?? s.controlNumber}</div></div>
                        <div><div className="il">Received</div><div className="iv">{d(s.receivedAt)}</div></div>
                      </div>

                      <div className="coa-section-h">Test Results by Location</div>
                      <CoaMatrixTable matrix={secMatrix} />
                    </>
                  ) : secSimple ? (
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
                          <div><div className="il">Result Date</div><div className="iv">{d(computeResultDate(secTests))}</div></div>
                          <div><div className="il">QC No.</div><div className="iv">{s.controlNumber}</div></div>
                          <div><div className="il">Certificate Date</div><div className="iv">{d(activeSection.approvedAt ?? s.approvedAt)}</div></div>
                        </div>
                      </div>

                      <div className="coa-section-h">Test Results</div>
                      <CoaSimpleTable simple={secSimple} testOrders={secTests} />
                    </>
                  ) : (
                    <div style={{ padding: 24, textAlign: "center", color: "var(--coa-ink3)", fontStyle: "italic" }}>
                      A Certificate of Analysis is not available for this section - none of its tests have recorded results yet.
                    </div>
                  )}

                  <div className="coa-section-h">Remarks</div>
                  <div className={`coa-remarks-box ${secRemarks ? "" : "is-empty"}`}>
                    {secRemarks ?? "No remarks."}
                  </div>

                  <div className={`coa-overall ${complies ? "" : "is-fail"}`}>
                    <div className="ot">Overall Conclusion</div>
                    <div className="od">{conclusion}</div>
                  </div>

                  <div
                    className="coa-sig-strip"
                    style={{
                      gridTemplateColumns: activeSection.reviewedByName ? "1fr 1fr" : "1fr",
                      maxWidth: activeSection.reviewedByName ? undefined : 320
                    }}
                  >
                    {activeSection.reviewedByName && (
                      <SignatureBlock
                        fallbackRole="Reviewer"
                        sig={undefined}
                        fallbackName={activeSection.reviewedByName}
                        fallbackAt={activeSection.reviewedAt}
                        fallbackMeaning="Reviewed"
                      />
                    )}
                    <SignatureBlock
                      fallbackRole="Approver"
                      sig={undefined}
                      fallbackName={activeSection.approvedByName}
                      fallbackAt={activeSection.approvedAt}
                      fallbackMeaning="Approved"
                    />
                  </div>
                </>
              );
            })()
          ) : null}

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
