# HPLC Chain — Real Test Data (USP-based seed for LIMSV2)

Purpose: realistic master data in the local LIMSV2 database before the S8 end-to-end test.
Section: FP (Physicochemical Laboratory, `DocumentSections.Id = 2`). Instrument: Agilent HPLC (`Equipment.Id = 10`).
Source: USP–NF monographs and USP <Reagents / Indicators / Volumetric Solutions>. Parameters were taken from USP
monograph text available to us (see "Sources" at the end); **the lab must check them against its current official USP–NF
before GMP use** — this is test data.

## 1. Material master (Reagents, Indicators, Reference Standards)

Codes: `RG-nnn` reagent, `IN-nnn` indicator, `RS-nnn` reference standard.

### Reagents (Category Reagent)
| Code | Name | Grade | Base unit |
|---|---|---|---|
| RG-001 | Acetonitrile | HPLC grade | Liter |
| RG-002 | Methanol | HPLC grade | Liter |
| RG-003 | Water, HPLC | HPLC grade (Milli-Q) | Liter |
| RG-004 | Monobasic potassium phosphate (KH2PO4) | ACS reagent | Gram |
| RG-005 | Phosphoric acid 85% | ACS reagent | Liter |
| RG-006 | Glacial acetic acid | ACS reagent | Liter |
| RG-007 | Potassium hydroxide (45% w/v solution) | ACS reagent | Liter |
| RG-008 | Chloroacetic acid | ACS reagent | Gram |
| RG-009 | Ammonium hydroxide 28% | ACS reagent | Liter |
| RG-010 | Monobasic sodium phosphate (NaH2PO4·H2O) | ACS reagent | Gram |
| RG-011 | Sodium hydroxide pellets | ACS reagent | Gram |
| RG-012 | Hydrochloric acid 37% | ACS reagent | Liter |
| RG-013 | Perchloric acid 70% | ACS reagent | Liter |
| RG-014 | Acetic anhydride | ACS reagent | Liter |
| RG-015 | Edetate disodium (EDTA-Na2·2H2O) | ACS reagent | Gram |
| RG-016 | Sodium thiosulfate pentahydrate | ACS reagent | Gram |
| RG-017 | Silver nitrate | ACS reagent | Gram |
| RG-018 | Iodine | ACS reagent | Gram |
| RG-019 | Potassium iodide | ACS reagent | Gram |
| RG-020 | Potassium permanganate | ACS reagent | Gram |
| RG-021 | Sodium carbonate, anhydrous | ACS reagent | Gram |
| RG-022 | Potassium biphthalate (KHP) | Primary standard | Gram |
| RG-023 | Tromethamine | Primary standard | Gram |
| RG-024 | Calcium carbonate (chelometric standard) | Primary standard | Gram |
| RG-025 | Potassium dichromate | Primary standard | Gram |
| RG-026 | Sodium chloride | Primary standard | Gram |
| RG-027 | Sodium oxalate | Primary standard | Gram |
| RG-028 | Sodium 1-heptanesulfonate | HPLC grade | Gram |
| RG-029 | Sodium hydroxide 10 N (pH adjustment) | ACS reagent | Liter |

### Indicators (Category Indicator)
| Code | Name | Working concentration | Solvent | Transition | Colour change | Use |
|---|---|---|---|---|---|---|
| IN-001 | Phenolphthalein TS | 1 g in 100 mL | Alcohol | pH 8.2–10.0 | Colourless → pink | Acid–base (NaOH VS) |
| IN-002 | Methyl orange TS | 0.1 g in 100 mL | Water | pH 3.1–4.4 | Red → yellow | Acid–base |
| IN-003 | Bromocresol green TS | 0.05 g in 100 mL | Alcohol | pH 3.8–5.4 | Yellow → blue | HCl VS vs tromethamine |
| IN-004 | Crystal violet TS | 0.5 g in 100 mL | Glacial acetic acid | — | Violet → blue-green | Non-aqueous (HClO4 VS) |
| IN-005 | Starch TS | 1 g in 100 mL | Water | — | Blue → colourless | Iodometry |
| IN-006 | Hydroxy naphthol blue | Triturate | — | pH ~12–13 | Red → blue | Complexometry (EDTA VS) |
| IN-007 | Eosin Y TS | 50 mg in 10 mL | Water | — | Pink on precipitate | Argentometry (AgNO3 VS) |
| IN-008 | Potassium chromate TS | 1 g in 20 mL | Water | — | Yellow → red-brown | Argentometry (Mohr) |
| IN-009 | Eriochrome black T | Triturate 1 in 100 NaCl | — | pH ~10 | Wine red → blue | Complexometry |

### Reference standards (Category ReferenceStandard)
| Code | Name | Source |
|---|---|---|
| RS-001 | Acetaminophen RS | USP |
| RS-002 | Caffeine RS | USP |
| RS-003 | Amoxicillin RS | USP |
| RS-004 | Diclofenac Sodium RS | USP |
| RS-005 | Ibuprofen RS | USP |
| RS-006 | Metformin Hydrochloride RS | USP |
| RS-007 | Diclofenac Related Compound A RS | USP (SST) |
| RS-008 | Valerophenone | USP reagent (SST / internal std) |
| RS-009 | Ascorbic Acid RS | USP |
| RS-010 | Folic Acid RS | USP |
| RS-011 | Cyanocobalamin RS | USP |
| RS-012 | Cholecalciferol RS | USP |
| RS-013 | Thiamine Hydrochloride RS | USP |
| RS-014 | Riboflavin RS | USP |
| RS-015 | Niacinamide RS | USP |
| RS-016 | Pyridoxine Hydrochloride RS | USP |

## 2. Stock lots (Materials) — one lot per master entry
- Every reagent/indicator: one lot, manufacturer Merck/Sigma-Aldrich/Fisher, received 2026-09-01, expiry 2028-08-31 (indicators 2027-08-31), location "FP chemical store".
- Reference standard lots with **Purity** and **MoisturePercent** (MC on the lot):
  RS-001 Acetaminophen lot R10360 P 99.8, MC 0.1 · RS-002 Caffeine R10340 P 99.9, MC 0.2 · RS-003 Amoxicillin R11020 P 86.9 (as is, anhydrous basis via MC) MC 13.2 · RS-004 Diclofenac Na R08790 P 99.6, MC 0.3 · RS-005 Ibuprofen R09860 P 99.7, MC 0.1 · RS-006 Metformin HCl R10610 P 99.9, MC 0.1 · RS-007 R05920 P 98.5, MC 0.5 · RS-008 R02100 P 99.0, MC 0.0.
  Quantity 200 mg (Gram 0.2), expiry 2027-12-31, location "RS fridge 2–8 °C".

## 3. Chromatography columns (USP L-designations), all compatible with Equipment 10
Existing: COL-C18-01 Hypersil BDS C18 5 µm 4.6×250 mm (L1).
| Code | Name | USP |
|---|---|---|
| COL-L1-02 | Zorbax Eclipse XDB-C18 5 µm 4.6×150 mm | L1 |
| COL-L1-03 | Inertsil ODS-3 5 µm 4.6×250 mm | L1 |
| COL-L1-04 | Waters Symmetry C18 5 µm 3.9×300 mm | L1 |
| COL-L1-05 | Phenomenex Luna C18(2) 5 µm 4.6×250 mm | L1 |
| COL-L7-01 | Zorbax Eclipse XDB-C8 5 µm 4.6×150 mm | L7 |
| COL-L7-02 | Inertsil C8-3 5 µm 4.6×250 mm | L7 |
| COL-L9-01 | Partisil SCX 10 µm 4.6×250 mm | L9 |
| COL-L10-01 | Zorbax SB-CN 5 µm 4.6×250 mm | L10 |
| COL-L11-01 | Phenomenex Luna Phenyl-Hexyl 5 µm 4.6×250 mm | L11 |
| COL-L3-01 | Zorbax Rx-SIL 5 µm 4.6×250 mm | L3 |
| COL-L1-06 | Zorbax Eclipse XDB-C18 5 µm 4.6×100 mm | L1 (new — APAP/caffeine, USP 4.6×100 mm) |
| COL-L39-01 | Polymeric L39 column 5 µm 6.0×150 mm (supplier model to be chosen, particle assumed) | L39 (new — ascorbic acid) |
Name keeps the L-designation in the text: e.g. "Zorbax Eclipse XDB-C18 5 µm 4.6×150 mm (USP L1)".

## 4. Solution master
### Titrants (Type Titrant, prefix VS; rules per USP <Volumetric Solutions>; replicates 3, factor 0.970–1.030, max RSD 0.2 %, validity 30 days, blank per USP)
| Name | Strength | Mode | Standard / reference | Equivalence (mg per mL of VS) | Blank | Components (per 1000 mL) |
|---|---|---|---|---|---|---|
| 0.1 N Sodium hydroxide VS | 0.1 N | Primary standard | RG-022 KHP (dried 120 °C 2 h) | 20.42 | yes | NaOH 4.0 g, CO2-free water to 1000 mL |
| 0.1 N Hydrochloric acid VS | 0.1 N | Primary standard | RG-023 Tromethamine | 12.114 | no | HCl 37% 8.5 mL, water to 1000 mL |
| 0.1 N Perchloric acid VS (in glacial acetic acid) | 0.1 N | Primary standard | RG-022 KHP | 20.42 | yes | HClO4 70% 8.5 mL, glacial acetic acid 500 mL, acetic anhydride 21 mL, to 1000 mL with glacial acetic acid |
| 0.05 M Edetate disodium VS | 0.05 M | Primary standard | RG-024 Calcium carbonate | 5.004 | no | EDTA-Na2 18.6 g, water to 1000 mL |
| 0.1 N Sodium thiosulfate VS | 0.1 N | Primary standard | RG-025 Potassium dichromate | 4.903 | yes | Na2S2O3·5H2O 26 g, Na2CO3 0.2 g, water to 1000 mL |
| 0.1 N Silver nitrate VS | 0.1 N | Primary standard | RG-026 Sodium chloride | 5.844 | no | AgNO3 17.5 g, water to 1000 mL |
| 0.1 N Potassium permanganate VS | 0.1 N | Primary standard | RG-027 Sodium oxalate | 6.700 | no | KMnO4 3.3 g, water to 1000 mL |
| 0.1 N Iodine VS | 0.1 N | Against VS | 0.1 N Sodium thiosulfate VS | — | no | Iodine 14 g, KI 36 g, HCl 3 drops, water to 1000 mL |
Shelf life: 30 days except KMnO4 and iodine 14 days (light-sensitive, amber glass). Storage: "Tightly closed, room temperature, protected from light" (thiosulfate/iodine/AgNO3/KMnO4 amber).

### Mobile phases and diluents: see §5 per method.

## 5. HPLC methods (10) — USP-monograph based
5 drug-product assays + 5 vitamin assays (vitamin C, folic acid, cyanocobalamin B12, cholecalciferol D3, B-complex B1/B2/B3/B6 multi-analyte).
Legend: values without a tag are quoted from the USP text (source in §7). **(A)** = assumed by us (not in USP text). **(E)** = estimated (run time ≈ 2.5 × main-peak RT, RT itself unknown).
Not in any USP text, so always assumed: column particle size where USP gives none (5 µm), column temperature where none (25 °C), standard-prep flask volumes where USP gives only a concentration, typical tablet weights, run times.
Findings that change the plan: **no USP method is gradient** (B-complex is isocratic); **ascorbic acid Tablets are titrimetric (DCPIP)**, the only USP HPLC ascorbic assay is under Ascorbic Acid Injection/Oral Solution, used here with the same conditions on a tablet sample (A); **cyanocobalamin has no HPLC in its own monograph** (UV 361 nm) — the HPLC is Water-Soluble Vitamins Tablets Method 1; USP **Acetaminophen and Caffeine Tablets (internal standard benzoic acid)** and **Ibuprofen Tablets (internal standard valerophenone)** both use an internal standard — recorded as USP states, but our system models external standard only (see notes).
Channel convention: isocratic premix = single channel A 100 %; the premix recipe is the named solution. "Std n" = standard injections.

### 5.1 Acetaminophen Tablets 500 mg — abbreviation `APAP`
Source: USP 35 Acetaminophen Tablets → Assay under Acetaminophen Capsules (p. 2031/2033).
| Field | Value |
|---|---|
| Name | Acetaminophen Tablets Assay (HPLC) |
| Column | USP L1, 300 mm × 3.9 mm, 5 µm (A); use COL-L1-04 (3.9×300) |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, 1.5 mL/min |
| Detector | UV 243 nm (acetaminophen) |
| Injection / run time | about 10 µL / 15 min (E; RT unknown) |
| Channel A 100 % | "APAP mobile phase": water–methanol 3:1 — 750 mL water + 250 mL methanol per 1000 mL, degassed; no pH |
| Diluent | Mobile phase |
| Analyte: Acetaminophen | Std RS-001 Acetaminophen RS; Th.Wt.std 100 mg (A: USP gives only ≈ 0.01 mg/mL; 100 mg/200 mL then 5.0→250 mL matches the sample dilution). Th.Wt.test 120 mg powder = 100 mg acetaminophen (200 mL, then 5.0→250 mL, filter 0.5 µm, discard 10 mL; assumed tablet weight 600 mg (A)) |
| Std n | 5 (RSD NMT 2.0 %) |
| SST | plates NLT 1000; tailing NMT 2; RSD NMT 2.0 % (no resolution/k' in USP) |
| Limits | 90.0–110.0 % of label |

### 5.2 Acetaminophen and Caffeine Tablets (500 mg / 65 mg) — `APAP-CAF`
Source: USP 35 Acetaminophen and Caffeine Tablets Assay (p. 2036–2037). **Internal standard method** — benzoic acid (RG-035, see §5.11). Our system models external standard: record the IS solution in the diluent/solution master and treat the ratio calculation as a known gap (or test with IS peak ignored).
| Field | Value |
|---|---|
| Name | Acetaminophen and Caffeine Tablets Assay (HPLC) |
| Column | USP L1, 100 mm × 4.6 mm, 5 µm; new COL-L1-06 |
| Column temp | 45 ± 1 °C |
| Elution / flow | Isocratic, about 2 mL/min |
| Detector | UV 275 nm (both analytes and internal standard) |
| Injection / run time | about 10 µL / 15 min (E; RRT 0.3 APAP, 0.5 caffeine, 1.0 benzoic acid) |
| Channel A 100 % | "APAP-CAF mobile phase": water–methanol–glacial acetic acid 69:28:3 — 690 mL + 280 mL + 30 mL per 1000 mL |
| Diluent | "Solvent mixture": methanol–glacial acetic acid 95:5 (950 mL + 50 mL). Internal standard solution: benzoic acid in methanol ≈ 6 mg/mL |
| Analyte 1: Acetaminophen | RS-001; Th.Wt.std 25 mg (A: USP 0.25 mg/mL stock, flask assumed 100 mL; then 20.0 mL stock + 3.0 mL IS → 50 mL, 0.1 mg/mL). Th.Wt.test 325 mg powder = 250 mg APAP in 100 mL Solvent mixture (shake 30 min), then 2.0 mL + 3.0 mL IS → 50 mL; assumed tablet 500 mg APAP + 65 mg caffeine, weight 650 mg (A) |
| Analyte 2: Caffeine | RS-002; Th.Wt.std 3.25 mg (A: 0.25·J mg/mL, J = 65/500 = 0.13, 100 mL stock). Th.Wt.test 325 mg powder (same preparation, = 32.5 mg caffeine) |
| Std n | 5 (RSD NMT 2.0 %) |
| SST | tailing NMT 1.2 for each analyte peak; **resolution NLT 1.4 between any analyte and internal-standard peaks**; RSD NMT 2.0 % |
| Limits | 90.0–110.0 % of label, each analyte |

### 5.3 Amoxicillin Capsules 500 mg — `AMOX`
Source: USP 35 Amoxicillin Capsules Assay (p. 2198). Potency (µg/mg) of the RS lot is used in the calculation (RS-003 lot has purity 86.9 / MC).
| Field | Value |
|---|---|
| Name | Amoxicillin Capsules Assay (HPLC) |
| Column | USP L1, 250 mm × 4 mm, **10 µm** (USP); our COL-L1-01/03 (4.6×250, 5 µm) are practical substitutes (A) |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, 1.5 mL/min |
| Detector | UV 230 nm |
| Injection / run time | 10 µL / 20 min (E) |
| Solution "Amox buffer" | 6.8 g monobasic potassium phosphate (RG-004) in 1000 mL water; adjust to pH 5.0 ± 0.1 with 45 % KOH (RG-007) |
| Channel A 100 % | "Amox mobile phase": acetonitrile–Buffer 1:24 — 40 mL acetonitrile + 960 mL Amox buffer |
| Diluent | Amox buffer |
| Analyte: Amoxicillin | RS-003; Th.Wt.std 120 mg (A: USP 1.2 mg/mL; flask assumed 100 mL; "use within 6 h"). Th.Wt.test 240 mg contents = 200 mg anhydrous amoxicillin in 200 mL buffer (sonicate; A: fill 600 mg per 500 mg capsule); sample 1.0 mg/mL nominal |
| Std n | 5 (RSD NMT 2.0 %) |
| SST | tailing NMT 2.5; RSD NMT 2.0 % (no plates/resolution/k') |
| Limits | 90.0–120.0 % of label |

### 5.4 Diclofenac Sodium Delayed-Release Tablets 50 mg — `DICLO`
Source: USP 36 Diclofenac Sodium DR Tablets Assay (p. 3221–3222).
| Field | Value |
|---|---|
| Name | Diclofenac Sodium DR Tablets Assay (HPLC) |
| Column | USP L7 (end-capped), 250 mm × 4.6 mm, 5 µm (A); COL-L7-02 |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, 1 mL/min |
| Detector | UV 254 nm |
| Injection / run time | 10 µL / 20 min (E; USP impurity test runs 2.5 × RT) |
| Solution "Diclo solution A" | equal volumes 0.01 M phosphoric acid and 0.01 M monobasic sodium phosphate (0.68 mL 85 % H3PO4 per litre [calc]; 1.38 g NaH2PO4·H2O per litre [calc]); adjust to pH 2.5 ± 0.2 with more of the appropriate component |
| Channel A 100 % | "Diclo mobile phase": methanol–Solution A 7:3 — 700 mL + 300 mL (raising buffer proportion raises resolution) |
| Diluent | methanol–water 7:3 |
| Analyte: Diclofenac sodium | RS-004; Th.Wt.std 75 mg (A: USP 0.75 mg/mL, flask 100 mL assumed). Th.Wt.test 375 mg powder = 75 mg diclofenac Na in 100 mL (A: USP disintegrates whole tablets, 0.75 mg/mL, filter 0.5 µm; assumed tablet weight 250 mg; shake NLT 30 min) |
| SST solution | diethyl phthalate 20 µg/mL + RS-007 Diclofenac Related Compound A 7.5 µg/mL + diclofenac Na RS 0.75 mg/mL in diluent (RRT 0.5 / 0.6 / 1.0) |
| Std n | 5 (RSD NMT 2.0 %) |
| SST | resolution NLT 2.2 (diethyl phthalate / RC A) and NLT 6.5 (RC A / diclofenac); RSD NMT 2.0 % |
| Limits | 90.0–110.0 % of label |

### 5.5 Ibuprofen Tablets 400 mg — `IBU`
Source: USP 35 Ibuprofen Tablets Assay (p. 3472) which refers to the Ibuprofen monograph Assay (p. 3470) for mobile phase, IS and standard. **Internal standard: valerophenone (RS-008)** — external-standard-only system: note as known gap.
| Field | Value |
|---|---|
| Name | Ibuprofen Tablets Assay (HPLC) |
| Column | USP L1, 250 mm × 4.6 mm, 5 µm (A); COL-L1-01 |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, about 2 mL/min |
| Detector | UV 254 nm |
| Injection / run time | about 5 µL / 20 min (E; RRT 0.75 ibuprofen, 1.0 valerophenone in Tablets text) |
| Channel A 100 % | "Ibuprofen mobile phase": dissolve 4.0 g chloroacetic acid (RG-008) in 400 mL water, adjust to pH 3.0 with ammonium hydroxide (RG-009), add 600 mL acetonitrile, filter, degas |
| Diluent | Internal standard solution: valerophenone in mobile phase ≈ 0.35 mg/mL |
| Analyte: Ibuprofen | RS-005; Th.Wt.std 1200 mg (USP ≈ 12 mg/mL in IS solution, 100 mL). Th.Wt.test 1800 mg powder = 1200 mg ibuprofen in 100.0 mL IS solution, shake 10 min, centrifuge (A: tablet weight 600 mg) |
| Std n | 5 (RSD NMT 2.0 %) |
| SST | resolution NLT 2.5 (ibuprofen / valerophenone); tailing NMT 2.5 each peak; RSD NMT 2.0 % |
| Limits | 90.0–110.0 % of label |

### 5.6 Ascorbic acid (vitamin C) Tablets 500 mg — `VIT-C`
Source: USP 35 Ascorbic Acid Injection / Oral Solution HPLC Assay (p. 2243). Ascorbic Acid Tablets monograph itself is titration (DCPIP, p. 2244) — **no USP HPLC for tablets**; tablet sample prepared in mobile phase is an adaptation (A).
| Field | Value |
|---|---|
| Name | Ascorbic Acid Assay (HPLC) |
| Column | USP L39 (polymeric), 150 mm × 6.0 mm, 5 µm (A); new COL-L39-01 |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, 0.6 mL/min |
| Detector | UV 245 nm |
| Injection / run time | about 4 µL / 10 min (E) |
| Channel A 100 % | "Ascorbic mobile phase": 15.6 g dibasic sodium phosphate + 12.2 g monobasic potassium phosphate in 2000 mL water (per 1000 mL: 7.8 g + 6.1 g), pH 2.5 ± 0.05 with phosphoric acid |
| Diluent | Mobile phase (refrigerate, protect from light; use ≤ 3 h after removal) |
| Analyte: Ascorbic acid | RS-009; Th.Wt.std 50 mg in 100 mL (USP 0.5 mg/mL). Th.Wt.test 140 mg powder = 100 mg ascorbic acid in 200 mL (A: dilute to 0.5 mg/mL; tablet weight 700 mg) |
| Std n | 6 (RSD NMT 1.5 %) |
| SST | plates NLT 3500; tailing NMT 1.6; RSD NMT 1.5 % |
| Limits | 90.0–110.0 % of label (Ascorbic Acid Tablets) |

### 5.7 Folic Acid Tablets 5 mg — `FOLIC`
Source: USP 35 Folic Acid Tablets Assay (p. 3276) — external standard. Also in the WSV monograph: Method 2 (L7, 270 nm, 2 mL/min, 50 °C) — not used.
| Field | Value |
|---|---|
| Name | Folic Acid Tablets Assay (HPLC) |
| Column | USP L1, 250 mm × 4.6 mm, 5 µm (A); COL-L1-01 |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, 1 mL/min |
| Detector | UV 254 nm |
| Injection / run time | 25 µL / 25 min (E) |
| Channel A 100 % | "Folic mobile phase": 35.1 g sodium perchlorate (new reagent) + 1.40 g monobasic potassium phosphate (RG-004) in a 1-L flask; add 7.0 mL 1 N KOH and 40 mL methanol; water to 1000 mL; adjust to pH 7.2 with 1 N KOH or phosphoric acid |
| Diluent | aqueous solution of 2 mL ammonium hydroxide (RG-009) and 1 g sodium perchlorate per 100 mL |
| Analyte: Folic acid | RS-010; Th.Wt.std 10 mg in 50 mL (A: USP 0.20 mg/mL, corrected for water). Th.Wt.test 240 mg powder = 10 mg folic acid in 50 mL (A: 5 mg tablet, weight 120 mg; shake gently, filter, discard first portion; NLT 20 powdered tablets; low-actinic glassware) |
| SST solution | 0.2 mg/mL each of Folic Acid RS and Folic Acid Related Compound A RS (calcium formyltetrahydrofolate) in diluent; 1 µm filter |
| Std n | 5 (RSD NMT 2.0 %) |
| SST | resolution NLT 3.6 (RC A / folic acid, SST solution); RSD NMT 2.0 % (standard) |
| Limits | 90.0–115.0 % of label |

### 5.8 Cyanocobalamin (vitamin B12) Tablets — `B12`
Source: USP 32 Water-Soluble Vitamins Tablets → Oil- and Water-Soluble Vitamins with Minerals Tablets, Assay for cyanocobalamin, Method 1 (primary USP text; Cyanocobalamin monograph USP 35 p. 2783 itself is UV 361 nm, no HPLC). Secondary source (unverified) gives methanol–water 7:13, 361 nm, 100 µL for another procedure.
| Field | Value |
|---|---|
| Name | Cyanocobalamin Assay (HPLC) |
| Column | USP L1, 150 mm × 4.6 mm, 5 µm; COL-L1-02 |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, 0.5 mL/min |
| Detector | UV 550 nm (USP). 361 nm is the stronger band and is a lab option (not USP HPLC text) |
| Injection / run time | about 200 µL / 15 min (E) |
| Channel A 100 % | "B12 mobile phase": water–methanol 65:35 — 650 mL + 350 mL, filtered, degassed |
| Diluent | Water (low-actinic glassware) |
| Analyte: Cyanocobalamin | RS-011; Th.Wt.std 1.0 mg in 100 mL stock 10 µg/mL (store dark, discard after 1 week), then 10.0 → 100 mL = 1 µg/mL. Th.Wt.test 50 mg powder = 100 µg cyanocobalamin in 100.0 mL water, extract 2 min, filter (A: 500 µg tablet, weight 250 mg; NLT 30 tablets powdered) |
| Std n | 6 (RSD NMT 3.0 %; USP <621> gives 6 injections above 2.0 %) |
| SST | RSD NMT 3.0 % (no plates/tailing/resolution in USP text) |
| Limits | 90.0–150.0 % of label (USP Water-Soluble Vitamins Tablets) |

### 5.9 Cholecalciferol (vitamin D3) — `VIT-D3`
Source: USP 35 Cholecalciferol (drug substance) Assay (p. 2652), normal phase, plus flow rate from Cholecalciferol Solution Assay (p. 2653). Alternative (WSV Method 1, L8 3 µm 4.6×150, hexane–isopropyl alcohol 99:1, 265 nm, 1 mL/min, 100 µL, resolution NLT 10) needs an L8 column and vitamin A extraction — not used.
| Field | Value |
|---|---|
| Name | Cholecalciferol Assay (HPLC, normal phase) |
| Column | USP L3 (silica), 250 mm × 4.6 mm, 5 µm (A); COL-L3-01 |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, 2 mL/min (A for the drug-substance text, which gives no flow; 2 mL/min is from the Solution assay) |
| Detector | UV 254 nm |
| Injection / run time | 5–10 µL (USP) → 10 µL / 25 min (E; RRT pre-cholecalciferol 0.4, trans 0.5, D3 1.0) |
| Channel A 100 % | "D3 mobile phase": n-amyl alcohol (1-pentanol) in dehydrated hexane 3 in 1000 — 3 mL + 997 mL (dehydrate hexane over activated siliceous earth column, 500 mL per USP) |
| Diluent | Toluene (stock), then mobile phase |
| Analyte: Cholecalciferol | RS-012; Th.Wt.std 6.0 mg in 10 mL toluene (USP 0.6 mg/mL stock), then dilute with mobile phase to 120 µg/mL (1 → 5 mL). Th.Wt.test 6.0 mg cholecalciferol (drug substance), same preparation. Fresh daily, low-actinic glass, no heating |
| SST solution | 250 mg USP Vitamin D Assay System Suitability RS (new RS) in 10 mL toluene–mobile phase 1:1, refluxed 90 °C 45 min |
| Std n | 5 (RSD NMT 2.0 %) |
| SST | resolution NLT 1.0 between trans-cholecalciferol and precholecalciferol; RSD NMT 2.0 % |
| Limits | 97.0–103.0 % (drug substance, dried basis) |

### 5.10 B-complex (thiamine, riboflavin, niacinamide, pyridoxine) Tablets — `VIT-B`
Source: USP 32 Water-Soluble Vitamins Tablets → Method 1 of Oil- and Water-Soluble Vitamins with Minerals Tablets (niacin/niacinamide, pyridoxine HCl, riboflavin, thiamine). **Isocratic, not gradient.** (Secondary-source hint of 270 nm / L7 / 50 °C belongs to the folic acid Method 2, not this.)
| Field | Value |
|---|---|
| Name | B-complex Assay (HPLC) |
| Column | USP L1, 300 mm × 3.9 mm, 5 µm (A); COL-L1-04 (Waters Symmetry C18 3.9×300) |
| Column temp | 25 °C (A) |
| Elution / flow | Isocratic, 1 mL/min |
| Detector | UV 280 nm (all four analytes) |
| Injection / run time | about 10 µL / 35 min (E; RRT 0.3 niacinamide, 0.5 pyridoxine, 0.8 riboflavin, 1.0 thiamine) |
| Channel A 100 % | "B-complex mobile phase": water–methanol–glacial acetic acid 73:27:1 (730 + 270 + 10 mL) containing sodium 1-hexanesulfonate 140 mg per 100 mL (1.4 g per 1000 mL); adjust if needed per SST |
| Diluent | "Diluting solution": water–acetonitrile–glacial acetic acid 94:5:1 (940 + 50 + 10 mL); heat 65–70 °C to dissolve, cool |
| Analyte 1: Niacinamide | RS-015; Th.Wt.std 80 mg per 200 mL (0.4 mg/mL). Th.Wt.test 50 mg powder = 10 mg in 25.0 mL diluting solution (A: tablet 100 mg niacinamide, weight 500 mg) |
| Analyte 2: Pyridoxine HCl | RS-016; Th.Wt.std 20 mg per 200 mL (0.1 mg/mL). Th.Wt.test 2.5 mg per 50 mg powder (A: 25 mg tablet) |
| Analyte 3: Riboflavin | RS-014; Th.Wt.std 20 mg per 200 mL. Th.Wt.test 2.5 mg per 50 mg powder (A: 25 mg tablet) |
| Analyte 4: Thiamine HCl | RS-013; Th.Wt.std 20 mg per 200 mL. Th.Wt.test 2.5 mg per 50 mg powder (A: 25 mg tablet) |
| Std prep note | one mixed standard of all four in 200-mL flask, ≈ 180 mL diluting solution, 65–70 °C 10 min, chill, to volume; use test filtrate within 3 h; low-actinic glassware; thiamine mononitrate factor 327.36/337.27 |
| Std n | 6 (RSD NMT 3.0 %) |
| SST | RSD NMT 3.0 % for replicate injections; RRT as above (no plates/tailing/resolution in USP text) |
| Limits | 90.0–150.0 % of label, each vitamin |

### 5.11 Master data additions required by these methods
| Code | Item | Note |
|---|---|---|
| RG-030 | Sodium perchlorate | folic acid mobile phase / diluent |
| RG-031 | Sodium 1-hexanesulfonate | B-complex mobile phase |
| RG-032 | n-Hexane (HPLC) | cholecalciferol |
| RG-033 | 1-Pentanol (n-amyl alcohol) | cholecalciferol |
| RG-034 | Toluene (HPLC) | cholecalciferol standard/sample |
| RG-035 | Benzoic acid | APAP-CAF internal standard |
| RG-036 | Dibasic sodium phosphate (Na2HPO4) | ascorbic acid mobile phase |
| RG-037 | Diethyl phthalate | diclofenac SST |
| RS-017 | Folic Acid Related Compound A RS | folic SST |
| RS-018 | USP Vitamin D Assay System Suitability RS | D3 SST |
| Columns | COL-L1-06, COL-L39-01 | §3 |
Existing RG-007 (KOH 45 %) is used for amoxicillin buffer; 1 N KOH for folic acid is prepared from it. RG-028 (heptanesulfonate) is not used by any of the 10 methods.

## 6. Items, test masters and specifications
One item per method, FP category, spec rows linked to the method analyte:
- `PercentLabelClaim` row: limits per USP monograph (verified in §5/§7): acetaminophen 90.0–110.0, caffeine 90.0–110.0, amoxicillin capsules **90.0–120.0**, diclofenac DR 90.0–110.0, ibuprofen 90.0–110.0, ascorbic acid tablets 90.0–110.0, folic acid tablets **90.0–115.0**, cyanocobalamin / B-complex (USP Water-Soluble Vitamins Tablets) **90.0–150.0**, cholecalciferol drug substance **97.0–103.0** (Cholecalciferol Solution would be 90.0–120.0). Metformin HCl tablets: old figure 95–105 not confirmed; USP 35 Metformin Tablets assay is UV (232 nm), HPLC only in dissolution / ER-tablet assay, so it is not a fallback for an HPLC assay.
- `MgPerUnit` row with label claim (e.g. 500 mg/tablet) and the same % limits expressed in mg.
Test master per method: WorkflowType HplcMethodAssay, HplcMethodId set, RequiresSystemSuitability true, stage replicates Finished 2 std / 2 sample, Bulk 2/3.

## 7. Sources
Local text extracts (scratchpad `usp/*.txt`) of USP 35 PDFs; all URLs at drugfuture.com. Lab must verify against current official USP–NF.
| Method | Monograph (edition, page) | URL |
|---|---|---|
| APAP | Acetaminophen Capsules Assay, Acetaminophen Tablets (USP 35, 2031, 2033) | https://www.drugfuture.com/Pharmacopoeia/usp35/PDF/ (Acetaminophen pages, local extracts apap_caps / acetaminophen_tabs) |
| APAP-CAF | Acetaminophen and Caffeine Tablets (USP 35, 2036–2037) | same folder, local extract apap_caf |
| AMOX | Amoxicillin Capsules (USP 35, 2198) | same folder, local extract amox_caps |
| DICLO | Diclofenac Sodium Delayed-Release Tablets (USP 36, 3221–3222) | local extract diclofenac_dr |
| IBU | Ibuprofen (3470–3471) + Ibuprofen Tablets (3472) (USP 35) | https://www.drugfuture.com/Pharmacopoeia/usp35/PDF/3470-3471%20Ibuprofen.pdf |
| VIT-C | Ascorbic Acid Injection / Oral Solution (USP 35, 2243); Ascorbic Acid Tablets (2244, titrimetric) | https://www.drugfuture.com/Pharmacopoeia/usp35/PDF/2242-2243%20Ascorbic%20Acid%20Injection.pdf |
| FOLIC | Folic Acid Tablets (USP 35, 3275–3276) | https://www.drugfuture.com/Pharmacopoeia/usp35/PDF/3275-3276%20Folic%20Acid.pdf |
| B12 | Cyanocobalamin (USP 35, 2783; UV only) and USP 32 Oil- and Water-Soluble Vitamins with Minerals Tablets, Method 1 | https://www.drugfuture.com/Pharmacopoeia/usp35/PDF/2783-2784%20Cyanocobalamin.pdf ; https://www.drugfuture.com/Pharmacopoeia/USP32/pub/data/v32270/usp32nf27s0_m88698.html |
| VIT-D3 | Cholecalciferol (USP 35, 2652) and Cholecalciferol Solution (2653) | local extract chole |
| VIT-B | USP 32 Water-Soluble Vitamins Tablets (limits 90–150 %) → Oil- and Water-Soluble Vitamins with Minerals Tablets, Method 1 | https://www.drugfuture.com/Pharmacopoeia/USP32/pub/data/v32270/usp32nf27s0_m88710.html ; ..._m88698.html |
Not directly verified from primary text: nothing in the tables above is taken from secondary web hints; hints differing from primary text (e.g. vitamin B-complex 270 nm/L7, cyanocobalamin 361 nm/methanol–water 7:13) were not used.
