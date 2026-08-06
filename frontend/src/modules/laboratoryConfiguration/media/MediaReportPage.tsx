import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { apiClient } from "../../../services/apiClient";
import { reportStyles } from "../../testingWorkspace/reportStyles";
import {
  CheckIcon, CrossIcon, DotIcon, humanize, dt, d,
  SignatureSection, EventTimelineSection, ArchivedCopiesSection, ReportFooter, PrintButton
} from "../../testingWorkspace/reportPrimitives";
import { ArchivedRecordsService, ArchivedRecordSummary } from "../../testingWorkspace/services/ArchivedRecordsService";
import { MediaSummary, MediaChallengeSummary } from "./types/mediaSummaryTypes";

// Released is the only "good" terminal state; a quarantined or rejected
// lot reads red, and anything still in flight reads neutral.
function releaseTone(s: MediaSummary): "" | "is-danger" | "is-warning" | "is-neutral" {
  if (s.isReleasedForUse) return "";
  if (s.approvalStatus === "Rejected" || s.status === "QuarantineFailed") return "is-danger";
  if (s.evaluation?.outcome === "Conform") return "is-warning";
  return "is-neutral";
}

function releaseLabel(s: MediaSummary): string {
  if (s.isReleasedForUse) return "Released for use";
  if (s.approvalStatus === "Rejected" || s.status === "QuarantineFailed") return "Quarantined";
  if (s.evaluation?.outcome === "Conform") return "Awaiting release approval";
  if (s.evaluation?.outcome === "NonConform") return "Evaluation failed";
  return "Pending evaluation";
}

export function MediaReportPage() {
  const { id } = useParams();
  const [summary, setSummary] = useState<MediaSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [archivedCopies, setArchivedCopies] = useState<ArchivedRecordSummary[]>([]);

  useEffect(() => {
    if (!id) return;
    apiClient.get(`/media/${id}/summary`)
      .then((r) => setSummary(r.data.data))
      .catch((e) => setError(e?.response?.data?.message ?? "Failed to load the media lot record."));
    ArchivedRecordsService.getForEntity("Media", Number(id)).then(setArchivedCopies).catch(() => setArchivedCopies([]));
  }, [id]);

  useEffect(() => {
    if (summary) document.title = `Media Lot Record - ${summary.lotNumber}`;
  }, [summary]);

  if (error) return <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#dc2626" }}>{error}</div>;
  if (!summary) return <div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#666" }}>Loading record…</div>;

  const s = summary;
  const tone = releaseTone(s);

  return (
    <div className="report-root">
      <style>{reportStyles}</style>

      <div className="report-wrapper">
        <div className="report-header">
          <div className="report-header-left">
            <div className="label">media lot record</div>
            <div className="sample-id">{s.lotNumber}</div>
            <div className="subtitle">{humanize(s.mediaClass)} · {s.materialName}</div>
          </div>
          <div className="report-header-right">
            <div className={`status-badge ${tone}`}>
              {s.isReleasedForUse ? <CheckIcon /> : tone === "is-danger" ? <CrossIcon /> : <DotIcon />}
              {releaseLabel(s)}
            </div>
            <div className="header-date">Prepared {dt(s.preparedAt)}</div>
          </div>
        </div>

        <div className="two-col-grid">
          <div className="section-card">
            <div className="section-label">lot identity</div>
            <div className="data-grid">
              <span className="key">Lot number</span><span className="value mono">{s.lotNumber}</span>
              <span className="key">Media class</span><span className="value">{humanize(s.mediaClass)}</span>
              <span className="key">Source material</span><span className="value">{s.materialName}</span>
              <span className="key">Manufacturer</span><span className="value">{s.manufacturerName || "—"}</span>
              <span className="key">Mfr. lot</span><span className="value mono">{s.manufacturerLot || "—"}</span>
              <span className="key">Expires</span><span className="value">{d(s.expiryDate)}</span>
            </div>
          </div>
          <div className="section-card">
            <div className="section-label">release status</div>
            <div className="data-grid">
              <span className="key">Inventory status</span><span className="value">{humanize(s.status)}</span>
              <span className="key">Approval</span><span className="value">{humanize(s.approvalStatus)}</span>
              <span className="key">Released</span><span className="value">{s.isReleasedForUse ? "Yes" : "No"}</span>
              <span className="key">Prepared by</span><span className="value">{s.preparedByName}</span>
              {s.approvedByName && (<><span className="key">Decided by</span><span className="value">{s.approvedByName}</span></>)}
              {s.approvedAt && (<><span className="key">Decided at</span><span className="value mono">{dt(s.approvedAt)}</span></>)}
            </div>
          </div>
        </div>

        <div className="section-card">
          <div className="section-label">preparation record</div>
          <div className="prep-grid">
            <div className="prep-item">
              <div className="prep-label">Weight / volume</div>
              <div className="prep-value">{s.totalWeight} g · {s.totalVolume}</div>
            </div>
            <div className="prep-item">
              <div className="prep-label">Autoclave</div>
              <div className="prep-value">{s.autoclaveName ?? "—"}</div>
              <div className="prep-sub">{s.autoclaveProgram || "—"} · {s.loadType || "—"}</div>
            </div>
            <div className="prep-item">
              <div className="prep-label">Cycle</div>
              <div className="prep-value">{s.temperature} °C · {s.cycleTime} min</div>
              <div className="prep-sub">Cycle no. {s.cycleNumber}</div>
            </div>
            <div className="prep-item">
              <div className="prep-label">pH</div>
              <div className="prep-value">{s.ph}</div>
              <div className="prep-sub">{dt(s.preparedAt)}</div>
            </div>
          </div>
        </div>

        <div style={{ marginBottom: 24 }}>
          <div className="section-divider">
            <div className="section-label">media evaluation</div>
            <div className="line" />
          </div>
          {!s.evaluation ? (
            <div className="section-card"><span style={{ fontSize: 13, color: "#888" }}>No evaluation assigned.</span></div>
          ) : (
            <div className="test-card">
              <div className="test-header">
                <div className="test-header-left">
                  <div className={`test-icon ${s.evaluation.outcome === "NonConform" ? "is-danger" : s.evaluation.outcome ? "" : "is-neutral"}`}>
                    {s.evaluation.outcome === "NonConform" ? <CrossIcon /> : s.evaluation.outcome ? <CheckIcon /> : <DotIcon />}
                  </div>
                  <div>
                    <div className="test-title">{humanize(s.evaluation.evaluationType)}</div>
                    <div className="test-subtitle">
                      Assigned {dt(s.evaluation.assignedAt)}
                      {s.evaluation.completedByName ? ` · Completed by ${s.evaluation.completedByName}` : ""}
                    </div>
                  </div>
                </div>
                <div>
                  <div className={`test-result-value ${s.evaluation.outcome === "NonConform" ? "is-danger" : s.evaluation.outcome ? "" : "is-neutral"}`}>
                    {humanize(s.evaluation.outcome) ?? "—"}
                  </div>
                  <div className="test-result-unit">{humanize(s.evaluation.status)}</div>
                </div>
              </div>

              <div className="observation-row">
                {s.evaluation.challenges.length === 0 ? (
                  <span style={{ fontSize: 13, color: "#888" }}>
                    No challenge organisms configured for this material — the lot cannot conform until challenge specs exist.
                  </span>
                ) : (
                  s.evaluation.challenges.map((c, i) => <ChallengeRow key={i} c={c} />)
                )}
              </div>

              <div className="test-footer">
                <span>
                  {s.evaluation.completedAt
                    ? <>Completed <strong>{dt(s.evaluation.completedAt)}</strong></>
                    : <>Evaluation in progress</>}
                </span>
                <span className={`pass-tag ${s.evaluation.outcome === "NonConform" ? "is-danger" : s.evaluation.outcome ? "" : "is-neutral"}`}>
                  {s.evaluation.outcome === "NonConform" ? <CrossIcon /> : s.evaluation.outcome ? <CheckIcon /> : <DotIcon />}
                  {humanize(s.evaluation.outcome ?? s.evaluation.status)}
                </span>
              </div>
            </div>
          )}
        </div>

        <EventTimelineSection events={s.timeline} />
        <SignatureSection signatures={s.signatures} />
        <ArchivedCopiesSection
          copies={archivedCopies}
          onDownload={(archiveId, fileName) => ArchivedRecordsService.download(archiveId, fileName)}
        />
        <ReportFooter documentId={s.lotNumber} />
      </div>

      <PrintButton />
    </div>
  );
}

// Which numbers matter depends on the evaluation type, so the row shows
// only the measurements that challenge actually produced.
function ChallengeRow({ c }: { c: MediaChallengeSummary }) {
  const detail: string[] = [];
  if (c.recoveryPercent !== null) detail.push(`Recovery ${c.recoveryPercent}% (${c.oldMediaCount} → ${c.newMediaCount})`);
  if (c.growthObserved !== null) detail.push(c.growthObserved ? "Growth observed" : "No growth");
  if (c.observedDescription) detail.push(`Observed: ${c.observedDescription}`);
  if (c.expectedDescription) detail.push(`Expected: ${c.expectedDescription}`);
  if (c.isTurbid !== null) detail.push(c.isTurbid ? "Turbid" : "Clear");

  return (
    <div className="observation-item">
      <span className="obs-step">
        {c.organismName}{c.challengeRole ? ` (${humanize(c.challengeRole)})` : ""}
        <span className="obs-meta">
          {c.cryovialCode ? ` · Cryovial ${c.cryovialCode}` : ""}
          {c.incubatorName ? ` · ${c.incubatorName}` : ""}
          {c.temperature ? ` · ${c.temperature}` : ""}
        </span>
      </span>
      <span>
        <strong>{humanize(c.outcome)}</strong>
        {detail.length > 0 && <span className="obs-meta"> · {detail.join(" · ")}</span>}
        {c.readByName && <span className="obs-meta"> · {c.readByName} · {dt(c.readAt)}</span>}
      </span>
    </div>
  );
}
