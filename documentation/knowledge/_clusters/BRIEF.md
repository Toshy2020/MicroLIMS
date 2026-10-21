# Phase 3–4 cluster brief (read fully before starting)

Project: end-user guide for MicroLIMS (repo `E:\MicroLIMS\MicroLIMS`, branch `docs/user-guide`).
Governing prompt: `C:\Users\moham\OneDrive\Desktop\user_guide_prompt_v2.md` (read "EVIDENCE LABELS", "SOURCES OF TRUTH", "CRITICAL PRINCIPLE", "PHASE 3", "PHASE 4", "PHASE 5" for your modules, "PHASE 6").

## Safety (absolute)
- Write ONLY your own cluster file under `documentation\knowledge\_clusters\`. Modify nothing else (no app code, config, migrations, other docs).
- No builds, servers, databases, network, or state-changing git. Read-only `git grep` / `git log` is fine. Scratch scripts: `E:\MicroLIMS\rls-tmp` (never C:).
- No real sample/batch/product/person names, credentials or connection strings. Use fake placeholders (e.g. "SMP-0001", "Product A").
- The application has NOT been run: all screen behaviour is from code. Do not claim otherwise.
- Never claim GMP / 21 CFR Part 11 / ALCOA+ compliance or validation. Describe functionality objectively ("the system records X in the audit trail").

## Evidence
- Every fact carries `[src: path:Lnnn]` (or `| symbol | endpoint | UI location`).
- Labels: IMPLEMENTED (verified in code), INTENDED (only from specs: `documents/DesignSpecification/BusinessRules.md`, `documents/DesignSpecification/WorkflowDiagrams.md`, `docs/*.md` specs and `docs/superpowers/specs/*.md` — see `documentation/knowledge/_raw/spec-sources.md` for which spec covers what), REQUIRES CONFIRMATION, DISCREPANCY (spec vs code, or spec vs spec).
- Code establishes IMPLEMENTED only, never INTENDED. No spec for a feature ⇒ its intended workflow is REQUIRES CONFIRMATION.
- Never infer a missing step. Accuracy over completeness.

## Inputs you may use
- `documentation/01_APPLICATION_MAP.md` (module/screen/workflow IDs — reuse these IDs exactly; add new IDs only by continuing the numbering)
- `documentation/COVERAGE_MATRIX.md`, `documentation/DOCUMENTATION_GAPS.md` (GAP-001..055 exist; do not edit that file)
- `documentation/knowledge/_raw/*.md` (Phase 1 inventories; `verified-roles-permissions.md` is the VERIFIED role/permission source and wins over `backend-permissions-roles-auth.md`)
- The source code itself.

## Cluster file format (EXACT headings, in this order — a script merges them)
File: `documentation\knowledge\_clusters\C<n>-<slug>.md`. First line `# C<n> <title>`. Then these level-2 headings, each present even if empty (write "None found." if empty). Inside each, use level-3 headings per module (`### MOD-XXX-NNN <name>`).

```
## modules
## screens
## roles
## workflows
## business-rules
## validations
## statuses
## terminology
## reports
## audit-trail
## traceability
## gaps
```

Content per heading:
- **modules** — purpose, sub-features, status label, lab scope (MICRO / FP, physchem areas fp / rmpm).
- **screens** — per SCR id: UI title, route, who can open (permission + lab membership), sections/tabs, fields (exact labels, required?), buttons (exact labels, condition to show/enable), empty/error states. Fill the field-label work lists in GAP-043 / GAP-044 for your modules.
- **roles** — per module: which permission codes gate which actions (backend enforcement point + UI gate), default roles holding them, role-type rules, lab scoping, segregation-of-duties rules with exact error text.
- **workflows** — filled by the Phase 4 (Sonnet) pass; the Haiku pass writes "Pending Phase 4." here.
- **business-rules** — `BR-<MODULE>-NNN`: rule statement, IMPLEMENTED / INTENDED / DISCREPANCY / REQUIRES CONFIRMATION, evidence. Calculations (formulas, rounding, limits) stated exactly as coded.
- **validations** — server and client validations with EXACT user-facing messages.
- **statuses** — every status the user sees for the module's records: UI label, enum value, meaning, and the transitions (from → to, triggering action, who, signed?).
- **terminology** — UI terms specific to the module, with plain-language meaning.
- **reports** — printouts / exports / report tabs from this module.
- **audit-trail** — which actions write audit entries / signatures, what is recorded (old/new values, reason, meaning).
- **traceability** — what links to what (sample → test → media lot → equipment …) as the UI exposes it.
- **gaps** — new questions only, numbered from your cluster's GAP range (given in your task), same fields as DOCUMENTATION_GAPS.md (question, affected, facts, why it matters, needs confirmation, who could confirm). Reference existing GAP ids instead of duplicating.

## Phase 4 workflow template (Sonnet pass)
For each WF id of the cluster (`### WF-XXX-NNN <name>`):
Start / trigger (who) → Prerequisites → numbered steps, each: user action → system response → validation → status change → Next action → Review → Approval → Final state.
Also list: required vs optional data, permissions per step, blocking conditions (time gates, missing configuration, expired material, SoD), error conditions (exact messages), signature points, and the INTENDED source (or REQUIRES CONFIRMATION) plus any DISCREPANCY.
