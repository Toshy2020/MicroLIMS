# Material file format

One file per material: `materials/<STM code>.json` (for example `materials/STM-RM-097.json`).
Enum values are the API's names, spelled exactly as below.

```json
{
  "itemCode": "STM-RM-097",
  "itemName": "Pyridoxine Hydrochloride",
  "stmFile": "Pyridoxine Hydrochloride__STM-Pyridoxine (004) after edit.txt",
  "specFiles": ["Pyridoxine Hydrochloride__Specifications sheet Pyridoxine Hydrochloride BP.txt"],
  "minQuantity": { "routine": 50, "retest": 20, "unit": "g", "source": "<file>:<line>" },
  "grades": [
    {
      "grade": "BP",
      "rows": [
        {
          "testCode": "RM-LOD",
          "parameterName": "Loss on drying",
          "limitType": "NotMoreThan",
          "lower": null, "upper": 0.5, "lowerInclusive": null,
          "unit": "%",
          "expectedResultText": null,
          "expectedState": null,
          "resultBasis": null,
          "referenceStandard": "BP 2024",
          "source": "<spec file>:<line>"
        }
      ]
    }
  ],
  "tests": [
    {
      "testCode": "RM-LOD",
      "procedure": "short summary of the STM procedure: conditions, amounts, equipment, calculation",
      "kind": "general | specific",
      "workflow": "Gravimetric | Titration | HplcMethodAssay | IcpMethodAssay | UvSpectrophotometry | Qualitative | Measurement | CountTest | Observation",
      "details": { "free-form, everything the STM states that a method or test master needs": "..." },
      "source": "<stm file>:<line range>"
    }
  ],
  "notApplicable": [
    { "testCode": "...", "grade": null, "justification": "...", "criticality": "...", "immediateAction": "...",
      "longTermAction": "...", "reference": "...", "source": "<file>:<line>" }
  ],
  "supplierAttributes": [
    { "grade": null, "name": "Particle size", "statedValue": "20 mesh", "source": "<file>:<line>" }
  ],
  "reagents": [ { "name": "as written in the STM", "use": "titrant | reagent | standard | indicator", "source": "<file>:<line>" } ],
  "openQuestions": [ { "question": "...", "source": "<file>:<line>" } ]
}
```

Field rules:

- `limitType`: `Range`, `NotMoreThan`, `NotLessThan`, `Qualitative` (text expectation, e.g. description, identification), `PresenceAbsence` (pathogens; set `expectedState` `Absence` or `Presence`), `CountTiered` (TAMC/TYMC count limits), `DisintegrationTime` (disintegration test: upper limit in min, unit "min"). Visual/comparison limit tests with a number are `Qualitative` rows with the limit in the text (e.g. "Complies (NMT 20 ppm)").
- `lowerInclusive`: only for `Range`; `null` elsewhere.
- `resultBasis`: only on Titration, UV and HPLC rows; `null` on Measurement, Gravimetric, Qualitative rows (analyst-entered Measurement tests keep their unit). Values: `PercentAsIs`, `PercentDriedBasis` ("dried substance"), `PercentAnhydrousBasis` ("anhydrous substance"), `Ppm`, `MgPerKg`; `null` when the row is not an assay.
- `testCode`: a provisional code; Task 3 renames them to catalog codes. Use `RM-` prefix and short names.
- Per-container description tests: put `"perContainer": true` in the test's `details`.
- `source`: file name (as in `E:/MicroLIMS/rls-tmp/rm-stms-txt`) and line number(s). Every value must have one.

- `heldBack`: `"<reason>"` on a row, test or catalog object: the loader skips it (HPLC/ICP method incomplete). Listed in `reports/held-back.md`.
- `catalog.json` `materialEntries[]`: `{name, category (Indicator|ReferenceStandard|PrimaryStandard), codePrefix, code: null, baseUnit, usedBy, source, loadedObjectNeedsIt}`; the loader creates absent entries by name, name only, no stock.
