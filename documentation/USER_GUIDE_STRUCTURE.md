# MicroLIMS User Guide — Structure (Phase 8)

Based on the prompt's Phase 8 list, renumbered to insert three sections for modules found in the code but not in that list (marked **new**). Section files live in `documentation/guide/NN-slug.md` and are assembled into `USER_GUIDE.md`.

Screenshot IDs: `SS-<section><n>` with the section number as two digits and n = 1–9 (e.g. §12 → SS-121, SS-122). This keeps IDs stable while sections are written in parallel; `SCREENSHOT_PLAN.md` (Phase 10) uses the same IDs.

| § | Title | Modules | Main sources (cluster / review) | Writer job |
|---|---|---|---|---|
| 1 | Introduction | — | 01_APPLICATION_MAP §1 | WA |
| 2 | Purpose and Scope | — | 01_APPLICATION_MAP, COVERAGE_MATRIX | WA |
| 3 | System Overview | all (overview) | 01_APPLICATION_MAP §1–3 | WA |
| 4 | User Roles and Permissions | MOD-ROLES-001 | knowledge/role-guide.md (Phase 7), C1 | WA |
| 5 | Login, Session and Password | MOD-AUTH-001 | C1, R1 | WA |
| 6 | User Interface | MOD-NAV-001 | C1 | WA |
| 7 | Navigation | MOD-NAV-001 | C1, 01_APPLICATION_MAP §3 | WA |
| 8 | Dashboard | MOD-DASH-001 | C1, R1 | WA |
| 9 | Notifications, Messages and Discussions (**new**) | MOD-NOTIF-001, MOD-MSG-001, MOD-DISC-001 | C1 | WA |
| 10 | Search and Filters | cross-cutting (list filters, Record Search, Audit Search) | C1, C2, C8 | WA |
| 11 | Sample Management and Tracking Board | MOD-SAMPLE-001, MOD-TRACK-001 | C2, R2 | WB |
| 12 | Sample Receiving | MOD-SAMPLE-001 | C2, R2 | WB |
| 13 | Test Orders and Analyst Assignment | MOD-SAMPLE-001, MOD-TESTING-001 | C2, R2 | WB |
| 14 | Analyst Testing Workspace and Sample Preparation | MOD-TESTING-001, MOD-PREP-001 | C2, R2 | WB |
| 15 | Result Entry (count, observation and location-grid tests) | MOD-COUNT-001, MOD-OBS-001 | C2, R2 | WB |
| 16 | Pathogen Testing | MOD-PATH-001 | C2, R2 | WB |
| 17 | Review | MOD-REVIEW-001 | C3, R3 | WC |
| 18 | Return to Analyst, Rework and Retest | MOD-REVIEW-001, MOD-APPROVAL-001 | C3, R3 | WC |
| 19 | Approval | MOD-APPROVAL-001 | C3, R3 | WC |
| 20 | OOS Tracking (**new**) | MOD-OOS-001 | C3, R3 | WC |
| 21 | Sample Report, Certificate of Analysis and Archived Records (Final Release: not implemented) | MOD-COA-001, MOD-ARCHIVE-001 | C3, C1, R3 | WC |
| 22 | Growth Promotion Testing and Media | MOD-MEDIA-001, MOD-MEDIACFG-001 | C5, C4, R5, R4 | WD |
| 23 | Environmental Monitoring | MOD-EM-001 | C2 (WF-SAMPLE-006), C4, R4 | WD |
| 24 | Water Monitoring | MOD-WATER-001 | C2 (WF-SAMPLE-007), C4, R4 | WD |
| 25 | After Cleaning Monitoring | MOD-AC-001 | C2 (WF-SAMPLE-008), C4, R4 | WD |
| 26 | Reference Strains (Reference Cryovials) | MOD-CRYO-001 | C5, R5 | WD |
| 27 | Physicochemical Testing (FP and RM & PM workspaces, HPLC, GC, ICP-OES, titration, other result types) | MOD-PHYSRESULT-001, MOD-TITR-001, MOD-HPLC-001, MOD-GC-001, MOD-ICP-001, MOD-HPLCM-001, MOD-ICPM-001 | C6, R6 | WE |
| 28 | Solution and Standard Preparation | MOD-SOLN-001, MOD-SOLNM-001, MOD-REAGENT-001, MOD-WS-001 | C6, R6 | WE |
| 29 | Material Stock | MOD-MATERIAL-001 | C5, R5 | WE |
| 30 | Equipment (inventory, configuration, chromatography columns) | MOD-EQUIP-001, MOD-EQUIP-002, MOD-COLUMN-001 | C5, C4, C6, R5 | WF |
| 31 | Item Master, Test Master and Specifications | MOD-ITEM-001, MOD-SPEC-001, MOD-TESTMASTER-001, MOD-EQTYPE-001 | C7, C4, C6, R7 | WF |
| 32 | Laboratory Configuration (Organisms, Receiving Configuration, Master Data Workbook) | MOD-ORG-001, MOD-RECVCFG-001, MOD-WORKBOOK-001 | C4, C7, R7 | WF |
| 33 | Laboratories, Departments and Data Segregation | lab membership, physchem areas, Document Control sections | C1, verified-roles §7, C8 | WG |
| 34 | Reports, KPIs and Trend Analysis | MOD-REPORTS-001, MOD-KPI-001 | C8, R8 | WH |
| 35 | Audit Trail | MOD-AUDIT-001 | C1, R1 | WG |
| 36 | Traceability | MOD-AUDIT-001 (chain view) + module traceability sections | C1, all `## traceability` | WG |
| 37 | Electronic Signatures and Approval Controls | MOD-SIG-001 | C1, all `## audit-trail`, verified-roles §5–6 | WG |
| 38 | Document Control | MOD-DC-001..004 | C8, R8 | WH |
| 39 | Users, Roles and Administration | MOD-USERS-001, MOD-ROLES-001 | C1, R1 | WG |
| 40 | Error Monitoring (Incidents and Error Log) | MOD-ERR-001 | C1, R1 | WG |
| 41 | Integrations | MOD-INSTIMPORT-001 | C6 | WG |
| 42 | Troubleshooting | MOD-LEGACY-001 + common errors | knowledge/validations.md, all | WI |
| 43 | Frequently Asked Questions | — | all | WI |
| 44 | Glossary | — | knowledge/terminology.md | WI |

## Not given a user section
Backend-only modules without screens: MOD-AUDIT-002, MOD-SECAUDIT-001, MOD-SIG-002, MOD-REPORTS-002, MOD-KPI-002, MOD-DC-005, MOD-RESULT-001. They are listed in §2 (scope) as not available in the user interface.

## Not implemented (no procedure written; see DOCUMENTATION_GAPS.md)
Final Release step / permission, sterility test, EM annual plan and schedules, water sampling frequencies and plan, reference-strain passage tracking, equipment qualification records, incident / deviation / CAPA module, separate Titration Methods master, SAP / ERP integration, AI assistant, live instrument data feed, Reception and QA roles.
