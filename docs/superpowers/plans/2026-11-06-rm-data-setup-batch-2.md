# RM data setup, batch 2 (73 materials) Plan

Same pipeline and rules as Plan 6 (`2026-11-04-rm-data-setup.md`, `tools/rm-data-setup/README.md`); the user chose
"load all, same as Plan 6" on 2026-11-06.

**Sources:** `E:\RM STMs\98c51911-New_STM_2025.zip`, `d92a33eb-New_STM_2025-2.zip`, `18dd0e99-New_STM_2025-4.zip`,
extracted to `E:/MicroLIMS/rls-tmp/rm-stms2`, text in `rm-stms2-txt-v2` (`rm2-tools/docx2txt.py`, reproduces the
batch-1 text). Inventory: `rm2-tools/inventory.json`.

**Not set up:** folders marked "Under Issue" / "Need Update" (Calcium Pantothenate, Chromium Chloride, Ferrous
Fumarate, HPC, L-Arginine, L-Cysteine, L-Methionine, Para-aminobenzoic acid), water SOPs, "Cancellation", the empty
"Vitamin c 97%" folder.

**Spec-only folders** (no STM): spec rows loaded; general tests load, material-specific tests (assays, specific
identifications) held back "no STM".

**Items:** matched to local `Items.Code`; materials with no item (Calcium Carbonate DC, Methylene Blue, Zinc Sulfate
Heptahydrate) are reported, not created.

## Tasks

1. Transcribe the 73 materials (11 parallel workers, brief `rm2-tools/TRANSCRIBE.md`) into `tools/rm-data-setup/materials/`.
2. Fidelity review: second reader checks every row against its source line.
3. Catalog: add the new test definitions, solutions, methods, material entries; gaps to `gaps.md`. **Stop: user decides the gaps.**
4. Local load (LIMSV2 backup first), rerun shows 0 changes.
5. E2E: one sample per material and grade through approval (`e2e.py`).
6. Production: same as batch 1 (Neon backup branch, then load).
