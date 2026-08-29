// Ported verbatim from the approved report design. Kept as a raw CSS
// string rather than converted to MUI `sx` on purpose: the @media print
// block is carefully tuned (A4 portrait, pt-based type scale,
// page-break-inside guards, print-color-adjust) and rewriting it in a
// JS styling API would be a lossy translation of the part that matters
// most. Class names match the original markup one-to-one.
export const reportStyles = `
:root {
  --font-sans: "Segoe UI", system-ui, -apple-system, sans-serif;
  --color-text-primary: #111;
  --color-text-secondary: #444;
  --color-text-tertiary: #666;
  --color-text-quaternary: #888;
  --color-border: #ddd;
  --color-surface: #fafafa;
  --color-surface-muted: #f3f3f3;
  --color-surface-raised: #fff;
  --color-positive: #16a34a;
  --color-danger: #dc2626;
  --color-warning: #d97706;
}

.report-root * { box-sizing: border-box; margin: 0; padding: 0; }

.report-root {
  font-family: var(--font-sans);
  font-size: 16px;
  line-height: 1.5;
  background: #f5f5f5;
  color: var(--color-text-primary);
  -webkit-font-smoothing: antialiased;
  min-height: 100vh;
}

.report-wrapper {
  max-width: 800px;
  margin: 32px auto;
  padding: 32px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08), 0 4px 12px rgba(0,0,0,0.05);
}

.report-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
  padding-bottom: 16px;
  border-bottom: 1.5px solid var(--color-border);
}

.report-header-left .label {
  font-size: 12px;
  color: var(--color-text-quaternary);
  letter-spacing: 0.5px;
  text-transform: uppercase;
  margin-bottom: 4px;
}

.report-header-left .sample-id {
  font-size: 28px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.report-header-left .subtitle {
  font-size: 14px;
  color: var(--color-text-secondary);
  margin-top: 4px;
}
.report-header-left .subtitle strong { color: var(--color-text-primary); font-weight: 600; }

.report-header-right { text-align: right; }

.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 6px;
  background: rgba(22, 163, 74, 0.1);
  color: var(--color-positive);
  font-size: 13px;
  font-weight: 600;
}
.status-badge.is-danger { background: rgba(220, 38, 38, 0.1); color: var(--color-danger); }
.status-badge.is-warning { background: rgba(217, 119, 6, 0.1); color: var(--color-warning); }
.status-badge.is-neutral { background: #eee; color: var(--color-text-secondary); }
.status-badge svg { width: 14px; height: 14px; }

.header-date { font-size: 12px; color: var(--color-text-quaternary); margin-top: 8px; }

.section-card {
  padding: 16px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  margin-bottom: 16px;
  background: #fff;
}

.section-label {
  font-size: 11px;
  color: var(--color-text-quaternary);
  text-transform: uppercase;
  letter-spacing: 0.6px;
  margin-bottom: 10px;
  font-weight: 500;
}

.two-col-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }

.data-grid {
  display: grid;
  grid-template-columns: 120px 1fr;
  gap: 8px 12px;
  font-size: 14px;
}
.data-grid .key { color: var(--color-text-tertiary); }
.data-grid .value { font-weight: 500; }
.data-grid .value.mono { font-variant-numeric: tabular-nums; }

.prep-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
.prep-item .prep-label { font-size: 12px; color: var(--color-text-tertiary); margin-bottom: 2px; }
.prep-item .prep-value { font-size: 15px; font-weight: 500; }
.prep-item .prep-sub { font-size: 12px; color: var(--color-text-quaternary); font-variant-numeric: tabular-nums; }

.section-divider { display: flex; align-items: center; gap: 10px; margin-bottom: 12px; }
.section-divider .section-label { margin-bottom: 0; }
.section-divider .line { flex: 1; height: 1px; background: var(--color-border); }

.matrix-section { border: 1px solid var(--color-border); border-radius: 10px; margin-bottom: 16px; background: #fff; overflow: hidden; }
.matrix-header { display: flex; justify-content: space-between; align-items: center; padding: 14px 16px; border-bottom: 1px solid var(--color-border); gap: 12px; }
.matrix-header h3 { font-size: 14px; font-weight: 700; }
.matrix-header .matrix-sub { font-size: 11.5px; color: var(--color-text-tertiary); margin-top: 2px; }
.matrix-wrap { overflow-x: auto; }
table.result-matrix { width: 100%; border-collapse: collapse; font-size: 12px; }
table.result-matrix th {
  background: var(--color-surface-muted); padding: 9px 10px; font-size: 10px; font-weight: 700;
  text-transform: uppercase; letter-spacing: 0.4px; color: var(--color-text-tertiary);
  border-bottom: 1px solid var(--color-border); text-align: center; white-space: nowrap;
}
table.result-matrix th:first-child { text-align: left; padding-left: 16px; }
table.result-matrix td { padding: 9px 10px; border-bottom: 1px solid var(--color-border); text-align: center; white-space: nowrap; }
table.result-matrix tr:last-child td { border-bottom: none; }
table.result-matrix td:first-child { text-align: left; padding-left: 16px; font-weight: 700; font-variant-numeric: tabular-nums; }
.matrix-cell { font-weight: 700; font-variant-numeric: tabular-nums; }
.matrix-legend { display: flex; gap: 16px; padding: 10px 16px; background: var(--color-surface-muted); font-size: 11px; color: var(--color-text-tertiary); border-top: 1px solid var(--color-border); flex-wrap: wrap; }
.matrix-legend .legend-dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 5px; }

.test-card {
  border: 1.5px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
  margin-bottom: 12px;
}
.test-card.is-superseded { opacity: 0.72; border-style: dashed; }

.test-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 16px;
  background: var(--color-surface-muted);
  gap: 12px;
}

.test-header-left { display: flex; align-items: center; gap: 10px; }

.test-icon {
  width: 32px; height: 32px;
  border-radius: 8px;
  background: rgba(22, 163, 74, 0.12);
  display: flex; align-items: center; justify-content: center;
  color: var(--color-positive);
  flex-shrink: 0;
}
.test-icon.is-danger { background: rgba(220, 38, 38, 0.12); color: var(--color-danger); }
.test-icon.is-neutral { background: #e8e8e8; color: var(--color-text-tertiary); }
.test-icon svg { width: 16px; height: 16px; }

.test-title { font-size: 15px; font-weight: 600; }
.test-subtitle { font-size: 12px; color: var(--color-text-tertiary); margin-top: 1px; }

.test-result-value {
  font-size: 22px; font-weight: 600;
  color: var(--color-positive);
  font-variant-numeric: tabular-nums;
  text-align: right;
  white-space: nowrap;
}
.test-result-value.is-danger { color: var(--color-danger); }
.test-result-value.is-neutral { color: var(--color-text-tertiary); }
.test-result-unit { font-size: 11px; color: var(--color-text-quaternary); text-align: right; }

.incubation-row {
  padding: 14px 16px;
  border-top: 1px solid var(--color-border);
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}
.incubation-item .inc-label { font-size: 11px; color: var(--color-text-quaternary); margin-bottom: 3px; }
.incubation-item .inc-value { font-size: 14px; font-weight: 500; }
.incubation-item .inc-value.mono { font-variant-numeric: tabular-nums; }

.plate-readings { padding: 14px 16px; border-top: 1px solid var(--color-border); background: var(--color-surface); }
.plate-readings-label {
  font-size: 11px; color: var(--color-text-quaternary);
  text-transform: uppercase; letter-spacing: 0.5px;
  margin-bottom: 10px; font-weight: 500;
}
.plate-stats { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; }
.plate-stat {
  text-align: center; padding: 10px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-surface-raised);
}
.plate-stat .stat-label { font-size: 11px; color: var(--color-text-quaternary); margin-bottom: 4px; }
.plate-stat .stat-value { font-size: 20px; font-weight: 600; font-variant-numeric: tabular-nums; }
.plate-meta {
  margin-top: 10px; display: flex; gap: 16px;
  font-size: 13px; color: var(--color-text-secondary); flex-wrap: wrap;
}
.plate-meta strong { color: var(--color-text-primary); font-weight: 600; }
.plate-meta .mono { font-variant-numeric: tabular-nums; }

.observation-row { padding: 14px 16px; border-top: 1px solid var(--color-border); background: var(--color-surface); }
.observation-item { display: flex; justify-content: space-between; align-items: center; font-size: 13px; padding: 4px 0; }
.observation-item + .observation-item { border-top: 1px dashed var(--color-border); }
.observation-item .obs-step { font-weight: 500; }
.observation-item .obs-meta { color: var(--color-text-quaternary); font-size: 12px; }

/* EM/After Cleaning per-location result table - a real grid, not a
   flat label/value line, since each location has several fields. */
.location-table-wrap { border-top: 1px solid var(--color-border); overflow-x: auto; }
.location-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.location-table th {
  text-align: left; font-size: 11px; color: var(--color-text-quaternary);
  text-transform: uppercase; letter-spacing: 0.4px; font-weight: 500;
  padding: 10px 16px; background: var(--color-surface-muted);
  border-bottom: 1px solid var(--color-border); white-space: nowrap;
}
.location-table td { padding: 10px 16px; border-bottom: 1px solid var(--color-border); vertical-align: middle; }
.location-table tr:last-child td { border-bottom: none; }
.location-table .loc-name { font-weight: 500; }
.location-table .loc-limits { color: var(--color-text-tertiary); font-variant-numeric: tabular-nums; white-space: nowrap; }
.location-table .loc-cfu, .location-table .loc-reported { font-variant-numeric: tabular-nums; }
.location-status-chip {
  display: inline-flex; align-items: center; padding: 3px 10px; border-radius: 5px;
  font-size: 11px; font-weight: 700; color: #fff; white-space: nowrap;
}

.test-footer {
  padding: 10px 16px;
  border-top: 1px solid var(--color-border);
  background: var(--color-surface-muted);
  font-size: 12px;
  color: var(--color-text-tertiary);
  display: flex; justify-content: space-between; align-items: center; gap: 12px;
}
.test-footer strong { color: var(--color-text-primary); font-weight: 600; }
.test-footer .pass-tag { display: inline-flex; align-items: center; gap: 4px; color: var(--color-positive); white-space: nowrap; }
.test-footer .pass-tag.is-danger { color: var(--color-danger); }
.test-footer .pass-tag.is-neutral { color: var(--color-text-tertiary); }
.test-footer .pass-tag svg { width: 12px; height: 12px; }

/* Collapsible card shell (CollapsibleTestCard / SecondaryToggle) */
.print-only { display: none; }

.test-card-header { display: flex; align-items: center; gap: 12px; padding: 13px 16px; cursor: pointer; }
.test-card-header:hover { background: var(--color-surface-muted); }
.test-card-chev { color: var(--color-text-tertiary); flex: none; transition: transform 0.15s; }
.test-card.is-open .test-card-chev { transform: rotate(90deg); }
.test-card-flex { flex: 1; min-width: 0; }
.test-card-loc-count { font-size: 11.5px; color: var(--color-text-tertiary); white-space: nowrap; }

.test-card-print-header { display: none; align-items: center; gap: 10px; }

.test-card-body { display: none; border-top: 1px solid var(--color-border); padding: 14px 16px 16px; background: var(--color-surface-muted); }
.test-card.is-open .test-card-body { display: block; }

.result-pills { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 12px; }
.result-pill {
  display: inline-flex; align-items: center; gap: 5px; font-size: 11px; font-weight: 600;
  background: rgba(22, 163, 74, 0.1); color: var(--color-positive); padding: 4px 10px; border-radius: 6px;
  font-variant-numeric: tabular-nums;
}
.result-pill.is-danger { background: rgba(220, 38, 38, 0.1); color: var(--color-danger); }

.secondary-toggle { margin-top: 4px; }
.secondary-toggle.is-open .secondary-toggle-collapsed { display: none; }
.stage-toggle-btn {
  display: inline-flex; align-items: center; gap: 6px; font-size: 11.5px; font-weight: 700;
  color: var(--color-text-secondary); cursor: pointer; padding: 7px 0; background: none; border: none;
  font-family: var(--font-sans);
}
.stage-toggle-chev { transition: transform 0.15s; }
.secondary-toggle.is-open .stage-toggle-chev { transform: rotate(90deg); }
.stage-detail-body { display: none; margin-top: 6px; }
.secondary-toggle.is-open .stage-detail-body { display: block; }

.timeline-wrap { padding: 16px; border: 1px solid var(--color-border); border-radius: 10px; }
.timeline-track { display: flex; align-items: center; position: relative; }
.timeline-step { flex: 1; text-align: center; position: relative; z-index: 1; }
.timeline-step .step-dot {
  width: 28px; height: 28px; border-radius: 50%;
  background: var(--color-positive); color: #fff;
  display: flex; align-items: center; justify-content: center;
  margin: 0 auto 6px;
}
.timeline-step .step-dot.is-pending { background: #d4d4d4; color: #fff; }
.timeline-step .step-dot.is-danger { background: var(--color-danger); }
.timeline-step .step-dot svg { width: 14px; height: 14px; }
.timeline-step .step-label { font-size: 12px; font-weight: 600; }
.timeline-step .step-label.is-pending { color: var(--color-text-quaternary); font-weight: 500; }
.timeline-step .step-time { font-size: 11px; color: var(--color-text-quaternary); font-variant-numeric: tabular-nums; }
.timeline-connector { flex: 1; height: 2px; background: var(--color-positive); position: relative; top: -14px; z-index: 0; }
.timeline-connector.is-pending { background: #d4d4d4; }

.signature-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
.signature-card { padding: 16px; border: 1px solid var(--color-border); border-radius: 10px; }
.sig-header { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; }
.sig-icon {
  width: 28px; height: 28px; border-radius: 50%;
  background: rgba(22, 163, 74, 0.12);
  display: flex; align-items: center; justify-content: center;
  color: var(--color-positive); flex-shrink: 0;
}
.sig-icon svg { width: 14px; height: 14px; }
.sig-name { font-size: 14px; font-weight: 600; }
.sig-username { font-size: 11px; color: var(--color-text-quaternary); font-variant-numeric: tabular-nums; }
.sig-role { font-size: 12px; color: var(--color-text-tertiary); }
.sig-time { font-size: 12px; color: var(--color-text-quaternary); font-variant-numeric: tabular-nums; }
.sig-meaning { margin-top: 6px; font-size: 11px; color: var(--color-text-quaternary); font-style: italic; }
.sig-comment { margin-top: 4px; font-size: 11px; color: var(--color-text-secondary); }

.report-footer { text-align: center; padding-top: 16px; border-top: 1px solid var(--color-border); margin-top: 8px; }
.report-footer div { font-size: 11px; color: var(--color-text-quaternary); }
.report-footer div:last-child { margin-top: 2px; font-variant-numeric: tabular-nums; }

.print-btn {
  position: fixed; bottom: 24px; right: 24px;
  padding: 12px 20px;
  background: #111; color: #fff; border: none; border-radius: 8px;
  font-family: var(--font-sans); font-size: 14px; font-weight: 500;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0,0,0,0.15);
  display: inline-flex; align-items: center; gap: 6px;
}
.print-btn:hover { transform: translateY(-1px); box-shadow: 0 6px 16px rgba(0,0,0,0.2); }

@media print {
  * { -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }

  @page { size: A4 portrait; margin: 18mm 16mm 22mm 16mm; }

  .report-root { background: #fff !important; font-size: 10pt; line-height: 1.45; min-height: 0; }
  .print-btn { display: none !important; }
  .no-print { display: none !important; }

  .report-wrapper {
    max-width: 100% !important; margin: 0 !important; padding: 0 !important;
    box-shadow: none !important; border: none !important; border-radius: 0 !important;
    background: #fff !important;
  }

  .report-header { border-bottom: 1.5pt solid #000 !important; padding-bottom: 10pt !important; margin-bottom: 14pt !important; }
  .report-header-left .label { font-size: 7pt !important; color: #555 !important; }
  .report-header-left .sample-id { font-size: 22pt !important; color: #000 !important; }
  .report-header-left .subtitle { font-size: 9pt !important; color: #333 !important; }
  .report-header-left .subtitle strong { color: #000 !important; }

  .status-badge { border: 1pt solid #000 !important; background: #e8e8e8 !important; color: #000 !important; font-size: 9pt !important; }
  .status-badge svg { stroke: #000 !important; }
  .header-date { font-size: 8pt !important; color: #555 !important; }

  .section-card {
    border: 0.75pt solid #bbb !important; border-radius: 4pt !important;
    padding: 10pt !important; margin-bottom: 10pt !important;
    page-break-inside: avoid !important;
  }
  .section-label { font-size: 7pt !important; color: #555 !important; margin-bottom: 6pt !important; }

  .data-grid { font-size: 9pt !important; }
  .data-grid .key { color: #555 !important; }
  .data-grid .value { color: #000 !important; }

  .prep-grid { grid-template-columns: repeat(4, 1fr) !important; gap: 10pt !important; }
  .prep-item .prep-label { font-size: 7pt !important; color: #555 !important; }
  .prep-item .prep-value { font-size: 9pt !important; color: #000 !important; }
  .prep-item .prep-sub { font-size: 7pt !important; color: #555 !important; }

  .section-divider .line { background: #bbb !important; }

  .test-card { border: 0.75pt solid #000 !important; border-radius: 4pt !important; page-break-inside: avoid !important; margin-bottom: 8pt !important; }
  .test-card.is-superseded { opacity: 1 !important; border-style: dashed !important; }
  .test-header { background: #f0f0f0 !important; border-bottom: 0.75pt solid #bbb !important; padding: 8pt 10pt !important; }
  .test-icon { background: #e8e8e8 !important; border: 0.5pt solid #000 !important; color: #000 !important; width: 22pt !important; height: 22pt !important; border-radius: 3pt !important; }
  .test-icon svg { stroke: #000 !important; width: 12pt !important; height: 12pt !important; }
  .test-title { font-size: 11pt !important; color: #000 !important; }
  .test-subtitle { font-size: 8pt !important; color: #555 !important; }
  .test-result-value { font-size: 18pt !important; color: #000 !important; }
  .test-result-unit { font-size: 8pt !important; color: #555 !important; }

  /* Collapsible card shell: the interactive header/toggles are .no-print
     (hidden above); force every collapsed body open and swap in the
     static print header so nothing needs a click to be readable on paper.
     Exception: a toggle with collapsedContent (the location table) stays
     summarized rather than expanded - the pills already carry all six
     results, and the full table is more detail than a printed report
     needs. Stage-details toggles have no collapsedContent, so they still
     force open per the rule above. */
  .print-only { display: flex !important; }
  .test-card-print-header { background: #f0f0f0 !important; border-bottom: 0.75pt solid #bbb !important; padding: 8pt 10pt !important; }
  .test-card-body { display: block !important; border-top: none !important; background: #fff !important; padding: 8pt 10pt !important; }
  .stage-detail-body { display: block !important; }
  .secondary-toggle-collapsed { display: block !important; }
  .secondary-toggle-collapsed + .stage-detail-body { display: none !important; }
  .result-pills { margin-bottom: 8pt !important; }
  .result-pill { border: 0.5pt solid #000 !important; background: #fff !important; color: #000 !important; font-size: 7pt !important; padding: 2pt 6pt !important; }
  .result-pill.is-danger { border-color: #000 !important; }

  .incubation-row { border-top: 0.5pt solid #bbb !important; padding: 8pt 10pt !important; gap: 10pt !important; }
  .incubation-item .inc-label { font-size: 7pt !important; color: #555 !important; }
  .incubation-item .inc-value { font-size: 9pt !important; color: #000 !important; }

  .plate-readings { border-top: 0.5pt solid #bbb !important; padding: 8pt 10pt !important; background: #fafafa !important; }
  .plate-readings-label { font-size: 7pt !important; color: #555 !important; }
  .plate-stats { gap: 8pt !important; }
  .plate-stat { padding: 6pt !important; border: 0.5pt solid #bbb !important; border-radius: 3pt !important; background: #fff !important; }
  .plate-stat .stat-label { font-size: 7pt !important; color: #555 !important; }
  .plate-stat .stat-value { font-size: 14pt !important; color: #000 !important; }
  .plate-meta { font-size: 8pt !important; color: #333 !important; margin-top: 6pt !important; }
  .plate-meta strong { color: #000 !important; }

  .observation-row { border-top: 0.5pt solid #bbb !important; padding: 8pt 10pt !important; background: #fafafa !important; }
  .observation-item { font-size: 8pt !important; color: #000 !important; }
  .observation-item .obs-meta { font-size: 7pt !important; color: #555 !important; }

  .test-footer { border-top: 0.5pt solid #bbb !important; background: #f0f0f0 !important; padding: 5pt 10pt !important; font-size: 8pt !important; color: #333 !important; }
  .test-footer strong { color: #000 !important; }
  .test-footer .pass-tag { color: #000 !important; }
  .test-footer .pass-tag svg { stroke: #000 !important; width: 9pt !important; height: 9pt !important; }

  .timeline-wrap { border: 0.75pt solid #bbb !important; border-radius: 4pt !important; padding: 10pt !important; page-break-inside: avoid !important; }
  .timeline-step .step-dot { width: 20pt !important; height: 20pt !important; background: #000 !important; color: #fff !important; margin-bottom: 3pt !important; }
  .timeline-step .step-dot.is-pending { background: #fff !important; border: 0.75pt solid #999 !important; }
  .timeline-step .step-dot svg { width: 10pt !important; height: 10pt !important; stroke: #fff !important; }
  .timeline-step .step-dot.is-pending svg { stroke: #999 !important; }
  .timeline-step .step-label { font-size: 8pt !important; color: #000 !important; }
  .timeline-step .step-label.is-pending { color: #777 !important; }
  .timeline-step .step-time { font-size: 7pt !important; color: #555 !important; }
  .timeline-connector { height: 1pt !important; background: #000 !important; top: -10pt !important; }
  .timeline-connector.is-pending { background: #ccc !important; }

  .signature-grid { gap: 10pt !important; }
  .signature-card { border: 0.75pt solid #000 !important; border-radius: 4pt !important; padding: 10pt !important; page-break-inside: avoid !important; }
  .sig-icon { width: 20pt !important; height: 20pt !important; background: #e8e8e8 !important; border: 0.5pt solid #000 !important; color: #000 !important; }
  .sig-icon svg { width: 10pt !important; height: 10pt !important; stroke: #000 !important; }
  .sig-name { font-size: 10pt !important; color: #000 !important; }
  .sig-username { font-size: 7pt !important; color: #555 !important; }
  .sig-role { font-size: 8pt !important; color: #555 !important; }
  .sig-time { font-size: 8pt !important; color: #555 !important; }
  .sig-meaning { font-size: 7pt !important; color: #777 !important; }
  .sig-comment { font-size: 7pt !important; color: #333 !important; }

  .report-footer { border-top: 0.75pt solid #000 !important; padding-top: 8pt !important; margin-top: 12pt !important; }
  .report-footer div { font-size: 7pt !important; color: #555 !important; }
}

/* Growth Promotion challenge cards - one card per organism, reorganized
   per the approved media-lot-organism-reorg mockup. Scoped under .gp-*
   so it never collides with the plain .observation-item rows the other
   three evaluation types (Inhibition/Indication/EnrichmentCharacteristics)
   still use unchanged. */
.gp-card { background: #fff; border: 1px solid var(--color-border); border-radius: 9px; padding: 13px 16px; }
.gp-card + .gp-card { margin-top: 8px; }
.gp-row1 { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; }
.gp-name { font-size: 13.5px; font-weight: 800; font-style: italic; }
.gp-inoc { font-size: 12px; color: var(--color-text-secondary); }
.gp-inoc b { color: var(--color-text-primary); font-weight: 700; }
.gp-cryo {
  margin-left: auto; font-variant-numeric: tabular-nums; font-size: 11.5px;
  color: #5B21B6; background: #F3EEFC; padding: 2px 9px; border-radius: 5px; white-space: nowrap;
}
.gp-row2 {
  display: flex; align-items: center; gap: 10px; margin-top: 9px; padding-top: 9px;
  border-top: 1px dashed var(--color-border); font-size: 12px; flex-wrap: wrap;
}
.gp-lot-pill { display: flex; align-items: center; gap: 6px; background: var(--color-surface); border: 1px solid var(--color-border); border-radius: 6px; padding: 5px 10px; }
.gp-lot-pill .lc { font-variant-numeric: tabular-nums; font-size: 11.5px; font-weight: 700; }
.gp-lot-pill .lv { font-size: 11.5px; color: var(--color-text-secondary); }
.gp-arrow { color: var(--color-text-quaternary); font-weight: 700; }
.gp-recovery { font-size: 12.5px; font-weight: 800; color: var(--color-positive); font-variant-numeric: tabular-nums; }
.gp-recovery.is-danger { color: var(--color-danger); }
.gp-conform-badge {
  margin-left: auto; display: flex; align-items: center; gap: 5px;
  background: rgba(22, 163, 74, 0.1); color: var(--color-positive); font-size: 11px; font-weight: 800;
  padding: 3px 11px; border-radius: 20px; white-space: nowrap;
}
.gp-conform-badge svg { width: 11px; height: 11px; }
.gp-conform-badge.is-danger { background: rgba(220, 38, 38, 0.1); color: var(--color-danger); }
.gp-flag {
  font-size: 9.5px; font-weight: 700; color: var(--color-warning); background: rgba(217, 119, 6, 0.12);
  padding: 1px 6px; border-radius: 4px; border: 1px solid rgba(217, 119, 6, 0.4); white-space: nowrap;
}
.gp-meta { font-size: 10.5px; color: var(--color-text-quaternary); margin-top: 6px; }

@media print {
  .gp-card { border: 0.75pt solid #bbb !important; border-radius: 4pt !important; padding: 8pt 10pt !important; page-break-inside: avoid !important; }
  .gp-card + .gp-card { margin-top: 5pt !important; }
  .gp-name { font-size: 9.5pt !important; color: #000 !important; }
  .gp-inoc { font-size: 8pt !important; color: #333 !important; }
  .gp-inoc b { color: #000 !important; }
  .gp-cryo { color: #000 !important; background: #eee !important; border: 0.5pt solid #999 !important; font-size: 8pt !important; }
  .gp-row2 { border-top: 0.5pt dashed #bbb !important; margin-top: 6pt !important; padding-top: 6pt !important; }
  .gp-lot-pill { background: #fafafa !important; border: 0.5pt solid #bbb !important; }
  .gp-lot-pill .lc { color: #000 !important; }
  .gp-lot-pill .lv { color: #555 !important; }
  .gp-recovery { color: #000 !important; }
  .gp-conform-badge { border: 0.5pt solid #000 !important; background: #fff !important; color: #000 !important; font-size: 7.5pt !important; }
  .gp-flag { color: #000 !important; background: #eee !important; border: 0.5pt solid #999 !important; font-size: 7pt !important; }
  .gp-meta { font-size: 7pt !important; color: #555 !important; }
}

/* Certificate of Analysis - ported from the approved COA mockup. Scoped
   under .coa-root/.coa-page so its variable names never collide with the
   Sample Summary Report styles above, even though the two pages never
   render at the same time. */
.coa-root {
  --coa-header: #0B3B2E; --coa-green: #0F6B3F; --coa-green-bg: #E4F5EB;
  --coa-red: #B3261E; --coa-red-bg: #FCEBEA;
  --coa-border: #D7DBE0; --coa-ink: #111827; --coa-ink2: #4B5563; --coa-ink3: #8A93A0;
  font-family: var(--font-sans);
  font-size: 13px;
  color: var(--coa-ink);
  background: #F7F8F9;
  min-height: 100vh;
}
.coa-page { max-width: 760px; margin: 0 auto; padding: 36px 40px; background: #fff; }

.coa-head { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 3px solid var(--coa-header); padding-bottom: 16px; margin-bottom: 20px; }
.coa-title { font-size: 20px; font-weight: 800; letter-spacing: .3px; }
.coa-sub { font-size: 11.5px; color: var(--coa-ink3); margin-top: 3px; }
.coa-doc-id { text-align: right; font-variant-numeric: tabular-nums; font-size: 12px; color: var(--coa-ink2); }

.coa-id-strip { display: grid; grid-template-columns: repeat(4, 1fr); gap: 14px; margin-bottom: 22px; padding-bottom: 18px; border-bottom: 1px solid var(--coa-border); }
.coa-id-strip .il { font-size: 10.5px; color: var(--coa-ink3); text-transform: uppercase; letter-spacing: .4px; }
.coa-id-strip .iv { font-size: 13px; font-weight: 700; margin-top: 2px; }

/* Product/RM/PM COA - no location dimension, so the item identity and
   date fields render as a strip + grid instead of the located branch's
   coa-id-strip, and results render as a plain per-test table instead of
   table.coa-matrix. */
.coa-item-strip { margin-bottom: 22px; padding-bottom: 18px; border-bottom: 1px solid var(--coa-border); }
.coa-item-name-row { display: flex; justify-content: space-between; align-items: baseline; gap: 12px; margin-bottom: 14px; }
.coa-item-name { font-size: 16px; font-weight: 800; }
.coa-item-sub { font-size: 11.5px; color: var(--coa-ink3); margin-top: 2px; }
.coa-item-qty { font-size: 12.5px; font-weight: 700; color: var(--coa-ink2); white-space: nowrap; }

.coa-dates-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 14px; }
.coa-dates-grid .il { font-size: 10.5px; color: var(--coa-ink3); text-transform: uppercase; letter-spacing: .4px; }
.coa-dates-grid .iv { font-size: 13px; font-weight: 700; margin-top: 2px; }

.coa-remarks-box { font-size: 12.5px; color: var(--coa-ink); background: #F7F8F9; border: 1px solid var(--coa-border); border-radius: 8px; padding: 12px 14px; margin-bottom: 22px; white-space: pre-wrap; }
.coa-remarks-box.is-empty { color: var(--coa-ink3); font-style: italic; }

table.coa-simple { width: 100%; border-collapse: collapse; margin-bottom: 6px; }
table.coa-simple th { text-align: left; font-size: 10.5px; font-weight: 800; color: var(--coa-ink); padding: 8px 10px; border: 1px solid var(--coa-border); background: #F0F2F4; }
table.coa-simple td { padding: 8px 10px; border: 1px solid var(--coa-border); font-size: 12px; }
table.coa-simple td.r-pass { color: var(--coa-green); font-weight: 700; }
table.coa-simple td.r-fail { color: var(--coa-red); font-weight: 700; background: var(--coa-red-bg); }

.coa-section-h { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .5px; color: var(--coa-ink3); margin-bottom: 10px; }

table.coa-matrix { width: 100%; min-width: 620px; border-collapse: collapse; margin-bottom: 6px; }
table.coa-matrix th {
  text-align: center; font-size: 10.5px; font-weight: 800; color: var(--coa-ink);
  padding: 8px 6px; border: 1px solid var(--coa-border); white-space: normal; background: #F0F2F4;
}
table.coa-matrix th.grp { font-size: 11.5px; background: #E4E7EB; }
table.coa-matrix th.loc-col { text-align: left; background: #F0F2F4; white-space: nowrap; }
table.coa-matrix th.sub { font-size: 9.5px; font-weight: 700; color: var(--coa-ink2); text-transform: none; background: #F7F8F9; }
table.coa-matrix th.sub.spec-req { color: var(--coa-ink3); font-style: italic; font-weight: 600; }
table.coa-matrix td { padding: 8px 6px; border: 1px solid var(--coa-border); font-size: 12px; text-align: center; font-variant-numeric: tabular-nums; }
table.coa-matrix td.loc-col { text-align: left; font-family: var(--font-sans); font-weight: 700; font-size: 12px; }
table.coa-matrix .r-pass { color: var(--coa-green); font-weight: 700; }
table.coa-matrix .r-fail { color: var(--coa-red); font-weight: 700; background: var(--coa-red-bg); }
table.coa-matrix .lim-dim { color: var(--coa-ink3); font-size: 11px; }
table.coa-matrix .coa-unit-sub { font-family: var(--font-sans); font-size: 9px; font-weight: 500; color: var(--coa-ink3); margin-top: 1px; }

.coa-footnote { font-size: 11px; color: var(--coa-ink3); margin: 8px 0 22px; }

.coa-overall { background: var(--coa-green-bg); border: 1px solid #7FCBA0; border-radius: 8px; padding: 16px 18px; margin-bottom: 26px; }
.coa-overall.is-fail { background: var(--coa-red-bg); border-color: #E7A19C; }
.coa-overall .ot { font-size: 10.5px; font-weight: 800; text-transform: uppercase; letter-spacing: .5px; color: var(--coa-green); margin-bottom: 5px; }
.coa-overall.is-fail .ot { color: var(--coa-red); }
.coa-overall .od { font-size: 13.5px; font-weight: 600; color: var(--coa-ink); }

.coa-sig-strip { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-top: 26px; padding-top: 20px; border-top: 1px solid var(--coa-border); }
.coa-sig-block .sn { font-size: 13px; font-weight: 700; }
.coa-sig-block .sr { font-size: 11px; color: var(--coa-ink3); margin-top: 1px; }
.coa-sig-block .sm { font-size: 11px; color: var(--coa-ink2); font-style: italic; margin-top: 6px; line-height: 1.5; }
.coa-sig-block .st { font-size: 10.5px; color: var(--coa-ink3); margin-top: 6px; font-variant-numeric: tabular-nums; }

.coa-footer-note { font-size: 9.5px; color: var(--coa-ink3); text-align: center; margin-top: 30px; line-height: 1.6; }

@media print {
  .coa-root { background: #fff !important; min-height: 0; font-size: 10pt; }
  .coa-page { max-width: 100% !important; margin: 0 !important; padding: 0 !important; }

  .coa-head { border-bottom: 2pt solid #000 !important; padding-bottom: 10pt !important; margin-bottom: 14pt !important; }
  .coa-title { font-size: 16pt !important; color: #000 !important; }
  .coa-sub { font-size: 8pt !important; color: #555 !important; }
  .coa-doc-id { font-size: 9pt !important; color: #333 !important; }

  .coa-id-strip { gap: 10pt !important; margin-bottom: 14pt !important; padding-bottom: 10pt !important; }
  .coa-id-strip .il { font-size: 7pt !important; color: #555 !important; }
  .coa-id-strip .iv { font-size: 9pt !important; color: #000 !important; }

  .coa-item-strip { margin-bottom: 14pt !important; padding-bottom: 10pt !important; }
  .coa-item-name { font-size: 13pt !important; }
  .coa-item-sub { font-size: 8pt !important; color: #555 !important; }
  .coa-item-qty { font-size: 9.5pt !important; color: #333 !important; }
  .coa-dates-grid { gap: 10pt !important; }
  .coa-dates-grid .il { font-size: 7pt !important; color: #555 !important; }
  .coa-dates-grid .iv { font-size: 9pt !important; color: #000 !important; }
  .coa-remarks-box { font-size: 9pt !important; padding: 8pt 10pt !important; margin-bottom: 14pt !important; border-color: #999 !important; }
  table.coa-simple th { font-size: 7pt !important; padding: 5pt 4pt !important; border-color: #999 !important; background: #eee !important; }
  table.coa-simple td { font-size: 8pt !important; padding: 5pt 4pt !important; border-color: #999 !important; }
  table.coa-simple td.r-fail { background: #f5d9d7 !important; }

  .coa-section-h { font-size: 8pt !important; color: #555 !important; margin-bottom: 6pt !important; }

  table.coa-matrix th { font-size: 7pt !important; padding: 5pt 4pt !important; border-color: #999 !important; background: #eee !important; }
  table.coa-matrix th.grp { font-size: 7.5pt !important; background: #ddd !important; }
  table.coa-matrix th.loc-col { background: #eee !important; }
  table.coa-matrix td { font-size: 8pt !important; padding: 5pt 4pt !important; border-color: #999 !important; }
  table.coa-matrix .r-fail { background: #f5d9d7 !important; }
  table.coa-matrix .coa-unit-sub { font-size: 6.5pt !important; color: #666 !important; }

  .coa-footnote { font-size: 7pt !important; margin: 5pt 0 14pt !important; }

  .coa-overall { border: 0.75pt solid #000 !important; border-radius: 3pt !important; padding: 10pt !important; margin-bottom: 16pt !important; page-break-inside: avoid !important; }
  .coa-overall .ot { font-size: 7pt !important; }
  .coa-overall .od { font-size: 9pt !important; color: #000 !important; }

  .coa-sig-strip { gap: 14pt !important; margin-top: 16pt !important; padding-top: 12pt !important; }
  .coa-sig-block .sn { font-size: 9pt !important; color: #000 !important; }
  .coa-sig-block .sr { font-size: 7pt !important; color: #555 !important; }
  .coa-sig-block .sm { font-size: 7pt !important; color: #333 !important; }
  .coa-sig-block .st { font-size: 7pt !important; color: #555 !important; }

  .coa-footer-note { font-size: 6.5pt !important; margin-top: 18pt !important; }
}
`;
