import { ReactNode, useState } from "react";

const ChevronIcon = ({ className }: { className: string }) => (
  <svg className={className} width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={3} strokeLinecap="round" strokeLinejoin="round">
    <path d="M9 6l6 6-6 6" />
  </svg>
);

export interface CollapsibleTestCardProps {
  icon: ReactNode;
  iconTone: "" | "is-danger" | "is-neutral";
  title: string;
  subtitle: ReactNode;
  locationCount?: number;
  badgeText: string;
  badgeTone: "" | "is-danger" | "is-neutral";
  // Exceptions open by default (constraint: "auto-expand exceptions"),
  // fully-conforming tests load collapsed. Callers derive this from
  // whatever status field already exists on the test.
  defaultOpen: boolean;
  isSuperseded?: boolean;
  children: ReactNode;
}

// The shared shell: header row (chevron, icon, title/subtitle, location
// count, status badge) + a body that's always in the DOM but display:none
// until opened. Keeping the body mounted (rather than conditionally
// rendering children) is what lets @media print force it open with a
// single CSS override, with nothing to re-render or re-fetch - see the
// .test-card-body print rule in reportStyles.ts.
export function CollapsibleTestCard({
  icon, iconTone, title, subtitle, locationCount, badgeText, badgeTone, defaultOpen, isSuperseded, children
}: CollapsibleTestCardProps) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <div className={`test-card ${isSuperseded ? "is-superseded" : ""} ${open ? "is-open" : ""}`}>
      <div className="test-card-header no-print" onClick={() => setOpen((o) => !o)}>
        <ChevronIcon className="test-card-chev" />
        <div className={`test-icon ${iconTone}`}>{icon}</div>
        <div className="test-card-flex">
          <div className="test-title">{title}</div>
          <div className="test-subtitle">{subtitle}</div>
        </div>
        {locationCount != null && (
          <span className="test-card-loc-count">{locationCount} location{locationCount === 1 ? "" : "s"}</span>
        )}
        <span className={`status-badge ${badgeTone}`}>{badgeText}</span>
      </div>
      {/* Print-only header: the interactive row above is hidden on paper
          (.no-print) since click affordances mean nothing there: this
          static line carries the same identity/status without a chevron. */}
      <div className="test-card-print-header print-only">
        <span className="test-title">{title}</span>
        <span className="test-subtitle">{subtitle}</span>
        <span className={`status-badge ${badgeTone}`}>{badgeText}</span>
      </div>
      <div className="test-card-body">{children}</div>
    </div>
  );
}

// A single nested disclosure inside a card body - used for both "Show
// incubation stage details" and "Show full location table" (decision A).
// Independent state per instance, so opening one doesn't open the other.
//
// `collapsedContent` (location pills) is a swap, not an append: it's the
// collapsed-state view of the same data the expanded body shows, so it's
// hidden the moment the toggle opens (.secondary-toggle-collapsed CSS) and
// suppressed outright in print - print always force-expands, which would
// otherwise render pills and table together. Callers with nothing to show
// while collapsed (stage details) omit the prop and keep the plain
// append-on-open behavior.
export function SecondaryToggle({ label, collapsedContent, children }: { label: string; collapsedContent?: ReactNode; children: ReactNode }) {
  const [open, setOpen] = useState(false);
  return (
    <div className={`secondary-toggle ${open ? "is-open" : ""}`}>
      <button type="button" className="stage-toggle-btn no-print" onClick={() => setOpen((o) => !o)}>
        <ChevronIcon className="stage-toggle-chev" />
        {label}
      </button>
      {collapsedContent && <div className="secondary-toggle-collapsed">{collapsedContent}</div>}
      <div className="stage-detail-body">{children}</div>
    </div>
  );
}
