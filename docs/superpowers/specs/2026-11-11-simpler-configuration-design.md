# C7 — Simpler Configuration Screens: Design Specification

**Roadmap package:** C7 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11

## 1. Purpose

Make the four largest configuration screens easier and safer: guided steps with a review before saving, copying from an existing record, seeing where a record is used and what a change will affect, and previewing what the analyst will see.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Changing a specification while samples are open | Warn, require a reason; the new limits apply to open samples (as today) |
| 2 | Delete / freeze | A reason is always required; deletion stays allowed |
| 3 | Copy from existing | Specifications from one item to another; a test with its workflow steps; an item; equipment |
| 4 | Order | Specification, Test Master, Equipment, Add Material |

## 3. Out of scope

- Specification versions with effective dates (each sample tied to the version in force at receipt). This would be its own package; see 9.2.
- An approval step for configuration changes.
- Other configuration screens.
- Any change to what is configured: no field, limit type or workflow option is added or removed.

## 4. Current state (from code, not run)

- `SpecificationParameterDialog.tsx`: 1,908 lines, 39 state variables, one long form covering 11 limit types plus HPLC and ICP blocks.
- `TestMasterPage.tsx`: 1,621 lines, 60 state variables. Workflow steps can be edited only after the test has been created.
- `EquipmentPage.tsx`: 1,609 lines; its save payload is untyped. `AddMaterialDialog.tsx`: 1,262 lines.
- Master records are **updated in place**. Recorded results keep a copy of the limits they were judged against, so history is safe; a result not yet entered is judged against whatever the specification says at that moment.
- **A specification is deleted outright, with no reason and no usage check. A test is frozen with no reason and no check for open samples.**
- No copy feature, no "where used" view, no preview of the analyst's view.
- A test's steps can already be read without a sample (`GET masterdata/test-definitions/{id}/steps`).

## 5. Design

### 5.1 One wizard pattern

A shared wizard frame used by all four screens:

- A step list at the top showing done, current and remaining steps; steps with a problem are marked with text and an icon.
- Each step checks its own fields before Next. Back never loses input.
- Steps that do not apply are left out (for example the HPLC step for a count test).
- The last step is always **Review**: every value grouped by step, with an Edit link back to each step. When editing an existing record the review shows **what changed**, old value beside new.
- Review also asks the server to validate the whole record without saving, so rule problems appear before Save.
- Saving sends the same data to the same endpoints as today.
- Unsaved input is kept as a device draft (from C2).

Steps per screen:

| Screen | Steps |
|---|---|
| Specification | Test and parameter → Method (HPLC / ICP, only when the test needs it) → Limit type and limits → Unit, reference standard, dilution → Review |
| Test Master | Basics → Settings for this test type → Stages and replicates → Workflow steps → Analyst preview → Review |
| Equipment | Identity and section → Settings for this equipment type → Calibration and location → Review |
| Add Material | Material and lot → Receipt and storage → Quantity, dates and documents → Review |

**Test Master change:** a new test and its workflow steps are created together at Save, all or nothing. Today the steps can only be added after the test exists, which leaves a test without steps in between.

Each step is its own small component, which also delivers the file-splitting the roadmap asks for.

### 5.2 Where used

A panel on each master record and inside its wizard's Review step, filled by one new endpoint:

| Record | Shows |
|---|---|
| Test | Items with a specification for it; items where it is auto-assigned; EM rooms, water points and machine parts configured with it; **open** test orders; total historical test orders |
| Specification | Open test orders for that item and test; results recorded against it |
| Item | Specifications; auto-assigned tests; open samples; total samples; documents |
| Equipment | Workflow uses (incubations, analyses, runs) in progress; total historical uses; linked autoclave programs and instruments |
| Material master entry | Stock lots; solution and media configurations that use it |

Counts link to the matching filtered list. "Open" means not yet approved, rejected or cancelled.

### 5.3 Impact and reason

- **Edit:** when the change affects open work (the where-used "open" count is above zero), Review shows "This change affects 7 open samples. Results not yet entered will use the new values." and a **reason is required**. With no open work, no reason is asked.
- **Delete (specification) and freeze (test, item):** the where-used panel is shown first and a **reason is always required**. Deletion remains allowed, as decided.
- The reason is stored in the audit entry of the change.
- Results already recorded are never touched: they keep the limits copied when they were entered.
- The server enforces the reason; the screen cannot skip it.

### 5.4 Copy from existing

Every copy fills a form or a preview. Nothing is saved until the user confirms, and each copied record passes the same validation as a new one.

| Copy | How |
|---|---|
| Specifications, item → item | On the target item: choose a source item, tick the tests to copy, see a preview. Tests the target already has are listed as "already present" and skipped. Save creates all selected rows or none |
| Test with workflow steps | "New from existing" opens the Test Master wizard pre-filled with settings, stages, steps, media and incubation windows. A new code and name are required |
| Item | "New from existing" opens the item form pre-filled, with tick boxes for "copy specifications" and "copy auto-assigned tests". Code, name and SOP number are not copied; documents are never copied |
| Equipment | "New from existing" opens the Equipment wizard pre-filled. Code, serial details, calibration dates and documents are not copied |

The audit entry of each created record notes which record it was copied from.

### 5.5 Analyst preview

A read-only view of the test as the analyst will meet it: the step chain in order, and for each step its media, incubation window and temperature range, what the analyst must enter, and the stages and replicates. In the wizard it is drawn from the unsaved form; on a saved test it uses the stored steps. It is labelled "Preview — configuration only" and records nothing.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Step invalid | Next is refused; the field shows its message; the step is marked |
| Server validation fails at Review | Messages appear on Review with links to the step and field |
| Record changed by someone else while the wizard was open | Save is refused as today; the wizard keeps the input and offers to reload and re-apply |
| Where-used cannot be loaded | Save of a change that needs a reason is blocked with "Usage could not be checked. Retry." |
| Copy source has rows that fail validation on the target | Listed in the preview with the reason; the user unticks or fixes them |
| Reason missing | The server refuses; the screen asks for it |

## 7. Testing

- **Per screen, before any change:** tests that pin today's saved data for representative cases (each of the 11 limit types; each test type; each equipment type; material create and edit). The wizard must send exactly the same data.
- **Backend `[PostgresFact]`:** where-used counts for each record type, including open versus historical and section scope; edit with open work without a reason refused, with a reason saved and audited; edit without open work needs no reason; delete and freeze without a reason refused; recorded results unchanged after a specification edit and after a delete; test plus steps created together and rolled back together; copy specifications all-or-nothing with "already present" skipped; copied-from noted in the audit entry.
- **Frontend (`userEvent`):** step navigation, skipped steps, per-step validation, Review grouping and change view, Edit links, server messages on Review, draft restore, where-used panel, reason prompt, each copy flow, preview from unsaved input.
- **Manual:** configure one new product end to end (item, specifications by copy, a new test from an existing one) and time it against the current screens; keyboard-only pass.

## 8. Delivery stages

1. Wizard frame; where-used endpoint and panel; reason rule on the server.
2. Specification wizard; copy specifications between items.
3. Test Master wizard with combined save; analyst preview; new test from existing.
4. Equipment wizard; new equipment from existing.
5. Add Material wizard.
6. Item copy.
7. Documents: requirements, guide chapter 31 and 32 updates, SOP note on change reasons.

## 9. Open items

1. **Deleting a specification that results refer to.** You chose to keep deletion allowed. The recorded results keep their copied limits, but they lose the link back to the specification row. The screen will say this before the delete. Tell me if you would rather retire than delete in that one case.
2. **Specification versions with effective dates** would let each sample keep the limits in force when it was received, instead of open samples following the latest change. If you want it, I add it to the roadmap as its own package.
3. Wording of the impact message and the minimum length of a reason (proposed: 10 characters).
