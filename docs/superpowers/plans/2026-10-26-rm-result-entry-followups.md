# RM result entry (Plan 2) — follow-ups

## Before deploying to production
- Existing dried/anhydrous titrations (ResultBasis 4/5) are refused until the item's LOD/KF test definitions have a MoistureRole and the sample has that result recorded. Run `E:/MicroLIMS/rls-tmp/neon-precheck-plan2.sql` (read-only) on Neon, set the role on the listed LOD/KF tests in Test Master (reason required), and finish or add a moisture order for the open orders it counts.

## Deferred minors (from task and final reviews)
- Task 1: minor (deferred): existing prod dried/anhydrous titration specs fail on next edit until an LOD/KF test is assigned — Neon precheck query (already in Task 7)
- Task 1: minor (deferred): MoistureRole can be cleared / LOD test unassigned or frozen after a spec relies on it — Task 3 resolver refuses at finalize (covers the anti-goal)
- Task 1: minor (deferred): spec-save check ignores TestDefinition.IsActive
- Task 1: minor (deferred): null=keep on update blocks switching a PerContainer/MoistureRole test to an incompatible type unless the client clears flags (clear refusal message)
- Task 1: minor (deferred): EF config lacks HasDefaultValue for the two new columns (defaults only in migration)
- Task 1: minor (deferred): no test asserts the TitrationChanged audit row on a successful MoistureRole change (refusal path is pinned)
- Task 2: minor (deferred): 500-char check now precedes "observation required" (error precedence only)
- Task 6: minor (deferred): defensive guard in handleReviewConfirm returns silently (unreachable while Sign is disabled); stray blank lines in ApprovalSignaturesCard
- Task 3: minor (deferred): GET role from all specs vs analyst's selected subset; cascade return event doesn't name the triggering moisture order
- Task 4: minor (deferred): no upper bound on retention quantity (numeric(18,6) overflow -> 500); no test that summary DTO returns a stored value; no whitespace/null unit test
- Task 5: minor (deferred): ref mutated inside setRows updaters (idempotent); count input needs blur/Enter before submit validity
- Task 3: minor (deferred): interleaving tests lock via LockMoistureOrderAsync directly, not via the real recorder/return; "must report one numeric result" refusal not asserted; resolver scope ctor param nullable
- Final review (agy seat): agrees M4 (raised to Critical) and I2. Rejected: "retention outside signature" — same SaveChanges as the signature, matching every other review field (no manifest in SignAndLogAsync); "micro-only RM blocked" — retention is required only for the FP section; 403 cross-section — earlier ruling. Deferred: a multi-parameter LOD def refuses (the clear message surfaces via F2); per-container mode ignores the parameter-level observation.
- Final re-review (haiku): F1-F4 done; F3 status filter missed Cancelled -> fixed 1fece9ac (35/35 moisture+return tests). Deferred: F1 test via reflection not serialized JSON; no test that a titration role change isn't double-audited.
