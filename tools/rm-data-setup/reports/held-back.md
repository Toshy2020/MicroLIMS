# Held back by the RM data setup (Plan 6)

Decision 2026-11-04: HPLC and ICP methods that lack required details are not loaded, nor are the tests and spec rows that need them, nor the solutions only they use.
The loader skips every object carrying `"heldBack"` in `catalog.json` / `materials/*.json`. Remove the flag (and fill the fields) when the lab supplies the details, then rerun the loader.

Totals: 12 methods (8 HPLC, 4 ICP), 12 test definitions, 23 spec rows in 7 materials, 15 solutions.

## Per material

### STM-RM-025 Sodium Selenate Anhydrous

| Grade | Held-back row | Test | Method it needs |
|---|---|---|---|
| In-house | Total Selenium | RM-ICP-SE | Selenium by AAS (sodium selenate) |
| In-house | Se as Selenate | RM-ICP-SE | Selenium by AAS (sodium selenate) |
| In-house | Se as Selenite | RM-ICP-SE | Selenium by AAS (sodium selenate) |

The lab must supply:
- **Selenium by AAS (sodium selenate)**:
  - element view (axial/radial): an ICP-OES setting, not stated for flame AAS
  - effectiveDate: STM issue date (loader reads it from the STM header)
  - minCorrelation: not stated (single standard level, text says "calibration graph")
  - conversionFactor: STM gives 78.9/188.94 = 0.4176 applied to the selenate assay; which result is the reported total Se is unclear (gaps A)
  - standardLevelsMgPerL: only one level (100 mg/L) is stated

### STM-RM-051 Sildenafil Citrate

| Grade | Held-back row | Test | Method it needs |
|---|---|---|---|
| BP | Related substances - Impurity A | RM-HPLC-SILD-RS | Sildenafil citrate related substances (BP, gradient HPLC) |
| BP | Related substances - Impurity D | RM-HPLC-SILD-RS | Sildenafil citrate related substances (BP, gradient HPLC) |
| BP | Related substances - Unspecified impurities (each) | RM-HPLC-SILD-RS | Sildenafil citrate related substances (BP, gradient HPLC) |
| BP | Related substances - Total impurities | RM-HPLC-SILD-RS | Sildenafil citrate related substances (BP, gradient HPLC) |
| BP | Assay | RM-HPLC-SILD-AS | Sildenafil citrate assay (BP, gradient HPLC) |

The lab must supply:
- **Sildenafil citrate related substances (BP, gradient HPLC)**:
  - standardInjections: not stated
- **Sildenafil citrate assay (BP, gradient HPLC)**:
  - standardInjections: not stated
  - injection / system suitability for the assay: STM text copies the related-substances list (reference solutions a, b, c not defined in the assay) and the resolution 5.0 criterion is tied to reference (a); which solutions the assay injects is unclear (gaps A)
  - columnTemperatureC, equilibration: not stated (optional fields)

### STM-RM-063 Sodium Propyl Paraben

| Grade | Held-back row | Test | Method it needs |
|---|---|---|---|
| BP | Related substances - Impurity A | RM-HPLC-PPB-RS | Sodium propyl paraben related substances (BP, HPLC) |
| BP | Related substances - Unspecified impurities (each) | RM-HPLC-PPB-RS | Sodium propyl paraben related substances (BP, HPLC) |
| BP | Related substances - Sum of impurities other than A | RM-HPLC-PPB-RS | Sodium propyl paraben related substances (BP, HPLC) |
| BP | Assay (Sodium Propyl Hydroxybenzoate) | RM-HPLC-PPB-AS | Sodium propyl paraben assay (BP, HPLC) |

The lab must supply:
- **Sodium propyl paraben related substances (BP, HPLC)**:
  - standardInjections: not stated
- **Sodium propyl paraben assay (BP, HPLC)**:
  - runTimeMin: not stated for the assay (RS part gives 2.5 x retention of about 4.5 min)
  - system suitability acceptance: "6 replicate injections" stated, no RSD limit

### STM-RM-064 Sodium Methyl Paraben

| Grade | Held-back row | Test | Method it needs |
|---|---|---|---|
| BP | Related substances - Impurity A | RM-HPLC-MPB-RS | Sodium methyl paraben related substances (BP, HPLC) |
| BP | Related substances - Unspecified impurities (each) | RM-HPLC-MPB-RS | Sodium methyl paraben related substances (BP, HPLC) |
| BP | Related substances - Sum of impurities other than A | RM-HPLC-MPB-RS | Sodium methyl paraben related substances (BP, HPLC) |
| BP | Assay | RM-HPLC-MPB-AS | Sodium methyl paraben assay (BP, HPLC) |

The lab must supply:
- **Sodium methyl paraben related substances (BP, HPLC)**:
  - standardInjections: not stated
- **Sodium methyl paraben assay (BP, HPLC)**:
  - runTimeMin: not stated for the assay (RS part gives 5 x retention of about 2.3 min)
  - system suitability acceptance: "6 replicate injections" stated, no RSD limit

### STM-RM-097 Pyridoxine Hydrochloride

| Grade | Held-back row | Test | Method it needs |
|---|---|---|---|
| BP | Related substances: Impurity B | RM-HPLC-PYR-RS | Pyridoxine hydrochloride related substances (BP, HPLC) |
| BP | Related substances: Unspecified impurities (each) | RM-HPLC-PYR-RS | Pyridoxine hydrochloride related substances (BP, HPLC) |
| BP | Related substances: Total | RM-HPLC-PYR-RS | Pyridoxine hydrochloride related substances (BP, HPLC) |
| USP | Assay | RM-HPLC-PYR-AS | Pyridoxine hydrochloride assay (USP, HPLC internal standard) |

The lab must supply:
- **Pyridoxine hydrochloride related substances (BP, HPLC)**:
  - standardInjections: not stated
- **Pyridoxine hydrochloride assay (USP, HPLC internal standard)**:
  - runTimeMin: not stated
  - theoreticalWeightStdMg, theoreticalWeightTestMg: only 0.5 mg/mL is stated, the stock flask volume is not
  - standardInjections: replicates for the 3.0 % RSD are not stated
  - column temperature, particle size: not stated (optional fields)

### STM-RM-150 Sodium Hydroxide

| Grade | Held-back row | Test | Method it needs |
|---|---|---|---|
| USP | Content of Sodium | RM-ICP-NA | Sodium by AAS (sodium hydroxide USP) |
| USP | Potassium | RM-ICP-K | Potassium by AAS (sodium hydroxide USP) |

The lab must supply:
- **Sodium by AAS (sodium hydroxide USP)**:
  - element view (axial/radial): an ICP-OES setting, not stated for flame AAS
  - effectiveDate: STM issue date (loader reads it from the STM header)
  - blank: STM states a diluent blank but no blank limit (blank check left off)
- **Potassium by AAS (sodium hydroxide USP)**:
  - element view (axial/radial): an ICP-OES setting, not stated for flame AAS
  - effectiveDate: STM issue date (loader reads it from the STM header)
  - sampleVolumeMl: final volume of the sample solution not stated
  - mode: NMT % result (0.5 % = 5000 ug/g) is not a ug/g impurity limit on the sheet (gaps A)

### STM-RM-151 Siberian Ginseng Powder Extract 4:1

| Grade | Held-back row | Test | Method it needs |
|---|---|---|---|
| In-house | Lead | RM-ICP-PB | Lead by AAS (Siberian ginseng extract) |

The lab must supply:
- **Lead by AAS (Siberian ginseng extract)**:
  - element view (axial/radial): an ICP-OES setting, not stated for flame AAS
  - effectiveDate: STM issue date (loader reads it from the STM header)
  - minCorrelation: not stated
  - calibrationStandardEntry: two lead entries match (RS-035, RS-026); which one is the STM "Lead Standard Solution 1000 ppm" is not decided

## Solutions not loaded

- **0.1 M Sodium thiosulfate VS** (Titrant; used by STM-RM-060, STM-RM-126): not loaded: storageCondition; standardizationMode; equivalenceMgPerMl not stated in the STM
- **0.05 M Iodine VS** (Titrant; used by STM-RM-060): not loaded: storageCondition not stated in the STM
- **0.5 M Hydrochloric acid VS** (Titrant; used by STM-RM-126): not loaded: storageCondition; equivalenceMgPerMl not stated in the STM
- **0.1 M Hydrochloric acid VS** (Titrant; used by STM-RM-091): not loaded: storageCondition; components; standardization not stated in the STM
- **0.004 M Benzethonium chloride VS** (Titrant; used by STM-RM-091): not loaded: storageCondition; instructions, components, standardization not stated in the STM
- **Sildenafil buffer-based mobile phase A (acetonitrile : buffer 20:80)** (MobilePhase; used by STM-RM-051): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition; components, pH not stated
- **Sildenafil mobile phase B (buffer : methanol : acetonitrile 20:20:60)** (MobilePhase; used by STM-RM-051): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition; components not stated
- **Sildenafil diluent (acetonitrile : mobile phase A 10:90)** (Diluent; used by STM-RM-051): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition; components not stated
- **Paraben mobile phase A (6.8 g/L potassium dihydrogen phosphate)** (MobilePhase; used by STM-RM-063, STM-RM-064): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition not stated
- **Paraben mobile phase B (methanol)** (MobilePhase; used by STM-RM-063, STM-RM-064): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition not stated
- **Paraben diluent (mobile phase 35:65)** (Diluent; used by STM-RM-063, STM-RM-064): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition; components not stated
- **Pyridoxine USP mobile phase (hexanesulfonate, pH 3.0, methanol)** (MobilePhase; used by STM-RM-097): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition; phTolerance not stated
- **Pyridoxine USP diluent (mobile phase)** (Diluent; used by STM-RM-097): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition; components not stated
- **Pyridoxine BP mobile phase (KH2PO4 pH 3.0)** (MobilePhase; used by STM-RM-097): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition; phTolerance; phAdjustingEntry not stated
- **Pyridoxine BP diluent (water)** (Diluent; used by STM-RM-097): not loaded: only used by held-back HPLC methods, and shelfLife, storageCondition not stated

## Test definitions not loaded

- RM-HPLC-SILD-AS (Sildenafil citrate assay (BP, HPLC gradient); STM-RM-051): needs the held-back method "Sildenafil citrate assay (BP, gradient HPLC)".
- RM-HPLC-SILD-RS (Sildenafil citrate related substances (BP, HPLC gradient); STM-RM-051): needs the held-back method "Sildenafil citrate related substances (BP, gradient HPLC)".
- RM-HPLC-PPB-AS (Sodium propyl paraben assay (BP, HPLC); STM-RM-063): needs the held-back method "Sodium propyl paraben assay (BP, HPLC)".
- RM-HPLC-PPB-RS (Sodium propyl paraben related substances (BP, HPLC); STM-RM-063): needs the held-back method "Sodium propyl paraben related substances (BP, HPLC)".
- RM-HPLC-MPB-AS (Sodium methyl paraben assay (BP, HPLC); STM-RM-064): needs the held-back method "Sodium methyl paraben assay (BP, HPLC)".
- RM-HPLC-MPB-RS (Sodium methyl paraben related substances (BP, HPLC); STM-RM-064): needs the held-back method "Sodium methyl paraben related substances (BP, HPLC)".
- RM-HPLC-PYR-AS (Pyridoxine hydrochloride assay (USP, HPLC internal standard); STM-RM-097): needs the held-back method "Pyridoxine hydrochloride assay (USP, HPLC internal standard)".
- RM-HPLC-PYR-RS (Pyridoxine hydrochloride related substances (BP, HPLC); STM-RM-097): needs the held-back method "Pyridoxine hydrochloride related substances (BP, HPLC)".
- RM-ICP-SE (Selenium (AAS as ICP, sodium selenate); STM-RM-025): needs the held-back method "Selenium by AAS (sodium selenate)". Also: specification shape.
- RM-ICP-NA (Sodium content (AAS as ICP, sodium hydroxide USP); STM-RM-150): needs the held-back method "Sodium by AAS (sodium hydroxide USP)". Also: specification shape.
- RM-ICP-K (Potassium (AAS as ICP, sodium hydroxide USP); STM-RM-150): needs the held-back method "Potassium by AAS (sodium hydroxide USP)". Also: specification shape.
- RM-ICP-PB (Lead (AAS as ICP, Siberian ginseng); STM-RM-151): needs the held-back method "Lead by AAS (Siberian ginseng extract)".

## Master entries created now though only held-back objects use them

Name-only entries (no stock), see `materialEntries[]` in `catalog.json` (`loadedObjectNeedsIt: false`): Phenol red solution; Sodium fluoresceinate solution 2 g/L; Dimidium bromide-sulfan blue mixed solution; Potassium bromate (primary standard); Sildenafil citrate CRS; Sildenafil impurity A CRS; Sildenafil for peak identification CRS; Imidazole CRS; Propyl para hydroxybenzoate RS; Sodium propyl para hydroxybenzoate RS; Methyl para hydroxybenzoate RS; Sodium methyl para hydroxybenzoate RS; Ethyl para hydroxybenzoate RS; 4-Hydroxybenzoic acid; Pyridoxine impurity A CRS; 4-Deoxypyridoxine hydrochloride; Sodium lauryl sulfate RS; Riboflavin sodium phosphate CRS; Sodium standard (NaCl) for AAS; Potassium standard (KCl) for AAS.
