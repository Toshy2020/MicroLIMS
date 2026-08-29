// Shared building blocks for every printable record (sample, media lot,
// cryovial batch) so the three reports stay one visual language rather
// than drifting apart. Styling lives in reportStyles.ts.

export const CheckIcon = ({ strokeWidth = 2.5 }: { strokeWidth?: number }) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={strokeWidth} strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12" />
  </svg>
);

export const CrossIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
    <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
  </svg>
);

export const DotIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="4" />
  </svg>
);

const PrinterIcon = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
    <polyline points="6 9 6 2 18 2 18 9" />
    <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
    <rect x="6" y="14" width="12" height="8" />
  </svg>
);

// Enum names reach the client in PascalCase ("FinishedProduct",
// "WithinLimits"). On a controlled document they need to read as English.
// Consecutive capitals are preserved so acronyms survive: "TAMC" stays
// "TAMC", "OOSInvestigation" becomes "OOS investigation".
export const humanize = (v: string | null | undefined): string => {
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
  Approved: "I approve the release of this record for its intended use.",
  Rejected: "I reject this record; it does not conform to specification.",
  RetestRequested: "I am ordering a retest of the retained sample.",
  InvestigationOrdered: "I am ordering an investigation into these results."
};

export const dt = (v: string | null | undefined) =>
  v ? new Date(v).toLocaleString("en-GB", { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }).replace(",", "") : "—";

export const d = (v: string | null | undefined) =>
  v ? new Date(v).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" }) : "—";

export interface SignatureLike {
  printedName: string;
  username: string;
  role: string;
  meaning: string;
  signedAt: string;
  comment: string | null;
}

export function SignatureSection({ signatures }: { signatures: SignatureLike[] }) {
  if (signatures.length === 0) return null;
  return (
    <div style={{ marginBottom: 24 }}>
      <div className="section-divider">
        <div className="section-label">electronic signatures</div>
        <div className="line" />
      </div>
      <div className="signature-grid">
        {signatures.map((sig, i) => (
          <div className="signature-card" key={i}>
            <div className="sig-header">
              <div className="sig-icon"><CheckIcon /></div>
              <div>
                <div className="sig-name">{sig.printedName}</div>
                {/* Full names collide across accounts in this lab, so the
                    username is what actually proves two signatures came
                    from different people. */}
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
  );
}

export interface TimelineEventLike {
  eventType: string;
  performedByName: string;
  timestamp: string;
  comment: string | null;
  decision: string | null;
}

// Media lots and cryovial batches pass through a single approval gate, so
// their history is a short event list rather than the sample's fixed
// five-stage track.
export function EventTimelineSection({ events }: { events: TimelineEventLike[] }) {
  return (
    <div style={{ marginBottom: 24 }}>
      <div className="section-divider">
        <div className="section-label">lifecycle timeline</div>
        <div className="line" />
      </div>
      <div className="timeline-wrap">
        {events.length === 0 ? (
          <span style={{ fontSize: 13, color: "#888" }}>No lifecycle events recorded.</span>
        ) : (
          events.map((e, i) => (
            <div className="observation-item" key={i}>
              <span className="obs-step">
                {humanize(e.eventType)}{e.decision ? ` — ${humanize(e.decision)}` : ""}
              </span>
              <span>
                <strong>{e.performedByName}</strong>
                <span className="obs-meta"> · {dt(e.timestamp)}</span>
                {e.comment && <span className="obs-meta"> · “{e.comment}”</span>}
              </span>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

export interface ArchivedCopyLike {
  id: number;
  fileName: string;
  sizeBytes: number;
  reason: string;
  generatedByNameSnapshot: string;
  generatedAt: string;
}

function formatBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  return `${(n / 1024).toFixed(0)} KB`;
}

// Not part of the printed page itself - it lists the immutable PDFs
// frozen at each final decision, distinct from this live view (which
// re-renders from current data every time it's opened). Rendered on
// screen only; the print stylesheet's .no-print rule hides it so it
// never appears inside the archived copy it links to.
export function ArchivedCopiesSection({
  copies, onDownload
}: {
  copies: ArchivedCopyLike[];
  onDownload: (id: number, fileName: string) => void;
}) {
  if (copies.length === 0) return null;
  return (
    <div className="no-print" style={{ marginBottom: 24 }}>
      <div className="section-divider">
        <div className="section-label">archived copies</div>
        <div className="line" />
      </div>
      <div className="timeline-wrap">
        <div style={{ fontSize: 11, color: "#888", marginBottom: 8 }}>
          Immutable PDFs frozen at each final decision - what this record looked like when it was issued, not this live view.
        </div>
        {copies.map((c) => (
          <div className="observation-item" key={c.id}>
            <span className="obs-step">
              {c.reason}
              <span className="obs-meta"> · {c.generatedByNameSnapshot} · {dt(c.generatedAt)} · {formatBytes(c.sizeBytes)}</span>
            </span>
            <span
              style={{ cursor: "pointer", color: "#2563eb", fontWeight: 600 }}
              onClick={() => onDownload(c.id, c.fileName)}
            >
              Download
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

export function ReportFooter({ documentId }: { documentId: string }) {
  return (
    <div className="report-footer">
      <div><strong>This is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled.</strong></div>
      <div>Document ID: {documentId} · Generated: {dt(new Date().toISOString())}</div>
    </div>
  );
}

export function PrintButton() {
  return (
    <button className="print-btn" onClick={() => window.print()}>
      <PrinterIcon />
      Print / Save PDF
    </button>
  );
}
