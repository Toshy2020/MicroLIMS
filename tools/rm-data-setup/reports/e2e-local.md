# End-to-end check on local LIMSV2 (Plan 6, Task 7)

Run 2026-11-05 22:34 against http://localhost:5001/api by `tools/rm-data-setup/e2e.py` (E2E test accounts, local only).

One sample per material and grade (batch `RM-E2E-<code>-<grade>`), received for every laboratory that has tests. Ordered = the test orders the receiving created; expected = the grade's loaded spec rows' tests minus approved not-applicable tests (rows held back are not loaded, so those tests are not expected). Micro orders are only checked (spec rows and limits), never run; so a sample with micro orders is not reviewed or approved, and its certificate is read from the sample summary while unapproved.

| Material | Grade | Sample | Ordered | Expected | Ordered = expected | Physchem results recorded | Micro orders | Approved | CoA rows ok |
|---|---|---|---|---|---|---|---|---|---|
| Pyridoxine Hydrochloride | BP | 121 | 15 | 15 | yes | 12 of 12 | 3 | n/a (micro orders not run) | yes |
| Pyridoxine Hydrochloride | USP | 122 | 10 | 10 | yes | 7 of 7 | 3 | n/a (micro orders not run) | yes |
| Sodium selenate anhydrous | In-house | 123 | 9 | 9 | yes | 6 of 6 | 3 | n/a (micro orders not run) | yes |
| Sildenafil citrate | BP | 124 | 6 | 6 | yes | 6 of 6 | 0 | yes | yes |
| Soluble sodium chloride tablets | BP | 125 | 4 | 4 | yes | 4 of 4 | 0 | yes | yes |
| Sodium Metabisulfite | BP | 126 | 9 | 9 | yes | 9 of 9 | 0 | yes | yes |
| Sodium Propyl Paraben | BP | 127 | 14 | 14 | yes | 11 of 11 | 3 | n/a (micro orders not run) | yes |
| Sodium Methyl Paraben | BP | 128 | 14 | 14 | yes | 11 of 11 | 3 | n/a (micro orders not run) | yes |
| Sodium Starch Glycolate Type A | BP | 129 | 14 | 14 | yes | 10 of 10 | 4 | n/a (micro orders not run) | yes |
| Sodium Lauryl Sulfate | BP | 130 | 10 | 10 | yes | 10 of 10 | 0 | yes | yes |
| Sodium Hypochlorite | BP | 131 | 4 | 4 | yes | 4 of 4 | 0 | yes | yes |
| Soya isoflavone extract 10-12% | In-house | 132 | 7 | 7 | yes | 3 of 3 | 4 | n/a (micro orders not run) | yes |
| Sodium hydroxide | BP | 133 | 10 | 10 | yes | 10 of 10 | 0 | yes | yes |
| Sodium hydroxide | USP | 134 | 7 | 7 | yes | 7 of 7 | 0 | yes | yes |
| Siberian Ginseng | In-house | 135 | 9 | 9 | yes | 5 of 5 | 4 | n/a (micro orders not run) | yes |
| Riboflavin Phosphate Sodium | BP | 136 | 11 | 11 | yes | 11 of 11 | 0 | yes | yes |

## Problems

### DATA problems (our catalog or spec rows are wrong)

- none

### SYSTEM problems (the app refuses something valid)

- none

### Workflows that could not be completed here

- none

## Per-sample detail

### STM-RM-097|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-097|USP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-025|In-house
- certificate supporting rows: ['Particle Size: Stated by Supplier (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-051|BP
- certificate: yes (approved sample)

### STM-RM-053|BP
- certificate: yes (approved sample)

### STM-RM-060|BP
- certificate: yes (approved sample)

### STM-RM-063|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-064|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-089|BP
- certificate supporting rows: ['Grade: Type (A) (stated by supplier)', 'Degree of substitution: 0.23 to 0.32 (stated by supplier)', 'Sodium glycolate: Maximum 2.0 per cent (stated by supplier)', 'Sodium chloride: Maximum 7.0 per cent (stated by supplier)', 'Assay: 2.8 per cent to 4.2 per cent of Na (stated by supplier)', 'Particles Size: Stated by supplier (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-091|BP
- certificate: yes (approved sample)

### STM-RM-126|BP
- certificate: yes (approved sample)

### STM-RM-147|In-house
- certificate supporting rows: ['Plant: Glycine max (stated by supplier)', 'Plant part used: Soy germ (stated by supplier)', 'Extraction solvent: Ethanol (stated by supplier)', 'Carrier / preservatives / additives: No carrier used, no preservatives added, No additives (stated by supplier)', 'Lead (Pb): Max. 0.5 ppm (stated by supplier)', 'Mercury (Hg): Max. 0.1 ppm (stated by supplier)', 'Cadmium (Cd): Max. 0.5 ppm (stated by supplier)', 'Arsenic (As): Max. 1 ppm (stated by supplier)', 'Residual ethanol: Max. 5,000 mg/kg (stated by supplier)', 'Pesticides: Conform to current EU legislation (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-150|BP
- certificate: yes (approved sample)

### STM-RM-150|USP
- certificate: yes (approved sample)

### STM-RM-151|In-house
- certificate supporting rows: ['Working standard: Should be supplied with the shipment (stated by supplier)', 'Plant part used: Stem & root (stated by supplier)', 'Extraction solvent: Ethanol + Water (stated by supplier)', 'Extraction ratio: 4:1 (stated by supplier)', 'Carrier used: Maltodextrin (stated by supplier)', 'Particle size: 100% pass through 80 mesh (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-56|BP
- certificate supporting rows: ['Composition (Riboflavin phosphate): Not applicable', 'Related substances (Riboflavin phosphate): Not applicable']
- certificate: yes (approved sample)


## Browser check (Brave, 2026-11-05): passed

- Receive Sample, Raw Material, Pyridoxine Hydrochloride: the grade picker offers exactly BP and USP ("Choose the
  grade stated on the supplier's CoA").
  - USP sample RM1126024 (#137) ordered the USP set (10 tests: chloride content by titration; no BP assay, UV or TLC
    identification), same as the scripted USP sample.
  - BP sample RM1126025 (#138) ordered the BP set (15 tests), same as the scripted BP sample.
- Riboflavin: item Supporting Records shows both not-applicable records Approved, signed by the Section Head then the
  QC Manager; the approved certificate of RM1126023 prints "Composition" and "Related substances" as "Not applicable",
  grade BP, conclusion Conform.

## Production (2026-11-06)

- An API run of `load.py` as a Physicochemical Section Head had loaded most of the setup (test definitions, 127 of the
  physchem/other spec rows, minimums, supplier attributes, Riboflavin's two not-applicable drafts).
- The rest was copied straight into Neon with `copy_to_prod.py` (development phase, user's decision): 2 reagent
  entries, 2 titrants (1 N sulfuric acid VS, 0.1 M ammonium thiocyanate VS) with their components, 2 titration
  tests (RM-TIT-NACLT, RM-TIT-NAOH-USP) and 29 spec rows (the 27 micro rows + those two assays). One transaction;
  Neon backup branch `backup-before-rm-data-2026-11-06` taken first.
- Check: production now has the same 156 spec rows as local, value for value, and the same RM test definitions;
  a second copy run finds nothing to add.
- Open: Riboflavin's two not-applicable drafts need the Section Head's and then a QC Manager's signature in the app;
  production has no user with the QC Manager role yet.
