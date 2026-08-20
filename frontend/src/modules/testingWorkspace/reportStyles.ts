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
     static print header so nothing needs a click to be readable on paper. */
  .print-only { display: flex !important; }
  .test-card-print-header { background: #f0f0f0 !important; border-bottom: 0.75pt solid #bbb !important; padding: 8pt 10pt !important; }
  .test-card-body { display: block !important; border-top: none !important; background: #fff !important; padding: 8pt 10pt !important; }
  .stage-detail-body { display: block !important; }
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

  .location-table-wrap { border-top: 0.5pt solid #bbb !important; }
  .location-table { font-size: 7.5pt !important; }
  .location-table th { padding: 5pt 8pt !important; font-size: 6.5pt !important; background: #f0f0f0 !important; border-bottom: 0.5pt solid #bbb !important; }
  .location-table td { padding: 5pt 8pt !important; border-bottom: 0.5pt solid #bbb !important; color: #000 !important; }
  .location-status-chip { padding: 2pt 6pt !important; border-radius: 3pt !important; font-size: 6.5pt !important; }

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
`;
