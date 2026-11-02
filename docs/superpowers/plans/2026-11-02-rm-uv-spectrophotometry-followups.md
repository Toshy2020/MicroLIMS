# Plan 4 UV spectrophotometry: follow-ups

Decisions for the user:

- **Standard lot not tied to the analyte.** A standard-comparison test accepts any usable lot in the section. HPLC ties the lot to the method analyte and titration to a standard entry. No STM uses this mode yet. Decide on a Test Master "expected standard" field before the first such test is configured.
- **Standard stock not deducted on UV signing.** HPLC suitability does not deduct either, and no STM asks for it.
- **No change reason on UV constants.** Editing A1 % or a volume is audited but needs no reason; titration asks for one.
- **Replicates for RM HPLC tests.** Test Master has no such field for HPLC; the analyst enters at least one. Carried from Plan 3 to Plan 6.

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
