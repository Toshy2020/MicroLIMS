# MicroLIMS User Guide — Coverage Matrix (Phase 2)

| Item | Value |
|---|---|
| Source | `01_APPLICATION_MAP.md` §2 (modules), §4 (screens), §7 (workflows), §5 (roles) |
| Phase | Phase 2 complete, awaiting approval. Screenshot IDs are all TBD until `SCREENSHOT_PLAN.md` (Phase 10). |
| Guide section | Planned section number from the Phase 8 list in the governing prompt; "new section" where that list has no matching section |
| Roles column | DEFAULT grants (roles are admin-editable). Permission codes named in this column are quoted from the application. Codes marked NOT-ENFORCED change nothing. |
| Status label | COVERED = inventoried to field level; PARTIAL = exists, some detail not yet read; NOT-EXPOSED-IN-UI; LEGACY/RETIRED; REQUIRES CONFIRMATION |
| Found in code | Y / N / Partial, as seen in the repository at commit f773042c |
| Completeness | Every module row has a status, screens (or "none" for backend-only), workflows (or "none"), roles, planned guide section, screenshot placeholder and gap references. Rows with detail still open are the PARTIAL, REQUIRES CONFIRMATION and NOT-EXPOSED-IN-UI rows, each tied to entries in `DOCUMENTATION_GAPS.md`. Workflow detail (Phase 4) and screenshot IDs (Phase 10) are intentionally not filled yet. |

| Module | Found in code (Y/N/Partial) | Screens | Workflows | Roles | Guide section | Screenshot IDs | Status label | Open questions |
|---|---|---|---|---|---|---|---|---|
| MOD-AUTH-001 Authentication, session, password | Y | SCR-AUTH-001, SCR-AUTH-002, SCR-AUTH-003, SCR-AUTH-004, SCR-AUTH-005, SCR-AUTH-006 | WF-AUTH-001, WF-AUTH-002, WF-AUTH-003, WF-AUTH-004, WF-AUTH-005 | Public pages (sign in, recovery); all signed-in users (password, profile) | §5 Login, Session and Password | TBD | COVERED | GAP-012, GAP-023, GAP-053 |
| MOD-NAV-001 Application shell and navigation | Y | SCR-NAV-001, SCR-NAV-002 | none | All signed-in users; links filtered by permission and laboratory membership | §6 User Interface; §7 Navigation | TBD | COVERED | GAP-035, GAP-039 |
| MOD-NOTIF-001 Notifications | Partial | SCR-NOTIF-001 | WF-NOTIF-001 | All signed-in users | new section (Notifications) next to §8 | TBD | PARTIAL | GAP-047 |
| MOD-DASH-001 Dashboards | Partial | SCR-DASH-001, SCR-DASH-002, SCR-DASH-003, SCR-DASH-004 | none | Variant by role type: Administrator, Analyst, Reviewer, Section Head | §8 Dashboard | TBD | PARTIAL | GAP-043, GAP-055 |
| MOD-USERS-001 User management | Partial | SCR-USERS-001, SCR-USERS-002 | WF-USERS-001, WF-USERS-002 | Users.Manage (default: System Administrator) | §37 Users, Roles and Administration; §31 | TBD | PARTIAL | GAP-015, GAP-043 |
| MOD-ROLES-001 Roles and permissions | Y | SCR-ROLES-001, SCR-ROLES-002, SCR-ROLES-003 | WF-ROLES-001 | Roles.Manage (default: System Administrator) | §4 User Roles and Permissions; §37 | TBD | COVERED | GAP-011, GAP-015, GAP-016, GAP-050 |
| MOD-SIG-001 Electronic signatures and segregation of duties | Y | SCR-SIG-001 | none | Any user performing a signed action | §35 Electronic Signatures and Approval Controls | TBD | COVERED | GAP-009, GAP-010, GAP-023, GAP-026, GAP-027, GAP-028 |
| MOD-AUDIT-001 Audit Trail (Audit Search) and traceability chain | Partial | SCR-AUDIT-001 | WF-AUDIT-001 | Audit.View (default: System Administrator, Section Head) | §33 Audit Trail; §34 Traceability | TBD | PARTIAL | GAP-043 |
| MOD-ERR-001 Error Monitoring | Y | SCR-ERR-001 | WF-ERR-001 | System.ViewErrorLog (default: System Administrator) | §38 Incidents and Error Log | TBD | COVERED | GAP-046, GAP-047 |
| MOD-MSG-001 Messages | Y | SCR-MSG-001 | WF-MSG-001 | All signed-in users (Messages.Use is NOT-ENFORCED) | new section (Messages and Discussions) | TBD | COVERED | GAP-011 |
| MOD-DISC-001 Discussions | Y | SCR-DISC-001, SCR-DISC-002 | WF-DISC-001 | All signed-in users; edit-any needs Discussions.EditAny | new section (Messages and Discussions) | TBD | COVERED | GAP-011 |
| MOD-ARCHIVE-001 Archived records | Partial | none | none | All signed-in users (no route guard) | §19 Final Release and Final Report | TBD | PARTIAL | GAP-035, GAP-043 |
| MOD-SAMPLE-001 Sample receiving and register | Partial | SCR-SAMPLE-001, SCR-SAMPLE-002, SCR-SAMPLE-003, SCR-SAMPLE-004, SCR-SAMPLE-005, SCR-SAMPLE-006 | WF-SAMPLE-001, WF-SAMPLE-002, WF-SAMPLE-003, WF-SAMPLE-004, WF-SAMPLE-005 | Samples.Receive / Samples.ReceiveOwnLab; correct and void Samples.Correct; assign analyst Samples.AssignAnalyst | §10 Sample Management; §11 Sample Receiving; §12 Test Orders and Assignment | TBD | PARTIAL | GAP-001, GAP-043 |
| MOD-TRACK-001 Tracking Board | Y | SCR-TRACK-001 | none | Samples.TrackAll (default: System Administrator, Section Head) | §10 Sample Management | TBD | COVERED | GAP-001 |
| MOD-PREP-001 Sample preparation | Partial | SCR-PREP-001 | WF-PREP-001, WF-PREP-002 | Laboratory members; item configuration Items.Manage | §13 Analyst Testing Workspace (preparation); §29 | TBD | PARTIAL | GAP-001, GAP-043 |
| MOD-TESTING-001 Laboratory workspaces | Partial | SCR-TESTING-001, SCR-TESTING-002, SCR-TESTING-003, SCR-TESTING-004, SCR-TESTING-005 | WF-TESTING-001, WF-TESTING-002 | Laboratory members; TestWorkflow.Execute; supervisor actions TestWorkflow.Supervise | §12; §13 Analyst Testing Workspace | TBD | PARTIAL | GAP-001, GAP-004, GAP-006, GAP-007, GAP-008, GAP-039 |
| MOD-COUNT-001 Count tests | Y | SCR-COUNT-001 | WF-COUNT-001 | TestWorkflow.Execute (default: all four roles) | §14 Result Entry | TBD | COVERED | GAP-001, GAP-008, GAP-010 |
| MOD-OBS-001 Observation and location-grid tests | Y | SCR-OBS-001 | WF-COUNT-002, WF-OBS-001 | TestWorkflow.Execute | §14 Result Entry | TBD | COVERED | GAP-001, GAP-010 |
| MOD-PHYSRESULT-001 Physicochemical result panels | Partial | SCR-PHYSRESULT-001 | WF-PHYSRESULT-001, WF-PHYSRESULT-003 | Laboratory members (endpoint policies REQUIRES CONFIRMATION) | §25 Physicochemical Testing | TBD | PARTIAL | GAP-027, GAP-044 |
| MOD-PATH-001 Pathogen testing | Y | SCR-PATH-001, SCR-PATH-002 | WF-PATH-001, WF-PATH-002, WF-PATH-003 | TestWorkflow.Execute; reset session TestWorkflow.Supervise | §15 Pathogen Testing | TBD | COVERED | GAP-001, GAP-004, GAP-005, GAP-006, GAP-007, GAP-008, GAP-010, GAP-019, GAP-029 |
| MOD-EM-001 Environmental Monitoring | Partial | SCR-EM-001 | none | MasterData.Manage (configuration); laboratory members (testing) | §21 Environmental Monitoring | TBD | PARTIAL | GAP-001, GAP-020, GAP-036 |
| MOD-WATER-001 Water Monitoring | Partial | SCR-WATER-001 | none | MasterData.Manage (configuration); laboratory members (testing) | §22 Water Monitoring | TBD | PARTIAL | GAP-001, GAP-036 |
| MOD-AC-001 After Cleaning monitoring | Partial | SCR-AC-001 | none | MasterData.Manage (configuration); laboratory members (testing) | §23 After Cleaning Monitoring | TBD | PARTIAL | GAP-001 |
| MOD-REVIEW-001 Technical review and return to analyst | Y | SCR-REVIEW-001, SCR-REVIEW-002 | WF-REVIEW-001, WF-REVIEW-002 | Samples.Review (default: System Administrator, Section Head, Reviewer) | §16 Review; §17 Return to Analyst, Rework and Retest | TBD | COVERED | GAP-001, GAP-009, GAP-018 |
| MOD-APPROVAL-001 Approval, retest, new sample, close testing | Y | SCR-APPROVAL-001, SCR-APPROVAL-002 | WF-APPROVAL-001, WF-APPROVAL-002, WF-APPROVAL-003 | Samples.Approve (default: System Administrator, Section Head) | §18 Approval; §17 | TBD | COVERED | GAP-001, GAP-017, GAP-018 |
| MOD-COA-001 Sample report and Certificate of Analysis | Partial | SCR-COA-001, SCR-COA-002 | WF-COA-001 | All signed-in users (no route guard) | §19 Final Release and Final Report | TBD | PARTIAL | GAP-014, GAP-035, GAP-043 |
| MOD-OOS-001 OOS Tracking | Y | SCR-OOS-001 | WF-OOS-001 | Oos.Manage (default: System Administrator, Section Head) | new section (OOS Tracking) next to §17 | TBD | COVERED | GAP-017, GAP-043 |
| MOD-MEDIA-001 Media preparation, evaluation, release, storage | Partial | SCR-MEDIA-001, SCR-MEDIA-002, SCR-MEDIA-003 | WF-MEDIA-001, WF-MEDIA-002, WF-MEDIA-003, WF-MEDIA-004 | Media.Prepare (default: all four roles); release Media.Release (System Administrator, Section Head) | §20 Growth Promotion Testing and Media | TBD | PARTIAL | GAP-021, GAP-022, GAP-034, GAP-043 |
| MOD-MEDIACFG-001 Media Configurations | Partial | SCR-MEDIACFG-001 | WF-MEDIACFG-001, WF-MEDIACFG-002 | MasterData.Manage; product code change: role type Section Head only | §20; §30 Laboratory Configuration | TBD | PARTIAL | GAP-013 |
| MOD-CRYO-001 Reference cryovials (reference strains) | Partial | SCR-CRYO-001, SCR-CRYO-002 | WF-CRYO-001, WF-CRYO-002, WF-CRYO-003 | Cryovials.Manage (all four roles); approve Cryovials.Approve (System Administrator, Section Head) | §24 Reference Strains | TBD | PARTIAL | GAP-037, GAP-043 |
| MOD-ORG-001 Organisms master | Y | SCR-ORG-001 | none | MasterData.Manage (default: System Administrator, Section Head) | §30 Laboratory Configuration | TBD | COVERED | none |
| MOD-MATERIAL-001 Materials Stock | Partial | SCR-MATERIAL-001 | WF-MATERIAL-001 | Materials.Manage (default: System Administrator, Section Head, Analyst) | §27 Material Stock | TBD | PARTIAL | GAP-043 |
| MOD-EQUIP-001 Equipment Inventory | Partial | SCR-EQUIP-001 | WF-EQUIP-001 | Equipment.Manage (default: System Administrator, Section Head, Analyst) | §28 Equipment | TBD | PARTIAL | GAP-038, GAP-043 |
| MOD-EQUIP-002 Equipment configuration | Partial | SCR-EQUIP-002, SCR-EQUIP-003 | WF-EQUIP-002 | MasterData.Manage | §28 Equipment; §30 | TBD | PARTIAL | GAP-038, GAP-043 |
| MOD-COLUMN-001 Chromatography Columns | Partial | SCR-COLUMN-001 | none | MasterData.Manage | §28 Equipment | TBD | PARTIAL | GAP-043 |
| MOD-ITEM-001 Items | Partial | SCR-ITEM-001 | WF-ITEM-001, WF-ITEM-002 | Items.Manage (default: System Administrator, Section Head); documents Items.DocumentUpload | §29 Item Master, Test Master and Specifications | TBD | PARTIAL | GAP-043 |
| MOD-SPEC-001 Specifications (stand-alone page) | Partial | SCR-SPEC-001 | none | MasterData.Manage | §29 | TBD | REQUIRES CONFIRMATION | GAP-034, GAP-043 |
| MOD-TESTMASTER-001 Test Master | Partial | SCR-TESTMASTER-001, SCR-TESTMASTER-002, SCR-TESTMASTER-003 | WF-TESTMASTER-001 | MasterData.Manage | §29 | TBD | PARTIAL | GAP-043 |
| MOD-EQTYPE-001 Equation Types | Partial | SCR-EQTYPE-001 | none | MasterData.Manage | §29 | TBD | PARTIAL | GAP-042 |
| MOD-RECVCFG-001 Receiving Configuration | Y | SCR-RECVCFG-001 | none | MasterData.Manage | §30 Laboratory Configuration | TBD | COVERED | none |
| MOD-WORKBOOK-001 Master Data Workbook | Y | SCR-WORKBOOK-001 | WF-WORKBOOK-001, WF-WORKBOOK-002 | MasterData.Manage; verifier must be a second person | §30 Laboratory Configuration | TBD | COVERED | GAP-041 |
| MOD-REAGENT-001 Reagents & Reference Standards (material master) | Partial | SCR-REAGENT-001 | none | MasterData.Manage | §26 Solution and Standard Preparation | TBD | PARTIAL | GAP-043 |
| MOD-SOLNM-001 Solutions Master | Partial | SCR-SOLNM-001 | none | MasterData.Manage | §26 | TBD | PARTIAL | GAP-043 |
| MOD-HPLCM-001 Chromatography Methods Master (HPLC and GC) | Partial | SCR-HPLCM-001 | none | MasterData.Manage | §25 Physicochemical Testing | TBD | PARTIAL | GAP-043 |
| MOD-ICPM-001 ICP Methods Master | Partial | SCR-ICPM-001 | none | MasterData.Manage | §25 | TBD | PARTIAL | GAP-043 |
| MOD-HPLC-001 HPLC Workspace | Partial | SCR-HPLC-001, SCR-HPLC-002, SCR-HPLC-003, SCR-HPLC-004, SCR-HPLC-005 | WF-HPLC-001 | No route guard; Hplc.Operate for writes (default: System Administrator, Section Head, Analyst) | §25 | TBD | PARTIAL | GAP-026, GAP-035, GAP-043 |
| MOD-GC-001 GC Workspace | Partial | SCR-GC-001 | WF-GC-001 | As HPLC | §25 | TBD | PARTIAL | GAP-035, GAP-043 |
| MOD-ICP-001 ICP-OES Workspace | Partial | SCR-ICP-001, SCR-ICP-002, SCR-ICP-003 | WF-ICP-001 | No route guard; Hplc.Operate for writes | §25 | TBD | PARTIAL | GAP-026, GAP-035, GAP-043 |
| MOD-SOLN-001 Solution Preparation and titrant standardization | Partial | SCR-SOLN-001, SCR-SOLN-002, SCR-SOLN-003 | WF-SOLN-001, WF-SOLN-002 | Solutions.Prepare (default: System Administrator, Section Head, Analyst) | §26 Solution and Standard Preparation | TBD | PARTIAL | GAP-043 |
| MOD-WS-001 Working Standards | Partial | SCR-WS-001 | WF-WS-001 | No route guard; create WorkingStandards.Qualify; review Samples.Review; approve Samples.Approve | §26 | TBD | PARTIAL | GAP-035, GAP-043 |
| MOD-TITR-001 Titration tests | Partial | none | WF-PHYSRESULT-002 | Laboratory members; configuration MasterData.Manage | §25 | TBD | PARTIAL | GAP-044, GAP-051 |
| MOD-REPORTS-001 Reports | Partial | SCR-REPORTS-001, SCR-REPORTS-002, SCR-REPORTS-003, SCR-REPORTS-004, SCR-REPORTS-005 | WF-REPORTS-001 | All signed-in users | §32 Reports, KPIs and Trend Analysis | TBD | PARTIAL | GAP-002, GAP-003, GAP-052 |
| MOD-KPI-001 KPI / Performance and Workflow Profiles | Partial | SCR-KPI-001, SCR-KPI-002 | WF-KPI-001, WF-KPI-002 | Kpi.View; others' figures Kpi.ViewTeam; profiles Kpi.ConfigureProfiles | §32 | TBD | PARTIAL | GAP-045, GAP-052 |
| MOD-DC-001 Document Control: library and document lifecycle | Partial | SCR-DC-001, SCR-DC-005, SCR-DC-006 | WF-DC-001, WF-DC-002, WF-DC-003, WF-DC-007 | Fixed role-type rules plus assignments (no route guard) | §36 Document Control | TBD | PARTIAL | GAP-024, GAP-025, GAP-048 |
| MOD-DC-002 Document Control: reading and training | Partial | SCR-DC-002, SCR-DC-003, SCR-DC-004 | WF-DC-004, WF-DC-005 | All signed-in users (UI); server gating REQUIRES CONFIRMATION | §36 | TBD | PARTIAL | GAP-024, GAP-025 |
| MOD-DC-003 Document Control: audit trail | Partial | SCR-DC-007 | none | Role type Reviewer, Section Head, System Administrator | §36 | TBD | PARTIAL | GAP-025 |
| MOD-DC-004 Document Control: configuration | Y | SCR-DC-008 | WF-DC-006 | Documents.ConfigManage (default: System Administrator) | §36 | TBD | COVERED | GAP-025 |
| MOD-AUDIT-002 Login history (backend) | Y (backend only) | none | none | Audit.View | none (no UI) | TBD | NOT-EXPOSED-IN-UI | GAP-030 |
| MOD-SECAUDIT-001 Security audit events (backend) | Y (backend only) | none | none | System.ViewSecurityAudit (NOT-ENFORCED) | none (no UI) | TBD | NOT-EXPOSED-IN-UI | GAP-011, GAP-030 |
| MOD-SIG-002 Signature trail (backend) | Y (backend only) | none | none | Signatures.Manage | none (no UI) | TBD | NOT-EXPOSED-IN-UI | GAP-030 |
| MOD-REPORTS-002 Reporting backfill (backend) | Y (backend only) | none | none | Reporting.Admin | none (no UI) | TBD | NOT-EXPOSED-IN-UI | GAP-030 |
| MOD-KPI-002 Analyst availability and KPI section settings (backend + service) | Partial (service without screen) | none | none | Kpi.View / Kpi.ConfigureProfiles (not verified per endpoint) | none (no UI) | TBD | NOT-EXPOSED-IN-UI (service exists in frontend, no screen caller found) | GAP-030 |
| MOD-DC-005 Document escalations (backend) | Y (backend only) | none | none | Authenticated (service rules not read) | none (no UI) | TBD | NOT-EXPOSED-IN-UI | GAP-030 |
| MOD-RESULT-001 Results list (backend) | Y (backend only) | none | none | Authenticated | none (no UI) | TBD | NOT-EXPOSED-IN-UI | GAP-030 |
| MOD-INSTIMPORT-001 Instrument report reading (OCR and parsers) | Partial (library code, no caller) | none | none | not applicable | §39 Integrations (state that no instrument import is available) | TBD | NOT-EXPOSED-IN-UI | GAP-031 |
| MOD-LEGACY-001 Legacy and retired features | Partial | none | none | not applicable | §40 Troubleshooting (old links and redirects only) | TBD | LEGACY/RETIRED | GAP-054 |

## Totals

- Modules: 66
- COVERED: 18
- PARTIAL: 38
- REQUIRES CONFIRMATION: 1
- NOT-EXPOSED-IN-UI: 8
- LEGACY/RETIRED: 1

## Step B candidates with no module row

These candidates from the prompt's checklist were NOT FOUND, so they have no row and no user procedure will be written. They are recorded in `01_APPLICATION_MAP.md` section 13 and in `DOCUMENTATION_GAPS.md`.

| Candidate | Result | Gap |
|---|---|---|
| Final Release (separate step / global permission) | NOT FOUND | GAP-014 |
| Sterility testing (distinct test) | NOT FOUND | GAP-022 |
| EM annual plan and schedules | NOT FOUND | GAP-036 |
| Water monitoring frequencies and plan | NOT FOUND | GAP-036 |
| Reference strain passage tracking | NOT FOUND | GAP-037 |
| Equipment qualification records | NOT FOUND | GAP-038 |
| Laboratory incident / deviation / CAPA module | NOT FOUND | GAP-046 |
| Separate Titration Methods master | NOT FOUND | GAP-051 |
| SAP / ERP integration | NOT FOUND | GAP-032 |
| Local AI assistant | NOT FOUND | GAP-033 |
| Live instrument data feed | NOT FOUND | GAP-031 |
| Reception and QA roles (seeded) | NOT FOUND | GAP-050 |
