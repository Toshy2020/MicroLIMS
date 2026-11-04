# Plan 4 UV spectrophotometry: follow-ups

Decisions for the user:

- **Standard lot tied to its material:** decided and built 2026-11-03 (dd45ffc4). A standard-comparison test names its standard material in Test Master; the entry screen lists only its lots and the server refuses others (browser check passed). Existing standard-comparison tests are refused at signing until the material is set; run `select "Code" from "TestDefinitions" where "WorkflowType" = 15 and "UvCalculationMode" = 0` on Neon before merge.
- **Standard stock not deducted on UV signing.** HPLC suitability does not deduct either, and no STM asks for it.
- **No change reason on UV constants.** Editing A1 % or a volume is audited but needs no reason; titration asks for one.
- **Replicates for RM HPLC tests:** decided 2026-11-03, the analyst chooses (at least one), as plates in a micro count test; a count on the test does not bind.

Known limits:

- Standard comparison with several specifications is refused only at signing.
- The CoA "Test Date" reads the preparation date, so an RM sample with no preparation prints a dash. This applies to all such samples, not only UV.
- `getLocalIsoString` and the physicochemical equipment loader are copied across the result panels.
- Panel numeric hints (wavelength 190-1100, absorbance up to 5) mirror the backend, which remains the authority.
- Equipment type Other is accepted as a UV instrument.
- Section check is skipped for all-access users.
- Test Master labels "Replicate count" and others show a doubled asterisk.

Behaviour change outside UV:

- The configured equipment summary now carries the section, so titration, gravimetric, measurement, qualitative, dissolution and disintegration panels list physicochemical instruments. Their lists were silently empty before.

Before Neon:

- Run `E:/MicroLIMS/rls-tmp/neon-precheck-plan4.sql`, and add a UV instrument if none is configured.
- Soya isoflavone still needs a method.
