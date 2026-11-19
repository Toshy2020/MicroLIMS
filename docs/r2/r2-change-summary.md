# R2 change summary

## Overview

R2 changes how staff use the laboratory application day to day. It adds a My Work page, bench and tablet screens, faster tables and data entry, live notifications, in-app help, simpler configuration wizards and clearer navigation. It also adds an Arabic interface and coded server messages, and fixes issues found during three scripted walkthroughs.

## C1 My Work

- A My Work page brings together tests to perform, review and approve. It draws from analyst work, reviews and approvals, documents and equipment, and shows incubations ending in the next 24 hours. (465dd38e, 1e448183, 9877897b, c01f7668, 5e2ea343)
- A landing page and a sidebar badge show the work. A deep link opens each item's workspace, and the dashboard was cleaned up. (beffda09, 2482cb72, cd7d1c16)
- Users can take and release unassigned tests. Releasing a test requires the caller's own take. (6c60247c, 24bed3f4)
- SoD locks review and approval, so a tester cannot review their own work. Reassignment is blocked once a test has a result, and testers cannot return a test. (9877897b, 5c47d10d, 127f5ede)
- Locked items stay out of the urgency tiles, the shown count says how many are locked, and lock reasons use the decision's own refusal sentence. (dec8bc0f, 5be9e7bf)
- Requirements, a guide and roadmap links were documented. (833230c5)

## C2 bench

- A device-level bench mode has its own theme variant. On touch devices it is offered once. (4dd781ef, dc7ae41a, 24a24774)
- Bench screens handle touch activity, switching user and wake lock. The bench can be installed as an app shell. (f9d5e67b, 54112f4f)
- Numeric entry, a keypad and an action bar support recording on tablets. The keypad now shows only while a numeric field has focus. (aa5ffe13, 4159dcad)
- Local drafts survive a lost connection, with connection status shown. A draft is restored after reconnecting and can then be saved. (2c754ada)
- Devices record the draft time as audit source context. (a949340e)
- Tablet screens cover count reading and incubation, EM/water/pathogen location grids, pathogen observations, and media and solution preparation. (569ebfd1, f6d12ce5, c09c5bc1, f7460549)
- Bench targets were raised to at least 48 px. (56ac6dd2)

## C3 tables

- Tables keep sort, filter and column state in the URL. A column menu and a density control were added. (fc020c80)
- Users can save and share views, with a saved views API and UI. (d4b5b5a3, 2e622b80)
- Users can select rows and export CSV with an audit record. Exports carry what the screen shows and work for a user with Data.Export. (5d51a5d3, 6406fc3f, b34f8024)
- Analysts can be assigned in bulk. The operation skips tests already on the chosen analyst. (7c991a74, 3c5e9e8f)
- Large tables use virtual scrolling and expandable rows. The workspace and document library sort on the server. (9fbe2f90, 0df2d788)
- The sample register, workspaces, reports and other registers moved onto the shared RegisterTable. The old DataTable was removed. (6926da29, e79798d9, 82b6095c, 936e61b2)
- Row menu buttons are named after their record. Received, Voided and Cancelled statuses now show in the register. (2af736cc, c032bc6b)

## C4 data entry

- Field-level errors from the API show which field is wrong. (222d3480)
- Receive Sample saves all rows or none, refuses duplicate batch and control numbers, and reports cell-level errors. Keyboard entry works in the grid. (f1351acc, 2a0e59f3, 6ead385d, d98817a3)
- Grid navigation works from the keyboard across the result grids. The last choice is remembered for later entries. (a96ddbc2, 7be45e2e, b8721472, d37bbd51)
- Paste with preview works for receiving, EM and water. A location paste warns how many entered readings it will replace. (98e134e7, bbff058f)
- Count entry has a server-side preview, and one calculator is used for both save and preview. (1e3b68db, ae30bddd)
- Plate readings accept digits only. TNTC with Uncountable is refused, and single add and edit follow the same duplicate rules as paste. (d96b902e, eac678ce)
- Paste and batch creation were added for item specifications, materials, solutions, equipment, water points and EM rooms. (781e2bf0, 97e0dfaa, b8a1fec1, 8e619059, 92e0a924, efd05b39, e5e2c401, b565915a, adde03a6)
- Requirements, a guide, conflicts and a decision register were documented. (229783dc)

## C5 live updates

- A notification catalogue, event-driven publishing, scheduled generation and an email outbox were added. (0c06e2dc, 55d9d481, c561e4d5)
- Users get a notification centre page, preferences and an email delivery card. (1e0da60a, 35c2e473, 3aae64b2, 6e986da7)
- Data-free signals are sent over SignalR after a commit. Refresh is signal-driven, with polling as a fallback. (85a9e40e, a731d97c)
- A record-changed banner appears when a record changes, including in configuration dialogs. It keeps unsaved input and asks before reloading. (42f0886b, 60a67555)
- The live hub connects with the sign-in token instead of browser credentials. (e6ad0cdc)
- Section escalation rules and a working-hours calendar and calculator were added. (9a5913ed, e6d64fb3, e906c2d8)
- Bell list times show a time zone, the My Work badge refreshes on the live signal, and the escalation text uses the singular for one hour. (ddebb75d, e98213f3, 2ab62ea6)
- Requirements and SOP notes were documented. (f5f54392)

## C6 help

- A help menu, an interface-state store and a version were added. Refusals from the server surface in the state store, and loaded values are kept on fallback. (dfd2530e, a61fe2dc)
- A help panel and guide page have a content pipeline and route map. Escape closes the panel only from inside it, and guide tables render correctly. (4a4446e3, 048cd804, f0a61676, d5e20875)
- Shared field hints appear on the first forms, with helper text described and wording verified. (0bab72c0, 4fcbc74a)
- "Items using a test" and an Open SOP action are available where a test is used. (abcc6e1a)
- A first-login tour guides new users. (aa05d3e9)
- Release notes appear once per version. (37cfbfb3)
- Guide fixes: a My Work chapter, placeholder text, hidden traceability markers, and receiving hints that keep their shared English export. (19e7b440, fbd94fa5, f9166a57)
- Requirements, guide chapters, SOP notes and thin help chapters were documented. (6200955d)

## C7 configuration

- A shared wizard frame is used for configuration. (1c626014)
- A where-used service and panel show where a specification or item is used. (0a6dd322)
- Changing a specification asks for a reason when samples are open. Specifications can be retired. (158e0ae8)
- Specifications and items can be copied, and a new item made from an existing one. (945d495b, 40216dfa)
- A test can be created with its stages and steps in one call, with an analyst preview in the test master wizard. (3e5be90b, d1e2a3bf)
- The specification, test master, equipment and add-material wizards were rebuilt with captured form payloads. (94d442ff, fd824989, 78c96e28, ec10636c, a47f196a, 77262b4a)
- Section Heads can switch self-assignment per section and have a card for taking tests. Session reset keeps results and marks them invalidated. (6956e616, a7939b96, 5d2fcb70)
- The decision that no-specification results stay refused, and the configuration requirements, were documented. (e121c84a, f5f54392)

## C8 navigation and reasons

- Users can keep favourites and see recently opened records in the sidebar, with Ctrl+K search. (955b5d4a, d58380d3, 0963d7f3)
- The last laboratory and tab are remembered. The Equipment page remembers its tab. (2af3aa6b, 2883c5db)
- Deep pages show breadcrumbs. (43c28863)
- Gated actions use the server's own availability and show visible reasons. Reasons appear in the interface language. (0d5e4a38, 95336898, 3d3169f5)
- Favourites and Recent sit after the whole OVERVIEW group. A malformed recents answer hides the section. (4c2af14e, 765096e5)
- Requirements for favourites, recents, remembered state, crumbs and action availability, and the built gated actions, were documented. (f5c9641c, 450e9639)

## F4 Arabic interface and coded server messages

- A language provider, an RTL theme, a glossary and a ratchet were added. Users choose the interface language from the account menu. (35408c84, ae73bee6, de2591c4)
- Every English text ships with the app, so no screen shows a raw key. A coded refusal keeps its English sentence when the templates cannot load. (f55d14a4, 85fcb56d)
- Coded server messages cover sample lifecycle, the test workflow and incubation, pathogens, OOS documents, receiving, monitoring, media, cryovials, solutions and instruments. (4e8ce713, cddfd8af, ec4e1ee6, 843f1a4e, 4d7e4ead, d2521563, edc4c520, b5f9866b, 7113decf, 3d481cf0, f50c6e86, aaf849be, 9d2423a9, 6ae3ac76)
- Coded server messages also cover physicochemical tests: titration, UV, dissolution, disintegration, weight variation, assays and HPLC working standards. (0b1484dd, 4b5ec19d, 3f9cd4ad, fc94c9f7, c0407a5e)
- Interface screens were translated into Arabic, including My Work, receiving, the testing workspace, monitoring, configuration components, admin, document control, reports and inventory. (bf5d1ba0, 69418370, 6ee0dd9e, 9677e791, a3fbf0f6, 87b6730c, 04d25a27, ec8f6820, 98dc7598)
- Arabic dates are wrapped in a right-to-left isolate. Numbers use Latin digits in every language. Arabic/RTL layout overflow, clipping and typography were fixed. (a3e6a07a, 45c2e404, d7208f5a, 6a994861)
- In-page printed tables and CSV exports are English and left to right for every user. The Reports page shell and Overview KPI cards are in Arabic. (853de195, 959b6ff2, feb791a0)
- A language change no longer wipes typed replicates or resets an open method form, and the recorded English meaning statement is kept on sign-off. (a69dff54, 3ddf2f78, 3d4f185c)
- Terms were reviewed and applied. The manual, the Arabic walk triage and the interface-language requirements were documented. (4fd38c15, a7611741, 5275be09, 6ce9c0ab)

## Walkthrough findings fixed

- `docs/r2/walkthrough-2026-11-17-part1.md` — part 1 walkthrough, with findings and triage (24d0e34d).
- `docs/r2/walkthrough-2026-11-18-part2.md` — part 2 walkthrough, with findings and triage (0c64360c).
- `docs/r2/arabic-walk-2026-11-19.md` — Arabic screenshot walk, with findings and triage (5275be09).
