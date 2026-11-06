# End-to-end check on local LIMSV2 (Plan 6, Task 7)

Run 2026-11-06 17:19 against http://localhost:5000/api by `tools/rm-data-setup/e2e.py` (E2E test accounts, local only).

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
| Biotin | BP | 141 | 10 | 10 | yes | 7 of 7 | 3 | n/a (micro orders not run) | yes |
| Biotin | USP | 142 | 7 | 7 | yes | 4 of 4 | 3 | n/a (micro orders not run) | yes |
| Zinc Sulphate Heptahydrate | USP | 143 | 5 | 5 | yes | 2 of 2 | 3 | n/a (micro orders not run) | yes |
| Zinc Sulphate Heptahydrate | BP | 144 | 9 | 9 | yes | 6 of 6 | 3 | n/a (micro orders not run) | yes |
| Manganese Sulfate Monohydrate | BP | 145 | 8 | 8 | yes | 5 of 5 | 3 | n/a (micro orders not run) | yes |
| Manganese Sulfate Monohydrate | USP | 146 | 5 | 5 | yes | 2 of 2 | 3 | n/a (micro orders not run) | yes |
| Hypromellose | None | 147 | 6 | 6 | yes | 6 of 6 | 0 | yes | yes |
| Potassium Iodide | None | 148 | 10 | 10 | yes | 7 of 7 | 3 | n/a (micro orders not run) | yes |
| Stearic Acid | BP | 149 | 6 | 6 | yes | 3 of 3 | 3 | n/a (micro orders not run) | yes |
| Colloidal anhydrous silica (aerosol 200) | BP | 150 | 10 | 10 | yes | 7 of 7 | 3 | n/a (micro orders not run) | yes |
| Povidone K30 PVP | None | 151 | 6 | 6 | yes | 6 of 6 | 0 | yes | yes |
| Polysorbate 80 | BP | 152 | 3 | 3 | yes | 3 of 3 | 0 | yes | yes |
| Cyanocobalamin | BP | 153 | 6 | 6 | yes | 3 of 3 | 3 | n/a (micro orders not run) | yes |
| Cyanocobalamin | USP | 154 | 6 | 6 | yes | 3 of 3 | 3 | n/a (micro orders not run) | yes |
| Grape Seed Extract (Proanthocyanidin 95%) | None | 155 | 11 | 11 | yes | 7 of 7 | 4 | n/a (micro orders not run) | yes |
| Calcium Carbonate MD | None | 156 | 13 | 13 | yes | 10 of 10 | 3 | n/a (micro orders not run) | yes |
| Honey | BP | 157 | 12 | 12 | yes | 8 of 8 | 4 | n/a (micro orders not run) | yes |
| Ascorbic acid (raw material) | USP | 158 | 9 | 9 | yes | 7 of 9 | 0 | no (results missing for ['TIT-REL', 'TIT-ASCORBIC']) | yes |
| Ferric ammonium citrate green | None | 159 | 11 | 11 | yes | 8 of 8 | 3 | n/a (micro orders not run) | yes |
| Carmellose Sodium (Carboxymethylcellulose Sodium) | BP | 161 | 11 | 11 | yes | 11 of 11 | 0 | yes | yes |
| Vitamin K1 5% SD | None | 162 | 7 | 7 | yes | 4 of 4 | 3 | n/a (micro orders not run) | yes |
| Crospovidone | USP | 163 | 13 | 13 | yes | 10 of 10 | 3 | n/a (micro orders not run) | yes |
| Copper Sulphate Pentahydrate | BP | 164 | 15 | 15 | yes | 12 of 12 | 3 | n/a (micro orders not run) | yes |
| Calcium Glycerophosphate | BP | 166 | 19 | 19 | yes | 16 of 16 | 3 | n/a (micro orders not run) | yes |
| Levofloxacin | USP | 167 | 10 | 10 | yes | 7 of 7 | 3 | n/a (micro orders not run) | yes |
| Potassium Sorbate | BP | 168 | 14 | 14 | yes | 10 of 11 | 3 | n/a (micro orders not run) | no: 1 spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok |
| Ferric Ammonium Citrate | None | 169 | 10 | 10 | yes | 7 of 7 | 3 | n/a (micro orders not run) | yes |
| Macrogols "Polyethylene glycol" | BP | 170 | 14 | 14 | yes | 11 of 11 | 3 | n/a (micro orders not run) | yes |
| Zinc Gluconate | USP | 171 | 14 | 14 | yes | 10 of 11 | 3 | n/a (micro orders not run) | no: 1 spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok |
| Glimepiride | USP | 172 | 9 | 9 | yes | 6 of 6 | 3 | n/a (micro orders not run) | yes |
| Fluconazole | USP | 173 | 9 | 9 | yes | 6 of 6 | 3 | n/a (micro orders not run) | yes |
| Magnesium Oxide | USP | 174 | 13 | 13 | yes | 8 of 10 | 3 | n/a (micro orders not run) | no: 4 spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok |
| Alpha Tocopheryl Hydrogen Succinate | USP | 175 | 11 | 11 | yes | 8 of 8 | 3 | n/a (micro orders not run) | yes |
| Liquid glucose | BP | 176 | 13 | 13 | yes | 10 of 10 | 3 | n/a (micro orders not run) | yes |
| Liquid sorbitol non crystallizing | BP | 177 | 12 | 12 | yes | 9 of 9 | 3 | n/a (micro orders not run) | yes |
| Zinc sulfate monohydrate | USP | 178 | 9 | 9 | yes | 9 of 9 | 0 | yes | no: spec rows not printed with a conforming result: ["RM-TIT-ZN-USP-067/Assay (zinc sulfate monohydrate, ZnSO4 · H2O) printed as '89.68 %' conform=False"] |
| Zinc sulfate monohydrate | BP | 179 | 9 | 9 | yes | 9 of 9 | 0 | yes | yes |
| Vitamin D3 500 IU/mg | None | 180 | 7 | 7 | yes | 4 of 4 | 3 | n/a (micro orders not run) | yes |
| Opadry white | None | 181 | 7 | 7 | yes | 4 of 4 | 3 | n/a (micro orders not run) | yes |
| Green Empty Hard Gelatin Capsule Size Two | None | 182 | 14 | 14 | yes | 10 of 10 | 4 | n/a (micro orders not run) | yes |
| Calcium Carbonate | USP | 183 | 17 | 17 | yes | 13 of 14 | 3 | n/a (micro orders not run) | no: 1 spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok |
| Mint Flavor (peppermint oil) | None | 184 | 10 | 10 | yes | 6 of 6 | 4 | n/a (micro orders not run) | yes |
| Microcrystalline Cellulose | BP | 185 | 16 | 16 | yes | 10 of 10 | 6 | n/a (micro orders not run) | yes |
| Microcrystalline Cellulose | USP | 186 | 17 | 17 | yes | 10 of 11 | 6 | n/a (micro orders not run) | no: 1 spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok |
| Lactose Monohydrate | BP | 187 | 14 | 14 | yes | 11 of 11 | 3 | n/a (micro orders not run) | yes |
| Croscarmellose Sodium | BP | 188 | 14 | 14 | yes | 11 of 11 | 3 | n/a (micro orders not run) | yes |
| Croscarmellose Sodium | USP | 189 | 14 | 14 | yes | 11 of 11 | 3 | n/a (micro orders not run) | yes |
| Magnesium Stearate | BP | 231 | 14 | 14 | yes | 9 of 10 | 4 | n/a (micro orders not run) | no: spec rows not printed with a conforming result: ['RM-DESC/Appearance printed 0 times', 'RM-SOLUB/Solubility printed 0 times', 'RM-ID-CR-MG/Identification (Chemical Reaction) printed 0 times', 'RM-ID-FP-MG/Identification (Freezing Point) printed 0 times', 'RM-ID-AV-MG/Identification (Acid value) printed 0 times', 'RM-LT-CL/Chlorides printed 0 times', 'RM-LT-SO4/Sulfates printed 0 times', "RM-LOD/Loss on drying printed as '—' conform=False"]; 1 spec rows have no result because their orders were refused: ['RM-TIT-MGST/Assay'] |
| Orange oil 5-fold extract | None | 191 | 10 | 10 | yes | 5 of 6 | 4 | n/a (micro orders not run) | no: 1 spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok |
| Feroglobin B12 granules | None | 192 | 6 | 6 | yes | 3 of 3 | 3 | n/a (micro orders not run) | yes |
| Maize Starch | None | 193 | 10 | 10 | yes | 6 of 6 | 4 | n/a (micro orders not run) | yes |
| Sweet orange oil | None | 194 | 10 | 10 | yes | 6 of 6 | 4 | n/a (micro orders not run) | yes |
| Vitamin A concentrate (Powder form) | BP | 195 | 3 | 3 | yes | 3 of 3 | 0 | yes | yes |
| Natural Beta carotene 20% CWD Powder | None | 196 | 10 | 10 | yes | 6 of 6 | 4 | n/a (micro orders not run) | yes |
| Magnesium Hydroxide | USP | 197 | 12 | 12 | yes | 8 of 9 | 3 | n/a (micro orders not run) | no: 1 spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok |
| Opadry green | None | 198 | 6 | 6 | yes | 3 of 3 | 3 | n/a (micro orders not run) | yes |
| Opadry pink | None | 199 | 7 | 7 | yes | 4 of 4 | 3 | n/a (micro orders not run) | yes |
| Aluminum Magnesium Silicate (Veegum) | BP | 201 | 16 | 16 | yes | 13 of 13 | 3 | n/a (micro orders not run) | yes |
| Calcium Hydrogen Phosphate Dihydrate | None | 202 | 16 | 16 | yes | 11 of 13 | 3 | n/a (micro orders not run) | no: 4 spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok |
| Tartrazine Lake | None | 203 | 9 | 9 | yes | 6 of 6 | 3 | n/a (micro orders not run) | yes |
| Maltodextrin | BP | 204 | 13 | 13 | yes | 9 of 9 | 4 | n/a (micro orders not run) | yes |
| Opadry brown | None | 205 | 7 | 7 | yes | 4 of 4 | 3 | n/a (micro orders not run) | yes |
| Ocobalamin 0.1% in WS | None | 206 | 8 | 8 | yes | 5 of 5 | 3 | n/a (micro orders not run) | yes |
| Opadry red | None | 207 | 7 | 7 | yes | 4 of 4 | 3 | n/a (micro orders not run) | yes |
| Vitamin D3 100 IU-mg Stabilized | None | 208 | 8 | 8 | yes | 5 of 5 | 3 | n/a (micro orders not run) | yes |
| Omega 3 fish oil | None | 209 | 9 | 9 | yes | 5 of 5 | 4 | n/a (micro orders not run) | yes |
| D-Panthenol | USP | 210 | 10 | 10 | yes | 10 of 10 | 0 | yes | yes |
| Marine collagen peptide | None | 211 | 9 | 9 | yes | 5 of 5 | 4 | n/a (micro orders not run) | yes |
| Horsetail Extract 07-10% | None | 212 | 9 | 9 | yes | 5 of 5 | 4 | n/a (micro orders not run) | yes |
| Inositol | None | 213 | 10 | 10 | yes | 7 of 7 | 3 | n/a (micro orders not run) | yes |
| Crystalline vitamin D3 | USP | 214 | 4 | 4 | yes | 4 of 4 | 0 | yes | yes |
| Coenzyme Q10 ( Ubidecarenone) | BP | 216 | 9 | 9 | yes | 6 of 6 | 3 | n/a (micro orders not run) | yes |
| xanthan gum | BP | 217 | 8 | 8 | yes | 4 of 4 | 4 | n/a (micro orders not run) | yes |
| xanthan gum | USP | 218 | 7 | 7 | yes | 3 of 3 | 4 | n/a (micro orders not run) | yes |
| Vitamin A Palmitate 1.7 IU/mg | BP | 225 | 6 | 6 | yes | 6 of 6 | 0 | yes | yes |
| Thiamine Hydrochloride | BP | 226 | 13 | 13 | yes | 10 of 10 | 3 | n/a (micro orders not run) | yes |
| Thiamine Hydrochloride | USP | 227 | 12 | 12 | yes | 9 of 9 | 3 | n/a (micro orders not run) | yes |
| Lemon oil | BP | 228 | 11 | 11 | yes | 7 of 7 | 4 | n/a (micro orders not run) | yes |
| Citric acid Monohydrate | BP | 229 | 15 | 15 | yes | 12 of 12 | 3 | n/a (micro orders not run) | yes |
| Sucrose | BP | 230 | 15 | 15 | yes | 12 of 12 | 3 | n/a (micro orders not run) | yes |

## Problems

### DATA problems (our catalog or spec rows are wrong)

- **STM-RM-086|BP certificate**: spec rows not printed with a conforming result: ['RM-DESC/Appearance printed 0 times', 'RM-SOLUB/Solubility printed 0 times', 'RM-ID-CR-MG/Identification (Chemical Reaction) printed 0 times', 'RM-ID-FP-MG/Identification (Freezing Point) printed 0 times', 'RM-ID-AV-MG/Identification (Acid value) printed 0 times', 'RM-LT-CL/Chlorides printed 0 times', 'RM-LT-SO4/Sulfates printed 0 times', "RM-LOD/Loss on drying printed as '—' conform=False"]

### SYSTEM problems (the app refuses something valid)

- **STM-RM-047|BP RM-UV-KSORB-047**: result refused: Absorbance must be above 0 and at most 5.
- **STM-RM-050|USP RM-TIT-RED-050**: result refused: Replicate 1: The blank volume must be greater than the sample titrant volume.
- **STM-RM-127|None RM-TIT-CAHP-127**: result refused: could not set up titrant for master 51
- **STM-RM-086|BP RM-TIT-MGST**: result refused: could not set up titrant for master 50

### Workflows that could not be completed here

- **RM-ASCORBIC|USP TIT-ASCORBIC**: script could not build the result: list index out of range
- **RM-ASCORBIC|USP TIT-REL**: script could not build the result: list index out of range
- **STM-RM-058|USP RM-BULKD**: script could not build the result: no numeric target for limit type Qualitative
- **STM-RM-058|USP RM-PSD-058**: script could not build the result: no numeric target for limit type Qualitative
- **STM-RM-078|USP RM-PSD-CACO3-078**: script could not build the result: no numeric target for limit type Qualitative
- **STM-RM-081|USP RM-BULKD**: script could not build the result: no numeric target for limit type Qualitative
- **STM-RM-087|None RM-COL-087**: e2e.py has no handler for workflow Observation
- **STM-RM-107|USP RM-BULKD**: script could not build the result: no numeric target for limit type Qualitative
- **STM-RM-127|None RM-PSD-127**: script could not build the result: no numeric target for limit type Qualitative
- **titrant 0.1 M Zinc sulfate VS**: no usable stock lot of Zinc sulfate heptahydrate

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

### C1I-91-151|BP
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-151|USP
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-153|USP
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-153|BP
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-158|BP
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-158|USP
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-169|None
- certificate: yes (approved sample)

### C1I-91-178|None
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-179|BP
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-228|BP
- certificate supporting rows: ['Surface area: 200 m2/g ±25 (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### C1I-91-229|None
- certificate: yes (approved sample)

### C1I-91-231|BP
- certificate: yes (approved sample)

### C5I-91-213|BP
- certificate: yes (unapproved sample, read from the summary)

### C5I-91-213|USP
- certificate: yes (unapproved sample, read from the summary)

### C5I-91-225|None
- certificate supporting rows: ['Extraction solvent: Water & Ethanol (stated by supplier)', 'Plant name: Vitis vinifera (stated by supplier)', 'Plant part used: Seed (stated by supplier)', 'Particle size: Stated by supplier (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### Caco3 MD|None
- certificate: yes (unapproved sample, read from the summary)

### P.Honey|BP
- certificate: yes (unapproved sample, read from the summary)

### RM-ASCORBIC|USP
- TIT-ASCORBIC: script error: list index out of range
- TIT-REL: script error: list index out of range
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-001|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-004|BP
- certificate: yes (approved sample)

### STM-RM-012|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-024|USP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-030|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-036|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-045|USP
- certificate supporting rows: ['Residual Solvents: Comply as Supplier COA (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-047|BP
- RM-UV-KSORB-047: refused: Absorbance must be above 0 and at most 5.
- certificate: no: 1 spec rows have no result because their orders were refused: ['RM-UV-KSORB-047/Identification (UV): specific absorbance at the absorption maximum (264 nm)'] (unapproved sample, read from the summary)

### STM-RM-048|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-049|BP
- certificate supporting rows: ['Hydroxyl value: 16 to 22 (stated by supplier)', 'Formaldehyde: Maximum 30 ppm (stated by supplier)', 'Ethylene glycol and diethylene glycol: Maximum 0.4 per cent (stated by supplier)', 'Ethylene oxide: Maximum 1 ppm (stated by supplier)', 'Dioxan: Maximum 10 ppm (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-050|USP
- RM-TIT-RED-050: refused: Replicate 1: The blank volume must be greater than the sample titrant volume.
- certificate: no: 1 spec rows have no result because their orders were refused: ['RM-TIT-RED-050/Reducing substances (as dextrose)'] (unapproved sample, read from the summary)

### STM-RM-052|USP
- certificate supporting rows: ['Particle size: Stated by supplier (stated by supplier)', 'Residual solvents: Stated by supplier (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-054|USP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-058|USP
- RM-BULKD: script error: no numeric target for limit type Qualitative
- RM-PSD-058: script error: no numeric target for limit type Qualitative
- certificate: no: 4 spec rows have no result because their orders were refused: ['RM-BULKD/Bulk density', 'RM-PSD-058/Particle size: 90% pass through 18 mesh', 'RM-PSD-058/Particle size: 50% pass through 30 mesh', 'RM-PSD-058/Particle size: 10% pass through 50 mesh'] (unapproved sample, read from the summary)

### STM-RM-059|USP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-061|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-062|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-067|USP
- certificate: no: spec rows not printed with a conforming result: ["RM-TIT-ZN-USP-067/Assay (zinc sulfate monohydrate, ZnSO4 · H2O) printed as '89.68 %' conform=False"] (approved sample)

### STM-RM-067|BP
- certificate: yes (approved sample)

### STM-RM-072|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-073|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-074|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-078|USP
- RM-PSD-CACO3-078: script error: no numeric target for limit type Qualitative
- certificate: no: 1 spec rows have no result because their orders were refused: ['RM-PSD-CACO3-078/Particle size distribution (D90 < 12 µm; D50 < 6 µm; D10 < 1.5 µm)'] (unapproved sample, read from the summary)

### STM-RM-080|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-081|BP
- certificate supporting rows: ['Particle Size Distribution: Retained 60 mesh (250 microns): NMT 8 % m/m (stated by supplier)', 'Particle Size Distribution: Retained 200 mesh (75 microns): NLT 45 % m/m (stated by supplier)', 'Identification Test (C): Degree of polymerization: NMT 350 (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-081|USP
- RM-BULKD: script error: no numeric target for limit type Qualitative
- certificate supporting rows: ['Particle Size Distribution: Retained 60 mesh (250 microns): NMT 8 % m/m (stated by supplier)', 'Particle Size Distribution: Retained 200 mesh (75 microns): NLT 45 % m/m (stated by supplier)', 'Identification Test (C): Degree of polymerization: NMT 350 (stated by supplier)']
- certificate: no: 1 spec rows have no result because their orders were refused: ['RM-BULKD/Bulk Density'] (unapproved sample, read from the summary)

### STM-RM-084|BP
- certificate supporting rows: ['Particles Size: Stated by Supplier (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-085|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-085|USP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-086|BP
- RM-TIT-MGST: refused: could not set up titrant for master 50
- certificate supporting rows: ['Particle-size distribution: No Particle is greater than 250 µm (stated by supplier)', 'Specific Surface area: 5.0 m2/gm to 15.0 m2/gm (stated by supplier)']
- certificate: no: spec rows not printed with a conforming result: ['RM-DESC/Appearance printed 0 times', 'RM-SOLUB/Solubility printed 0 times', 'RM-ID-CR-MG/Identification (Chemical Reaction) printed 0 times', 'RM-ID-FP-MG/Identification (Freezing Point) printed 0 times', 'RM-ID-AV-MG/Identification (Acid value) printed 0 times', 'RM-LT-CL/Chlorides printed 0 times', 'RM-LT-SO4/Sulfates printed 0 times', "RM-LOD/Loss on drying printed as '—' conform=False"]; 1 spec rows have no result because their orders were refused: ['RM-TIT-MGST/Assay'] (unapproved sample, read from the summary)

### STM-RM-087|None
- RM-COL-087: no handler for Observation
- certificate: no: 1 spec rows have no result because their orders were refused: ['RM-COL-087/Color'] (unapproved sample, read from the summary)

### STM-RM-088|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-090|None
- certificate supporting rows: ['Sulfur dioxide: Maximum 50 ppm (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-093|None
- certificate supporting rows: ['Grade: Pharmaceutical Grade (stated by supplier)', 'Solvent Used: MonoPropylene Glycol (stated by supplier)', 'Optical Rotation: Stated by Supplier (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-099|BP
- certificate: yes (approved sample)

### STM-RM-102|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-107|USP
- RM-BULKD: script error: no numeric target for limit type Qualitative
- certificate: no: 1 spec rows have no result because their orders were refused: ['RM-BULKD/Bulk Density'] (unapproved sample, read from the summary)

### STM-RM-114|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-119|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-125|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-127|None
- RM-PSD-127: script error: no numeric target for limit type Qualitative
- RM-TIT-CAHP-127: refused: could not set up titrant for master 51
- certificate supporting rows: ['Fluorides: Maximum 100 ppm (stated by supplier)']
- certificate: no: 4 spec rows have no result because their orders were refused: ['RM-TIT-CAHP-127/Assay', 'RM-PSD-127/Particle size: 90% pass through 35 mesh', 'RM-PSD-127/Particle size: 50% pass through 60 mesh', 'RM-PSD-127/Particle size: 10% pass through 120 mesh'] (unapproved sample, read from the summary)

### STM-RM-128|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-129|BP
- certificate supporting rows: ['Protein %: max 0.1% (stated by supplier)', 'Iodine Test: negative (stated by supplier)', 'Residue on 40 micron: results must be minimum 70% (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-130|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-131|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-132|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-133|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-139|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-141|USP
- certificate: yes (approved sample)

### STM-RM-143|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-144|None
- certificate supporting rows: ['Plant part used: Aerial parts (stem + leaf) (stated by supplier)', 'Extraction solvent: Ethanol + water (stated by supplier)', 'Extraction ratio: 5-7:1 (stated by supplier)', 'Particle size: 100% pass through mesh 80 (stated by supplier)', 'Residual solvents: Maximum 2.0% (stated by supplier)', 'Assay of silica: 7.0 to 10.0 % (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-145|None
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-146|USP
- certificate: yes (approved sample)

### STM-RM-152|BP
- certificate: yes (unapproved sample, read from the summary)

### Xanth gum|BP
- certificate supporting rows: ['Particle-size distribution: 100% through 60 mesh; not less than 95% through 80 mesh (stated by supplier)', 'Powder flow: Comply (stated by supplier)']
- certificate: yes (unapproved sample, read from the summary)

### Xanth gum|USP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-149|BP
- certificate supporting rows: ['Identification (TLC, Vitamin A Palmitate): Not applicable', 'Stabilizer: Stabilized with tocopherol (stated by supplier)']
- certificate: yes (approved sample)

### STM-RM-003|BP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-003|USP
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-002|BP
- certificate supporting rows: ['Chromatographic profile (GC, Lemon Oil): Not applicable', 'Identification by thin layer chromatography (Lemon Oil): Not applicable']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-035|BP
- certificate supporting rows: ['Aluminium (Citric Acid Monohydrate): Not applicable']
- certificate: yes (unapproved sample, read from the summary)

### STM-RM-124|BP
- certificate supporting rows: ['Dextrin (Sucrose): Not applicable']
- certificate: yes (unapproved sample, read from the summary)

