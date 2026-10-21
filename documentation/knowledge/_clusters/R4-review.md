# R4 Domain and GMP review — C4 Monitoring & micro configuration

Scope: EM / Water / After Cleaning configuration (the sample flow is C2, context only), Organisms, Media Configurations, microbiology Equipment configuration, microbiology Test Master. Application not run; all statements are from code and the cluster file. No specification exists for Organisms, Media Configurations, Equipment configuration or the micro Test Master (C4 business-rules evidence note).

## Phase 5 domain checklist

### MOD-EM-001 Environmental Monitoring configuration
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Plans | NOT IMPLEMENTED | MOD-EM-001 | No sampling plan object; only departments, rooms, room tests. |
| Schedules / frequencies | PARTIAL | BR-EM-001 area, SCR-EM-001 | "Testing frequency" is stored text, informational only; no scheduling or reminder uses it. |
| Locations / sampling points | DOCUMENTED | SCR-EM-001, WF-EM-001 | Department > room (grade A to D) > room test. |
| Sampling / testing | DOCUMENTED | WF-EM-001, BR-EM-003/004 | Five test types; unit required for Active air, Compressed air, Drains. Sampling itself is C2. |
| Results / limits | DOCUMENTED | BR-EM-005..007 | Limits free text, unvalidated on server; evaluated at result entry. |
| Status synchronization | DOCUMENTED | BR-EM-006 | Limits read at result entry from the linked configuration; edits before entry apply (GAP-402). |
| Out-of-trend rule (spec) | NOT IMPLEMENTED | BR-EM-011, GAP-211 | Spec rule not in code. |
| Review / approval of configuration | NOT IMPLEMENTED | roles | No second-person review of config changes. |

### MOD-WATER-001 Water configuration
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Plans / sample locations | DOCUMENTED | SCR-WATER-001, WF-WATER-001 | System > location with assigned tests. |
| Frequencies | PARTIAL | BR-WATER-007, GAP-415 | Frequency on location only, informational; spec put it on the system (DISCREPANCY). |
| Limits per count test | DOCUMENTED | BR-WATER-003..005 | No duplicate or unit rule on server (GAP-401). A location prepared before its limit row exists gets no limits. |
| Status synchronization | DOCUMENTED | BR-WATER-005/006 | Batch comparison per location. |
| Spec standing | REQUIRES CONFIRMATION | GAP-423 | Both water specs headed "Design — awaiting approval". |

### MOD-AC-001 After Cleaning configuration
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Machines / parts / tests | DOCUMENTED | SCR-AC-001, BR-AC-001..006 | Delete guards present, including used pathogen tests. |
| Collective pathogen sample + TAMC per part (spec) | NOT IMPLEMENTED | BR-AC-007, GAP-225 | DISCREPANCY spec vs code. |
| Unit override | REQUIRES CONFIRMATION | GAP-403 | Typed unit overrides derived unit. |

### MOD-ORG-001 Organisms master
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Organism record / uniqueness | DOCUMENTED | BR-ORG-001 | Scientific name unique, case-insensitive. |
| Passage / lot / expiry | NOT IMPLEMENTED | n/a | Organism is a name list only; lots are cryovials (C5). |
| Delete guard | PARTIAL | BR-ORG-002, GAP-420 | Test Master step targets not guarded; generic error. |
| Status active/inactive | NOT IMPLEMENTED | statuses | Delete only. |

### MOD-MEDIACFG-001 Media Configurations (configuration side)
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Media configuration | DOCUMENTED | SCR-MEDIACFG-001, WF-MEDIACFG-001 | Product + incubation conditions + one evaluation configuration. |
| Organism selection / inoculum | DOCUMENTED | BR-MEDIA-009..011 | Inoculum stored as text. |
| Acceptance criteria (recovery %) | PARTIAL | BR-MEDIA-008 | Min/max stored; no value defined by the guide. Applied at evaluation (C5). |
| Expected colony description | PARTIAL | BR-MEDIA-009, GAP-405 | Required in dialog only. |
| Sterility as configuration | NOT IMPLEMENTED | n/a | No sterility setting here (C5). |
| Activation / media status | NOT IMPLEMENTED | statuses | No status on configuration; lot status is C5. |
| Enrichment Characteristics type | PARTIAL | BR-MEDIA-015, GAP-407 | In enum/API, not on screen. |
| Controlled change of used conditions | DOCUMENTED | BR-MEDIA-013 | Used condition cannot be edited/deleted. |

### MOD-EQUIP-002 Equipment configuration
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Equipment master | DOCUMENTED | SCR-EQUIP-002, BR-EQUIP-001..004 | Section-scoped list. No delete/retire action (BR-EQUIP-014). |
| Inventory link | PARTIAL | BR-EQUIP-005, GAP-417 | Page only pre-fills the form; API link unused. |
| Calibration | PARTIAL | BR-EQUIP-012; C2 BR-TESTING-013 | Due date is a field (own or inventory's); calibration events are not recorded here. An incubator with an overdue due date is excluded from the eligible list and grouped actions (C2). |
| Qualification | NOT IMPLEMENTED | n/a | No qualification record on this page (inventory side: see R5). |
| Usage restrictions | PARTIAL | C2 BR-TESTING-013, GAP-220 | Only incubator set-point range and calibration due date; autoclave programs not tied to a restriction found in this cluster. Single-path/Stage 2 gaps are C2. |
| Set point change | DOCUMENTED | BR-EQUIP-006/007 | Reason required; history row. Edit Equipment bypasses it (GAP-422). |
| Autoclave programs | DOCUMENTED | BR-EQUIP-008..010 | Comment marked required but not enforced (GAP-410). |

### MOD-TESTMASTER-001 Test Master (Microbiology)
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Test definitions / steps / step media | DOCUMENTED | SCR-TESTMASTER-001, BR-TM-001..020 | Template validator returns all rule violations. |
| Freeze / unfreeze | PARTIAL | BR-TM-003, GAP-411 | No usage check, reason or signature; effect on open orders not traced. |
| Section scope | PARTIAL | GAP-419 | Only test update is scoped. |
| Step media by product vs material | REQUIRES CONFIRMATION | GAP-413, BR-TM-018 | Material-based in this cluster file. |
| Picker-created tests | REQUIRES CONFIRMATION | GAP-416 | Default Observation, no steps. |
| Confirmatory media count | PARTIAL | GAP-424 | Page cannot set it (stored 1). |

## Phase 6 GMP / data-integrity facts

### MOD-EM-001 / MOD-WATER-001 / MOD-AC-001 (monitoring configuration)
- Writes require `MasterData.Manage` (default holders System Administrator, Section Head); reads need only sign-in. [src: C4 roles]
- The system records create, update and delete of departments, rooms, locations, limits, machines and parts in the generic audit log with previous and new values and the acting user id. No reason text is captured. [src: C4 audit-trail; MicroLimsDbContext.cs:L259-L375]
- No electronic signature is required for any of these changes. [src: C4 audit-trail]
- No second-person review or approval of configuration changes; the same user can create, change and delete. [src: C4 roles]
- Limits are free text; the server does not validate format or order. An unparseable non-empty limit is treated as no limit at result time. [src: BR-EM-005, BR-EM-007, GAP-400]
- Limits are copied onto the sample location at result entry; a configuration edit before result entry applies to that sample, an edit afterwards does not change the stored location. [src: BR-EM-006, BR-WATER-005]
- Several deletes (used room test, water location with batch use, EM/water department with samples, organism targeted by a step) are refused by the database with a generic error, not a guarded message. [src: GAP-420]
- Record versions: updates are refused when a stale version is sent; without the version header no check is made. [src: BR-EM-010]

### MOD-ORG-001 Organisms master
- Create, update and delete are in the generic audit only; no reason, no signature. [src: C4 audit-trail]
- Delete is refused while media challenges, cryovials or materials reference the organism. [src: BR-ORG-002]

### MOD-MEDIACFG-001 Media Configurations
- Changing a media product code requires the Section Head role type (not the permission; a System Administrator is refused), a reason and the signer's password. The system records an electronic signature (meaning MasterDataChanged), the audit event MediaProductCodeChanged with reason and old/new code, and logs a failed password attempt. [src: C4 audit-trail; MediaMasterDataController.cs:L52-L56]
- Product create, rename and delete, incubation conditions and evaluation configurations (including recovery limits and challenge organisms) are changed with the generic audit only: no reason, no signature. [src: GAP-406]
- A used incubation condition cannot be edited or deleted; a media product cannot be deleted while configurations, batches or step-media use exist. [src: BR-MEDIA-004, -013]
- A media configuration delete has no usage check. [src: BR-MEDIA-018]
- Received batches keep the code they were received with; later lots use the new code. [src: BR-MEDIA-003]

### MOD-EQUIP-002 Equipment configuration
- Equipment create/update and autoclave program changes: generic audit; programs and set points also keep their own history (previous/new values, comment, user, UTC time). No signature. [src: C4 audit-trail; GAP-408]
- An incubator set point change through "Edit Set Point" requires a reason and writes a history row. Changing the same value through "Edit Equipment" requires no reason and writes no history row. [src: BR-EQUIP-013, GAP-422]
- The autoclave program comment is marked required but a blank comment is accepted. [src: GAP-410]
- Equipment cannot be deleted or retired from this page; no status field. [src: BR-EQUIP-014]
- Calibration due date is a plain date field (own or inventory's). The system excludes an incubator with an overdue due date from eligible-incubator lists and refuses it in grouped actions; the stage 2 transfer start has no range check and the single path does not check lot expiry (C2 GAP-220). [src: C2 BR-TESTING-008, -009, -013]
- Section scope covers equipment list/get/update/set point/programs-create; configured-summary, all-programs, program edit/status/history and link-inventory have no section check. [src: C4 roles]
- The set point dialog text refers to "ALCOA+ compliance" as UI wording; this is not a statement of compliance. [src: GAP-408]

### MOD-TESTMASTER-001 Test Master (Microbiology)
- Test, step and step-medium changes (including freeze and unfreeze) are in the generic audit only; no reason, no signature. A step save records the replaced medium rows as deletes and creates. [src: C4 audit-trail; GAP-412]
- A step used by a test order cannot be deleted (matched by step name and test code); editing a used step is allowed. [src: BR-TM-007]
- Freeze has no usage, section or version check. [src: BR-TM-003, GAP-411, GAP-419]
- Step media hours and temperatures are copied from the chosen incubation condition when the step is saved. [src: BR-TM-010]
- Job separation: none. A single user holding `MasterData.Manage` can create a test, change its steps and freeze it. [src: C4 roles]

## Mock / placeholder / unverified data
None found in this cluster. Screens in C4 are configuration forms with live data. Defaults to note: a new Add Program form prefills "Initial program configuration" as the comment; the unused inventory-link API writes a default incubator set point of 32.5 °C without history. [src: BR-EQUIP-005, SCR-EQUIP-002]

## Guide callouts
1. Water, EM and After Cleaning configuration changes are recorded in the audit trail but need no reason and no signature. (GAP-404)
2. Limits are free text; a mistyped limit is accepted and, if unreadable, is treated as no limit. (GAP-400)
3. Limit edits made after a sample is prepared but before results are entered apply to that sample. (GAP-402)
4. A typed unit replaces the standard unit for passive air, surface, swab and rinse results. (GAP-403)
5. Testing frequency is a label only; it schedules nothing. (SCR-EM-001)
6. Some deletes fail with "An unexpected error occurred." when the record is in use. (GAP-420)
7. Only the Section Head role type can change a media product code (signed); a System Administrator cannot. Other media product changes are unsigned. (GAP-013, GAP-406)
8. Edit Equipment can change an incubator set point with no reason and no history entry; use Edit Set Point. (GAP-422)
9. Set point and autoclave program changes are not signed; the autoclave comment is not enforced. (GAP-408, GAP-410)
10. Freezing a test has no check against open work, and its effect on open orders is unconfirmed. (GAP-411)
11. Test Master changes (tests, steps, media) are not signed and carry no reason. (GAP-412)
12. A test added through "+ Add to Test Master" in a picker becomes an Observation test with no steps. (GAP-416)
13. The page cannot set the confirmatory media count. (GAP-424)
14. The collective pathogen / per-part TAMC design for After Cleaning is not implemented. (GAP-225, BR-AC-007)
15. Equipment cannot be retired or deleted from the page; qualification is not recorded here. (BR-EQUIP-014)
16. Whether step media should be chosen by product instead of stock material is unconfirmed. (GAP-413)

## Corrections made to the cluster file
None. Spot checks (media code route role attribute, Test Master IsActive, equipment page signature and ALCOA text, incubator eligibility calibration condition) matched C4.
