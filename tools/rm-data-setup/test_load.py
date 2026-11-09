"""Tests for load.py against a fake in-memory API. Run: py -I tools/rm-data-setup/test_load.py"""
import copy
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from urllib.parse import parse_qs, urlparse

sys.path.insert(0, str(Path(__file__).resolve().parent))
import load  # noqa: E402

BASE = "http://localhost:5000/api"

CATALOG = {
    "_meta": {"proposedHouseDefaultsForTitrants": {
        "replicateCount": 3, "factorMin": 0.97, "factorMax": 1.03, "maxRsdPercent": 0.2, "validityDays": 30,
        "shelfLife": "30 days", "storageCondition": "Tightly closed, room temperature"}},
    "materialEntries": [
        {"name": "Silver nitrate", "category": "Reagent"},
        {"name": "Sodium chloride PS", "category": "PrimaryStandard"},
        {"name": "Held entry", "category": "Reagent", "heldBack": "no supplier"},
    ],
    "solutions": [{
        "name": "0.1 M Silver nitrate VS", "type": "Titrant", "finalVolumeMl": 1000, "instructions": "17 g in 1000 mL",
        "components": [{"entry": "Silver nitrate", "quantity": 17.0, "unit": "Gram"}],
        "nominalStrength": 0.1, "strengthUnit": "Molar", "standardizationMode": "PrimaryStandard",
        "standardEntry": "Sodium chloride PS", "equivalenceMgPerMl": 5.844}],
    "testDefinitions": [
        {"code": "DESC-RM", "displayName": "Description", "section": "Physicochemical Laboratory",
         "workflowType": "Qualitative", "equationType": "Qualitative", "physchemArea": "RawPackaging"},
        {"code": "RM-TIT", "displayName": "Titration assay", "section": "Physicochemical Laboratory",
         "workflowType": "Titration", "equationType": "Titration", "titrantSolution": "0.1 M Silver nitrate VS",
         "titrationType": "Precipitation", "titrationEquivalencyFactor": 58.44, "replicateCount": 1},
        {"code": "TAMC", "displayName": "TAMC", "section": "Physicochemical Laboratory",
         "workflowType": "CountTest", "equationType": "None"},
        {"code": "RM-HELD", "displayName": "Held", "section": "Physicochemical Laboratory",
         "workflowType": "Qualitative", "equationType": "Qualitative", "heldBack": "method details missing"},
    ],
    "mapping": {"STM-T-1": {"RM-DESC": "DESC-RM", "RM-ASSAY": "RM-TIT", "RM-TAMC": "TAMC", "RM-HELD": "RM-HELD"}},
}

MATERIALS = [
    {"itemCode": "STM-T-1", "itemName": "Test material",
     "minQuantity": {"routine": 100, "retest": 50, "unit": "g"},
     "grades": [{"grade": "BP", "rows": [
         {"testCode": "RM-DESC", "parameterName": "Appearance", "limitType": "Qualitative", "lower": None, "upper": None,
          "lowerInclusive": None, "unit": None, "expectedResultText": "White powder", "expectedState": None,
          "resultBasis": None, "referenceStandard": "BP"},
         {"testCode": "RM-ASSAY", "parameterName": "Assay", "limitType": "Range", "lower": 99.0, "upper": 101.0,
          "lowerInclusive": True, "unit": "%", "expectedResultText": None, "expectedState": None,
          "resultBasis": "PercentAsIs", "referenceStandard": "BP"},
         {"testCode": "RM-TAMC", "parameterName": "TAMC", "limitType": "CountTiered", "lower": None, "upper": 1000,
          "lowerInclusive": None, "unit": "CFU/g", "expectedResultText": None, "expectedState": None,
          "resultBasis": None, "referenceStandard": "USP"},
         {"testCode": "RM-DESC", "parameterName": "Held row", "limitType": "Qualitative", "lower": None, "upper": None,
          "lowerInclusive": None, "unit": None, "expectedResultText": "x", "expectedState": None,
          "resultBasis": None, "referenceStandard": "BP", "heldBack": "contradictory source"},
         {"testCode": "RM-HELD", "parameterName": "Held test row", "limitType": "Qualitative", "lower": None,
          "upper": None, "lowerInclusive": None, "unit": None, "expectedResultText": "y", "expectedState": None,
          "resultBasis": None, "referenceStandard": "BP"},
     ]}],
     "notApplicable": [{"testCode": "RM-DESC", "grade": "BP", "justification": "none", "criticality": "Minor",
                        "immediateAction": "Remove", "longTermAction": "PR", "reference": None}],
     "supplierAttributes": [{"grade": "BP", "name": "Particle size", "statedValue": "20 mesh"}]},
    {"itemCode": "STM-T-MISSING", "itemName": "Not on server", "minQuantity": {"routine": 10, "retest": None, "unit": "g"},
     "grades": [{"grade": "BP", "rows": [
         {"testCode": "RM-DESC", "parameterName": "Appearance", "limitType": "Qualitative", "lower": None, "upper": None,
          "lowerInclusive": None, "unit": None, "expectedResultText": "White", "expectedState": None,
          "resultBasis": None, "referenceStandard": "BP"}]}],
     "notApplicable": [], "supplierAttributes": []},
]


class FakeApi:
    """Just enough of the MicroLIMS API: stores what the loader writes and returns it the way the API would."""

    def __init__(self):
        self.next_id = 100
        self.calls = []
        self.sections = [{"sectionId": 1, "sectionName": "Physicochemical Laboratory", "sectionCode": "PC"}]
        self.entries, self.solutions, self.tests, self.specs, self.attrs, self.nas = [], [], [], [], [], []
        self.items = [{"id": 1, "code": "STM-T-1", "name": "Test material", "category": "RawMaterial", "sopNumber": "",
                       "assignedTests": [], "minSampleQuantity": None, "minRetestSampleQuantity": None,
                       "minSampleQuantityUnit": None}]

    def nid(self):
        self.next_id += 1
        return self.next_id

    def __call__(self, method, url, body=None, token=None):
        u = urlparse(url)
        path = u.path[len("/api"):]
        self.calls.append((method, path))
        if path == "/auth/login":
            return {"success": True, "data": {"token": "T"}}
        ok = lambda d: {"success": True, "data": d}
        if method == "GET":
            if path == "/org/sections":
                return ok(self.sections)
            table = {"/masterdata/material-masters": self.entries, "/masterdata/solution-masters": self.solutions,
                     "/masterdata/test-definitions": self.tests, "/items": self.items,
                     "/masterdata/hplc-methods": [], "/masterdata/icp-methods": []}
            if path in table:
                return ok(copy.deepcopy(table[path]))
            if path == "/masterdata/specifications":
                item_id = int(parse_qs(u.query)["itemId"][0])
                return ok(copy.deepcopy([s for s in self.specs if s["itemId"] == item_id]))
            if path.endswith("/supplier-attributes"):
                return ok(copy.deepcopy(self.attrs))
            if path.endswith("/not-applicable-tests"):
                return ok(copy.deepcopy(self.nas))
            raise load.ApiError("HTTP 404: " + path)
        item = self.items[0]
        if method == "POST":
            if path == "/masterdata/material-masters":
                r = dict(body, id=self.nid(), isActive=True)
                self.entries.append(r)
                return ok(r)
            if path == "/masterdata/solution-masters":
                r = dict(body, id=self.nid(), isActive=True)
                r.setdefault("blankRequired", False)
                self.solutions.append(r)
                return ok(r)
            if path == "/masterdata/test-definitions":
                r = dict(body, id=self.nid(), isActive=True)
                self.tests.append(r)
                return ok(r)
            if path == "/masterdata/specifications":
                if body["testCode"] not in {t["testCode"] for t in item["assignedTests"]}:
                    raise load.ApiError(f"HTTP 400: Test '{body['testCode']}' is not assigned to item {body['itemId']}.")
                r = dict(body, id=self.nid())
                r.setdefault("lowerInclusive", True)
                r["unit"] = r.get("unit") or ""
                if r["limitType"] != "CountTiered":
                    r["specLimit"] = "canonical"
                self.specs.append(r)
                return ok(r)
            if path.endswith("/supplier-attributes"):
                r = dict(body, id=self.nid())
                self.attrs.append(r)
                return ok(r)
            if path.endswith("/not-applicable-tests"):
                if body["testCode"] not in {t["testCode"] for t in item["assignedTests"]}:
                    raise load.ApiError("HTTP 400: test is not assigned")
                r = dict(body, id=self.nid(), status="Draft")
                self.nas.append(r)
                return ok(r)
        if method == "PUT":
            tail = path.rsplit("/", 1)
            rid = int(tail[1])
            tables = {"/masterdata/specifications": self.specs, "/masterdata/test-definitions": self.tests,
                      "/masterdata/solution-masters": self.solutions, "/masterdata/material-masters": self.entries,
                      "/supplier-attributes": self.attrs, "/items": self.items}
            rec = next(r for r in tables[tail[0]] if r["id"] == rid)
            rec.update(body)
            if tail[0] == "/masterdata/solution-masters":
                assert body.get("reason") == load.REASON
            if tail[0] == "/masterdata/test-definitions":
                assert body.get("changeReason") == load.REASON
            if tail[0] == "/masterdata/specifications":
                rec["unit"] = rec.get("unit") or ""
            return ok(rec)
        raise load.ApiError(f"HTTP 404: {method} {path}")

    def writes(self):
        return [c for c in self.calls if c[0] in ("POST", "PUT") and c[1] != "/auth/login"]


class LoaderTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        d = Path(self.tmp.name)
        (d / "materials").mkdir()
        (d / "catalog.json").write_text(json.dumps(CATALOG), encoding="utf-8")
        for m in MATERIALS:
            (d / "materials" / f"{m['itemCode']}.json").write_text(json.dumps(m), encoding="utf-8")
        self.dir = d
        self.fake = FakeApi()
        self._orig = load.http
        load.http = self.fake
        os.environ["RM_LOADER_PASSWORD"] = "not-a-real-password"

    def tearDown(self):
        load.http = self._orig
        self.tmp.cleanup()

    def run_loader(self, *flags, base=BASE):
        lines = []
        rc = load.main(["--base-url", base, "--user", "u", "--data-dir", str(self.dir), *flags], out=lines.append)
        return rc, lines

    def counts(self, lines):
        totals = next(l for l in lines if l.startswith("totals:"))
        return {k: int(v) for k, v in (p.strip().split(" ")[:2] for p in totals[len("totals: "):].split(" (")[0].split(","))}

    def test_1_dry_run_never_writes(self):
        rc, lines = self.run_loader("--dry-run")
        self.assertEqual(rc, 0, lines)
        self.assertEqual(self.fake.writes(), [])
        self.assertGreater(self.counts(lines)["create"], 5)
        self.assertEqual(self.counts(lines)["error"], 0, lines)

    def test_2_apply_creates_each_object_once(self):
        rc, lines = self.run_loader("--apply")
        self.assertEqual(rc, 0, lines)
        self.assertEqual([e["name"] for e in self.fake.entries], ["Silver nitrate", "Sodium chloride PS"])
        self.assertEqual([e["code"] for e in self.fake.entries], ["RG-001", "PS-001"])
        self.assertEqual(len(self.fake.solutions), 1)
        sol = self.fake.solutions[0]
        self.assertEqual((sol["shelfLifeValue"], sol["shelfLifeUnit"], sol["replicateCount"]), (30, "Days", 3))
        self.assertEqual(sol["standardEntryId"], self.fake.entries[1]["id"])
        self.assertEqual(sorted(t["code"] for t in self.fake.tests), ["DESC-RM", "RM-TIT", "TAMC"])
        tit = next(t for t in self.fake.tests if t["code"] == "RM-TIT")
        self.assertEqual(tit["titrantSolutionMasterId"], sol["id"])
        self.assertEqual(len(self.fake.specs), 3)
        self.assertEqual(next(s for s in self.fake.specs if s["testCode"] == "TAMC")["specLimit"], "1000")
        self.assertEqual(len(self.fake.attrs), 1)
        self.assertEqual(len(self.fake.nas), 1)
        self.assertEqual(self.fake.nas[0]["status"], "Draft")
        item = self.fake.items[0]
        self.assertEqual((item["minSampleQuantity"], item["minRetestSampleQuantity"], item["minSampleQuantityUnit"]),
                         (100, 50, "g"))
        self.assertEqual(sorted(t["testCode"] for t in item["assignedTests"]), ["DESC-RM", "RM-TIT", "TAMC"])
        self.assertEqual(self.counts(lines)["error"], 0, lines)
        # each POST path was hit once per object, never twice
        posts = [c for c in self.fake.calls if c[0] == "POST" and c[1] != "/auth/login"]
        self.assertEqual(len(posts), 2 + 1 + 3 + 3 + 1 + 1)

    def test_3_second_apply_changes_nothing(self):
        self.run_loader("--apply")
        self.fake.calls.clear()
        rc, lines = self.run_loader("--apply")
        c = self.counts(lines)
        self.assertEqual((c["create"], c["update"], c["error"]), (0, 0, 0), lines)
        self.assertEqual(self.fake.writes(), [])

    def test_4_changed_limit_reports_update_with_diff(self):
        self.run_loader("--apply")
        m = copy.deepcopy(MATERIALS[0])
        m["grades"][0]["rows"][1]["upper"] = 102.0
        (self.dir / "materials" / "STM-T-1.json").write_text(json.dumps(m), encoding="utf-8")
        rc, lines = self.run_loader("--apply")
        updates = [l for l in lines if ": update" in l]
        self.assertEqual(len(updates), 1, lines)
        self.assertIn("upperLimit: 101.0 -> 102.0", updates[0])
        self.assertEqual(self.counts(lines)["update"], 1)
        self.assertEqual(next(s for s in self.fake.specs if s["parameterName"] == "Assay")["upperLimit"], 102.0)

    def test_5_prod_apply_needs_approval(self):
        rc, lines = self.run_loader("--apply", base="https://rls.example.com/api")
        self.assertEqual(rc, 2)
        self.assertEqual(self.fake.calls, [])
        rc, _ = self.run_loader("--apply", "--i-approve-prod", base="https://rls.example.com/api")
        self.assertEqual(rc, 0)
        rc, _ = self.run_loader("--dry-run", base="https://rls.example.com/api")   # reads need no approval
        self.assertEqual(rc, 0)

    def test_6_held_back_rows_and_objects_are_skipped(self):
        rc, lines = self.run_loader("--apply")
        self.assertTrue(any("Held row" in l and ": skip (held back: contradictory source)" in l for l in lines), lines)
        self.assertTrue(any(l.startswith("entry Held entry: skip (held back") for l in lines), lines)
        self.assertTrue(any(l.startswith("test RM-HELD: skip (held back") for l in lines), lines)
        self.assertTrue(any("Held test row" in l and "skip (test RM-HELD held back" in l for l in lines), lines)
        self.assertNotIn("Held entry", [e["name"] for e in self.fake.entries])
        self.assertNotIn("RM-HELD", [t["code"] for t in self.fake.tests])
        self.assertFalse(any(s["parameterName"] in ("Held row", "Held test row") for s in self.fake.specs))

    def test_7_missing_item_is_skipped_not_created(self):
        rc, lines = self.run_loader("--apply")
        self.assertTrue(any(l == "item STM-T-MISSING: skip (item not found)" for l in lines), lines)
        self.assertEqual([i["code"] for i in self.fake.items], ["STM-T-1"])
        self.assertFalse([c for c in self.fake.calls if c[0] == "POST" and c[1] == "/items"])
        self.assertTrue(all(s["itemId"] == 1 for s in self.fake.specs))

    def test_only_limits_to_one_item(self):
        rc, lines = self.run_loader("--dry-run", "--only", "STM-T-MISSING")
        self.assertFalse(any("STM-T-1" in l for l in lines))
        self.assertTrue(any("STM-T-MISSING" in l for l in lines))


if __name__ == "__main__":
    unittest.main(verbosity=1)
