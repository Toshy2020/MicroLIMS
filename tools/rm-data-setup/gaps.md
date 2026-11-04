# RM data setup: gaps (Task 3)

Companion of `catalog.json`. File names are the STM / spec sheet names under `E:/MicroLIMS/rls-tmp/rm-stms-txt`;
"STM" and "spec" are shortened to the material's own files. Codes in brackets are catalog codes.

## A. Blocking: a spec row or test cannot be set up correctly without an answer

Cross-cutting (system rules found in code)

1. **Visual / TLC limit tests with a numeric NMT limit.** The Qualitative workflow accepts only Qualitative or PresenceAbsence rows (`SingleResultRecorder.cs:280`). Sheet rows NMT on such tests: 025 Heavy metals 20 ppm; 051 Impurity E 0.1 % (TLC); 060 Iron 20 ppm; 063 and 064 Chlorides 350 ppm, Sulfates 300 ppm; 089 Iron 20 ppm; 150 Chlorides 200 ppm, Sulfates 200 ppm, Iron 10 ppm. Load each as a Qualitative row with the limit in the expected text ("Not more than 350 ppm: opalescence not more intense than the standard")? [RM-LT-HM, RM-LT-FE, RM-LT-CL, RM-LT-SO4, RM-IMP-E-SILD]
2. **ICP rows for % w/w results.** ICP spec rules hold only mineral rows with a label claim (mg/unit or % label claim) or impurity rows NMT in ug/g. Held back: 025 Total Se 40.9-42.6 %, Se as selenate NLT 99.0 %, Se as selenite NMT 1.0 % (spec:17-19); 150 USP Content of sodium 54.0-59.8 % and Potassium NMT 0.5 % (USP spec:15,18). How should these be held? (Lead 2 ppm in 151 is fine: 2 ug/g.) [RM-ICP-SE, RM-ICP-NA, RM-ICP-K]
3. **Karl Fischer water (051, 063, 064).** STM gives only the sample size (0.200 g / 0.500 g) and "calibrated Karl Fischer": no titrant/titre (mg water/mL), endpoint or replicates. The Water row is also the anchor of the anhydrous-basis assays of these three materials. Which KF reagent and titre, which endpoint, how many replicates? (051 STM:42; 063 and 064 STM:38) [RM-KF]

Per material

4. **025 Sodium Selenate.** One AAS procedure (selenate result x 78.9/188.94) feeds three rows; no speciation step for selenate/selenite; result basis not stated (formula divides by 100-MC); which result is the reported total Se? (STM:36; spec:17-19) [RM-ICP-SE]
5. **051 Sildenafil.** Assay injections and system suitability reference reference solutions (a), (b), (c) that the assay section never defines (it defines test (a), (b) and reference (d)). Which solutions are injected, which SST applies? (STM:43) [RM-HPLC-SILD-AS]
6. **053 Sodium Chloride Tablets.** Assay limit 95.0-105.0 % "of the stated amount" is a label-claim basis, only allowed for Finished Product items; no stated amount or average tablet weight; printed formula lacks the 50 mL silver nitrate term. (spec:14; STM:30) [RM-TIT-NACLT]
7. **053.** Identification has no procedure of its own ("proceed as test of assay"); spec "Comply with BP". Separate test or derived from the assay? (STM:27; spec:11) [RM-ID-NACLT]
8. **053.** Disintegration: number of tablets, stage-2 units and pass rule not stated (STM: dissolve within 15 min, water 19-21 C). Use the local DISINT-UI values (6 / 12 tablets, 2 stage-1 failures, 16 pass)? (STM:29; spec:13) [RM-DISINT]
9. **060 Sodium Metabisulfite.** Assay formula "(E.p x Eq.wt x F)/Wt x 100" shows no back-titration subtraction and "F" is garbled; assay basis not stated (as-is assumed); thiosulfate standardization equivalence is cut off. (STM:26,35; spec:19) [RM-TIT-SMB]
10. **063 / 064 Parabens, identification.** User ruled HPLC retention time; STM only describes an IR pellet, no chromatographic conditions or standard preparation. Use the assay HPLC conditions and the RS/WS standard? (063 STM:32; 064 spec:14) [RM-ID-RT]
11. **063 / 064 assay.** Formula copied from another STM (mentions Sildenafil, duplicated terms); MC is the moisture of the working standard while the limit is on the anhydrous substance and a Water row exists. Which moisture goes into 100/(100-MC)? (063 STM:39; 064 STM:39) [RM-HPLC-PPB-AS, RM-HPLC-MPB-AS]
12. **064 Methyl Paraben, related substances.** Spec sheet says "Complies with BP"; the STM gives numeric limits (Impurity A 3.0 %, each unspecified 0.5 %, sum of others 1.0 %, disregard 0.1 %). Confirm the three rows are taken from the STM. (spec:19; STM:35) [RM-HPLC-MPB-RS]
13. **089 Sodium Starch Glycolate Type A, minimum quantity.** Spec sheet 120 g; STM 110 g routine and retention, 50 g retest. Which? (STM:41; spec)
14. **089, identification C.** "To an acidified solution add iodinated potassium iodide R1": which solution, which acid, what volume? (STM:32) [RM-ID-SSG-C]
15. **089, TAMC/TYMC.** Both documents print "NMT 103 / 102" (superscripts lost); rows hold 1000 / 100 CFU/g. Confirm 10^3 and 10^2. (STM:39)
16. **091 Sodium Lauryl Sulfate, NaCl + Na2SO4.** Total NMT 8.0 % from two determinations (argentometric NaCl, BaSO4 gravimetry), no formula for either and no summing rule; spec technique says Thermogravimetry. (STM:33; spec:19) [RM-NACL-SLS]
17. **091, Alkalinity.** Limit is NMT 0.5 mL of 0.1 M HCl (a volume); STM says "Comply with BP"; end-point colours not stated; the titration workflow reports content %. (spec:17; STM:31) [RM-TIT-SLS-ALK]
18. **091, Assay (NLT 85.0 %).** Two-phase benzethonium titration: no formula, no basis, no preparation or standardization of 0.004 M benzethonium chloride or of the dimidium bromide-sulfan blue indicator; the titration types offered do not name it. (STM:34; spec:20) [RM-TIT-SLS]
19. **091, Non-esterified alcohols.** STM limit is a residue of max 0.4 g from 10.0 g (stated); sheet is NMT 4.0 %; no calculation stated. Same 10.0 g basis (4.0 %)? (STM:32; spec) [RM-NONEST-ALC]
20. **097 Pyridoxine, minimum quantity.** Testing 100 g, retention 80 g, no retest quantity stated anywhere. (STM:58)
21. **097 BP, UV identification.** Three specific-absorbance ranges at three maxima on solutions B and C (425-445 at 288-296 nm; 175-195 at 248-256 nm; 345-365 at 320-327 nm); one UV test records one specific absorbance. How to hold? (BP spec:14) [UV-PYRI]
22. **126 Sodium Hypochlorite, miscibility.** STM reads "add 0 ml of sample"; sample volume unreadable. (STM:31) [RM-MISC]
23. **126, total alkalinity and assay.** Results are % w/w using "weight per mL" (procedure not stated), the assay formula has no 10 mL to 250 mL dilution and no mg-to-g step, samples are taken by volume; spec says "% of chlorine". (STM:32-33; spec:14) [RM-TIT-NAOCL-ALK, RM-TIT-NAOCL]
24. **147 Soya Isoflavone, tapped density.** STM fills a 10 mL cylinder, weighs and divides; no tapping step. Is it bulk density, or is a tapping procedure missing? (STM:32) [RM-TAPDEN]
25. **147, description.** Row includes "bitter astringent taste"; STM procedure is visual only. Keep the taste in the expected text without a procedure? (STM:30)
26. **150 Sodium Hydroxide, minimum quantity.** Spec 50 g; combined STM routine/retention 50 g, retest 100 g; BP STM 30 g / 60 g; undated STM 30 g / 60 g. (combined STM:42; BP STM:38; spec:21)
27. **150 BP, identification pH (NLT 11.0).** Combined STM dilutes 1 mL of the 0.1 g/10 mL solution to 100 mL; the BP-only STM measures it undiluted. Which for BP? (combined STM:30; BP STM:30) [RM-PH]
28. **150, carbonates.** BP Carbonates NMT 2.0 % and USP Sodium carbonate NMT 3.0 % come from the second-stage volume of the phenolphthalein / methyl orange titration; the workflow has one endpoint. How to hold? (combined STM:32 and 40; BP STM:32) [RM-CARB-NAOH]
29. **151 Siberian Ginseng, odor and taste.** Expected text "Characteristic taste of ginger" on a ginseng extract; copy error? (spec:13; STM:32) [RM-ODOR]
30. **56 Riboflavin phosphate, specific optical rotation (38.0-43.0, dried substance).** No unit, wavelength, temperature, tube length or formula; Measurement tests cannot hold a dried basis. (spec:17) [RM-OPTROT]
31. **56, minimum quantity.** Sheet "200 ml", user answer 200 g routine; STM retest 25 g and retention 50 g. Confirm retest 25 g and retention. (spec:22; STM:38)

## B. Method objects that cannot be completed

Common to every new titrant (10): shelf life, storage, standardization replicates, factor limits, max RSD and validity days are not stated in any STM. Local titrants use 3 replicates, factor 0.97-1.03 (0.98-1.02 for 1 N), max RSD 0.2 %, validity 30 days, "Tightly closed, room temperature". Use the same? (`catalog.json` `_meta.proposedHouseDefaultsForTitrants`, not applied.)

1. **0.1 M Sodium thiosulfate VS** (060, 126): standardized against 0.033 M potassium bromate VS (a solution; the system offers a weighed primary standard or another titrant; potassium bromate is not a Primary Standard entry); equivalence "2.783 mg of [cut off]". (060 STM:26; 126 STM:28)
2. **0.1 M Silver nitrate VS** (053): standardized with a quantity of tablet powder holding 0.1 g NaCl, not a primary standard. Standardize against Sodium chloride (RG-026, 5.844 mg/mL stated) instead? (STM:25)
3. **0.5 M Hydrochloric acid VS** (126): equivalence quoted for 1 M HCl only (121.1 mg tris); the 0.5 M value is not stated. (STM:28)
4. **0.1 M Hydrochloric acid VS** (091, from 1 M HCl VS, no components), **0.004 M Benzethonium chloride VS** (091, nothing stated). (091 STM:26,33,34)
5. **Mobile phase / diluent solutions** (Sildenafil A, B, diluent; Paraben diluent; Pyridoxine USP diluent): components not entered, they are mixtures of a prepared buffer or of another solution and quantities would be derived. pH tolerance not stated (Pyridoxine mobile phases). Shelf life/storage not stated.
6. **HPLC methods, 8 of 8 incomplete.** Sildenafil assay (SILD-AS) and RS (SILD-RS): reference standard entry missing, standard injections not stated, assay injection list unclear (A5). Paraben assay (PPB-AS, MPB-AS): run time not stated, standard entry missing, no RSD limit for the 6 injections. Paraben RS (PPB-RS, MPB-RS): standard entry missing, standard injections not stated (run times derived 11.25 and 11.5 min). Pyridoxine USP (PYR-USP): run time, theoretical weights (stock flask volume), replicates for the 3.0 % RSD not stated; internal standard has no entry. Pyridoxine RS (PYR-RS): standard injections not stated (run time 30 min derived).
7. **ICP methods, 4 of 4 incomplete.** All: element view (axial/radial) not stated for flame AAS, effective date to be read from the STM. AAS-SE: one standard level (100 mg/L) only, minimum correlation not stated, which result carries the 78.9/188.94 factor (A4). AAS-NA: no sodium Reference Standard entry. AAS-K: no potassium entry, final sample volume not stated. AAS-PB: minimum correlation not stated; two lead entries match (RS-035, RS-026).

## C. Reagents / standards with no matching material master entry

Reference standards (category Reference Standard needed by HPLC analytes and ICP calibration): Sildenafil citrate CRS/WS, Sildenafil impurity A CRS, Sildenafil for peak identification CRS, Imidazole CRS (051); Propyl / Sodium propyl para hydroxybenzoate RS/WS (063); Methyl / Sodium methyl para hydroxybenzoate RS/WS (064); Ethyl para hydroxybenzoate RS and 4-hydroxybenzoic acid (063, 064); p-Hydroxybenzoic acid (internal standard), Pyridoxine impurity A CRS, 4-Deoxypyridoxine hydrochloride (097); Sodium lauryl sulfate RS (091); Riboflavin sodium phosphate CRS (056; "Riboflavin RS" RS-014 is a different substance); sodium standard (NaCl) and potassium standard (KCl) for AAS (150).

Primary standard: potassium bromate as a Primary Standard (plain reagent RG-088 exists).

Indicators (titration indicator must be an Indicator-category entry): ferric ammonium sulfate solution R2 (053; reagent RG-138), phenol red solution (091; reagent RG-050), sodium fluoresceinate 2 g/L (091; RG-056 sodium fluorescein), dimidium bromide-sulfan blue mixed solution (091; reagents RG-222/RG-238, RG-248).

Matched (titrant components, standards, indicators): see `catalog.json` `reagentLinks`. Two lead standards match "Lead Standard Solution 1000 ppm" (RS-035, RS-026). Culture media in 147 and the micro STMs belong to the media module, not the material master.

## D. Non-blocking notes

- **Replicate counts** are 1 on new gravimetric, measurement and titration tests (single determination described); local LOD-UI uses 2 and local titration tests 3. Confirm.
- **LOD-UI** display name carries "(UI test)"; LOD temperature/time differ per material (105 C 2 h; 130 C 1.5 h in 089; 105 C 5 h in 056; USP vacuum/silica gel 4 h in 097) and are not definition fields. A per-test condition (or the STM) carries them.
- **DESC-RM, LOD-UI, UV-RIBO, UV-PHOS, UV-PYRI** exist on local LIMSV2 only (created by hand); **Salmonella** has a trailing space in its code on local LIMSV2. Check prod before the loader matches them.
- **Equivalency factors** were converted from "mg per mL of titrant" to mg per mmol/mEq (F = mg/mL divided by molarity) for 060, 097 BP, 126, 053, 091; derivation recorded per test.
- **Titrant names/recipes:** STM titrants are 0.1 M; local ones are 0.1 N with other recipes (AgNO3 17.5 g vs 17.0 g, perchloric acid 21 vs 30 mL anhydride, thiosulfate 26 g pentahydrate vs 25 g unspecified hydrate). New solutions follow the STM; the thiosulfate hydrate, hydrochloric acid and sulfuric acid concentrations are not stated (entries assumed, flagged in the catalog).
- **Strengths of dilute reagents** not defined: dilute nitric acid (053), dilute HCl and acetic acid (060, 063/064 sulfates), 2 M acetic acid, hydrogen peroxide 20 vol (126), acid molybdate and ferrous sulfate solutions (056), ammonium carbonate, 100 g/L and 500 g/L sulfuric acid (091), iodinated KI concentration (089 defines R1; 060 does not).
- **Hygroscopicity** (part of Appearance in 063, 064, 089, 056): the calculation expression is missing in all four STMs.
- **Solubility:** first solvent unnamed in 025, 060, 063, 089, 151 (water implied); 091 sheet says "partly soluble in ethanol", not a term of the scale.
- **Spec references:** 060 sheet header code "SM-RM-060"; 063 spec file is named "Sodium Methyl Paraben" but holds the propyl sheet; 097 issue request file is named "Sodium Methyl Paraben"; 056 name differs (Riboflavin sodium phosphate / 5-phosphate), versions differ between STM and sheet (053, 056); 150 undated STM superseded by the coded files; BP year missing on several sheets.
- **pH technique** on 063/064 sheets says Voltammetry, STM says pH meter. 091 NaCl/Na2SO4 sheet technique says Thermogravimetry (A16).
- **BP formulas with mg vs g** (097 BP assay, 150 BP assay): Eq.wt in mg, Wt in g as printed; the system's own titration formula is used. 097 USP chloride content and USP HPLC assay are "dried basis" without LOD correction in the formula.
- **Related substances** extras on sheets or STMs only: 051 reporting threshold 0.05 % and citrate peak disregarded (used as disregard limit); 097 disregard limit 0.05 % (STM only); 063/064 impurity C and A identification peaks.
- **025:** identification Test B (AAS response, "Comply") is in the STM but not on the sheet (no row); STM equipment list has no AAS or bulk density cylinder; spec technique for LOD says Thermogravimetry while the STM uses an oven.
- **060:** the STM is the "without arsenic" version; no arsenic test exists (no row). Both identification tests are labelled "Test B".
- **151:** micro limits in the STM cite "In-house Specification for Calcium Carbonate MD" (copy error); Salmonella pre-enrichment time 18-24 h vs 24-48 h unclear; lead conversion to ppm in the sample (x 25 / 1) implied, not stated.
- **147 / 151 / 089 micro:** sheets cite USP, STMs cite in-house; media, incubation and dilutions are in the micro definitions (SOP C2I-91-128), not in these STMs.
- **Not-applicable drafts** (Riboflavin phosphate related substances and composition) are loader objects, not catalog objects (source: `Not applicable For Riboflavin 5-phosphate.txt:2`).
- **Not created by instruction:** Sodium Hypochlorite disinfectant efficacy; Soya isoflavone total isoflavones (UV); Siberian Ginseng assay; Sodium Lauryl Sulfate micro limits.
