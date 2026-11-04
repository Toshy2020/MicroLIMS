# Plan 5 RM supporting records: follow-ups

Built: QC Manager role, not-applicable tests (Section Head + QC Manager signatures), supplier-stated attributes, outsourced tests, CoA supporting rows, `samples.prepare` permission. Browser check passed 2026-11-04 on item E2E-RM-UV (297): sample RM1126007 approved; CoA shows "External lab: External Lab A", "Not applicable (QC form NA-01)" and "20 mesh (stated by supplier)" with the verdict Conform. RM1126006 carries deliberately failing UV values and stays under testing.

Found in the browser check:

- **Reviewer summary hides the outsourcing.** The Sample Summary shows an outsourced LOD like an in-house one: no external lab, report number or report file, and an analysis time of 00:00 (the report date only). The CoA does name the lab.

Accepted limits:

- **Live CoA rows.** A withdrawn not-applicable record or an edited supplier attribute changes the live CoA of an earlier sample. The archived copy is frozen at each decision.
- **Void/record race.** If one user voids the external report while another signs the result, the signed result can cite a voided (still stored) report.
- **Role change away from a moisture role** (LOD to None/Water) is unguarded and blocks dried assays until restored. This is a pre-existing Test Master gap.
- **Document upload saves, then audits** in a separate write (Plan 1 pattern). If the audit write fails, the upload is unaudited.
- **No outsource action in the HPLC, ICP and GC workspaces.** Only test-analysis workflows can be outsourced.
- **`samples.prepare` reaches a signed-in user at the next token refresh**, not at once.
- Minor: CoA rows dedupe by test code; the external-lab row's date is the entry time; the not-applicable list is readable by every signed-in user.

Before Neon:

- Run `E:/MicroLIMS/rls-tmp/neon-precheck-plan5.sql`. Check the roles outside Types 0-3: they lose sample preparation.
- Set the standard material on every UV standard-comparison test after deploy.
- Squash commit `25f120c0` (conflict markers, fixed in `fa7e91d3`) before any push.
- Neon prechecks for Plans 2-4 have not been run either.
