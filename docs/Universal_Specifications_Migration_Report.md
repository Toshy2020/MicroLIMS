# Universal Specifications - Migration Report (LIMSV2, local)

Migration `20260919072246_UniversalSpecifications`, dry-run on a restored copy of LIMSV2
(`LIMSV2_specscratch`, 2026-09-19) before applying to LIMSV2. Backup taken first:
`E:\MicroLIMS\db-backups\LIMSV2_before_universal_specs_20260919.dump`. Down() verified on the copy.

| Result | Rows | Notes |
|---|---|---|
| Count-Tiered, Spec 100 | 7 | TYMC-style, values unchanged |
| Count-Tiered, Spec 1000 | 7 | TAMC-style, values unchanged |
| Presence/Absence, Absent | 15 | pathogen tests; SpecLimit text unchanged ("Absent") |
| **Corrected** | 1 | a pathogen row read "Abent" - now "Absent" |

Parameter names were taken from the Test Master display name (TestCode when missing).

**Needs manual re-specification:** none in LIMSV2 - there are no Finished Product / physico-chemical
specifications yet. On any other database, rows whose SpecLimit was free text other than a count
limit or "Absent" are backfilled as Count-Tiered (legacy behaviour, still evaluated by the existing
parser) and should be re-entered with the correct Limit Type. Query to list them:

```sql
SELECT s."Id", i."Code" AS item, s."TestCode", s."SpecLimit"
FROM "Specifications" s JOIN "Items" i ON i."Id" = s."ItemId"
LEFT JOIN "TestDefinitions" td ON td."Code" = s."TestCode"
WHERE s."LimitType" = 4 AND td."WorkflowType" <> 0;
```

Legacy Alert/Action/Spec/Unit/Dilution Factor columns are kept (they remain the Count-Tiered values);
no column is dropped.
