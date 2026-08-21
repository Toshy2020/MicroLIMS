import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { apiClient } from "../../../services/apiClient";
import { reportStyles } from "../../testingWorkspace/reportStyles";
import {
  CheckIcon, CrossIcon, DotIcon, humanize, dt, d,
  SignatureSection, EventTimelineSection, ArchivedCopiesSection, ReportFooter, PrintButton
} from "../../testingWorkspace/reportPrimitives";
import { ArchivedRecordsService, ArchivedRecordSummary } from "../../testingWorkspace/services/ArchivedRecordsService";
import { CryovialSummary } from "./types/cryovialSummaryTypes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

function approvalTone(s: CryovialSummary): "" | "is-danger" | "is-warning" | "is-neutral" {
  if (s.isDestroyed || s.approvalStatus === "Rejected") return "is-danger";
  if (s.approvalStatus === "Approved") return "";
  return "is-warning";
}

function approvalLabel(s: CryovialSummary): string {
  if (s.approvalStatus === "Rejected") return "Rejected";
  if (s.isDestroyed) return "Destroyed";
  if (s.approvalStatus === "Approved") return "Approved for use";
  return "Awaiting approval";
}

export function CryovialReportPage() {
  const { id } = useParams();
  const [summary, setSummary] = useState<CryovialSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [archivedCopies, setArchivedCopies] = useState<ArchivedRecordSummary[]>([]);

  useEffect(() => {
    if (!id) return;
    apiClient.get(`/cryovials/${id}/summary`)
      .then((r) => setSummary(r.data.data))
      .catch((e) => setError(e?.response?.data?.message ?? "Failed to load the cryovial batch record."));
    ArchivedRecordsService.getForEntity("Cryovial", Number(id)).then(setArchivedCopies).catch(() => setArchivedCopies([]));
  }, [id]);

  useEffect(() => {
    if (summary) document.title = `Cryovial Batch Record - ${summary.code}`;
  }, [summary]);

  if (error) return <PinnedLightTheme><div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#dc2626" }}>{error}</div></PinnedLightTheme>;
  if (!summary) return <PinnedLightTheme><div style={{ padding: 32, fontFamily: "Segoe UI, sans-serif", color: "#666" }}>Loading record…</div></PinnedLightTheme>;

  const s = summary;
  const tone = approvalTone(s);
  const depleted = s.vialsRemaining === 0;

  return (
    <PinnedLightTheme>
    <div className="report-root">
      <style>{reportStyles}</style>

      <div className="report-wrapper">
        <div className="report-header">
          <div className="report-header-left">
            <div className="label">cryovial batch record</div>
            <div className="sample-id">{s.code}</div>
            <div className="subtitle">{s.organismName} · {s.materialName}</div>
          </div>
          <div className="report-header-right">
            <div className={`status-badge ${tone}`}>
              {s.approvalStatus === "Approved" && !s.isDestroyed ? <CheckIcon /> : tone === "is-danger" ? <CrossIcon /> : <DotIcon />}
              {approvalLabel(s)}
            </div>
            <div className="header-date">Prepared {dt(s.preparedAt)}</div>
          </div>
        </div>

        <div className="two-col-grid">
          <div className="section-card">
            <div className="section-label">batch identity</div>
            <div className="data-grid">
              <span className="key">Code</span><span className="value mono">{s.code}</span>
              <span className="key">Organism</span><span className="value">{s.organismName}</span>
              <span className="key">Source material</span><span className="value">{s.materialName}</span>
              <span className="key">Material batch</span><span className="value mono">{s.materialBatchNumber || "—"}</span>
              <span className="key">Manufacturer</span><span className="value">{s.manufacturerName || "—"}</span>
              <span className="key">Expires</span><span className="value">{d(s.expiryDate)}</span>
            </div>
          </div>
          <div className="section-card">
            <div className="section-label">stock &amp; approval</div>
            <div className="data-grid">
              <span className="key">Vials prepared</span><span className="value mono">{s.numberOfVialsPrepared}</span>
              <span className="key">Vials remaining</span>
              <span className="value mono">{s.vialsRemaining}{depleted ? " (depleted)" : ""}</span>
              <span className="key">Storage</span><span className="value">{s.storageCondition || "—"}</span>
              <span className="key">Approval</span><span className="value">{humanize(s.approvalStatus)}</span>
              <span className="key">Prepared by</span><span className="value">{s.preparedByName}</span>
              {s.approvedByName && (<><span className="key">Decided by</span><span className="value">{s.approvedByName}</span></>)}
              {s.approvedAt && (<><span className="key">Decided at</span><span className="value mono">{dt(s.approvedAt)}</span></>)}
            </div>
          </div>
        </div>

        {s.physicalCheckText && (
          <div className="section-card">
            <div className="section-label">physical check</div>
            <div style={{ fontSize: 14 }}>{s.physicalCheckText}</div>
          </div>
        )}

        <div style={{ marginBottom: 24 }}>
          <div className="section-divider">
            <div className="section-label">identity confirmation panel</div>
            <div className="line" />
          </div>
          <div className="test-card">
            <div className="test-header">
              <div className="test-header-left">
                <div className={`test-icon ${s.identityConfirmations.length === 0 ? "is-neutral" : ""}`}>
                  {s.identityConfirmations.length === 0 ? <DotIcon /> : <CheckIcon />}
                </div>
                <div>
                  <div className="test-title">Identity confirmation</div>
                  <div className="test-subtitle">
                    {s.identityConfirmations.length} media {s.identityConfirmations.length === 1 ? "row" : "rows"} · this is the only place batch identity is confirmed
                  </div>
                </div>
              </div>
            </div>
            <div className="observation-row">
              {s.identityConfirmations.length === 0 ? (
                <span style={{ fontSize: 13, color: "#888" }}>No panel rows recorded.</span>
              ) : (
                s.identityConfirmations.map((i, idx) => (
                  <div className="observation-item" key={idx}>
                    <span className="obs-step">
                      {i.mediaLotNumber ?? "—"}
                      <span className="obs-meta">{i.incubatorName ? ` · ${i.incubatorName}` : ""}</span>
                    </span>
                    <span>
                      <strong>{i.observationText || "—"}</strong>
                      <span className="obs-meta"> · {d(i.incubationStart)} → {d(i.incubationEnd)}</span>
                    </span>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        {s.thawHistory.length > 0 && (
          <div style={{ marginBottom: 24 }}>
            <div className="section-divider">
              <div className="section-label">thaw history</div>
              <div className="line" />
            </div>
            <div className="timeline-wrap">
              {s.thawHistory.map((t, i) => (
                <div className="observation-item" key={i}>
                  <span className="obs-step">Vial thawed</span>
                  <span>
                    <strong>{t.thawedByName}</strong>
                    <span className="obs-meta"> · {dt(t.thawedAt)}</span>
                    {t.notes && <span className="obs-meta"> · {t.notes}</span>}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        <EventTimelineSection events={s.timeline} />
        <SignatureSection signatures={s.signatures} />
        <ArchivedCopiesSection
          copies={archivedCopies}
          onDownload={(archiveId, fileName) => ArchivedRecordsService.download(archiveId, fileName)}
        />
        <ReportFooter documentId={s.code} />
      </div>

      <PrintButton />
    </div>
    </PinnedLightTheme>
  );
}
