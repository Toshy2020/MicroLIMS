"""Idempotent loader for the RM data setup (Plan 6).

Reads catalog.json and materials/*.json and creates or updates master data through the MicroLIMS
REST API (so every change is audited). Python 3 standard library only.

  py -I load.py --base-url http://localhost:5000/api --user <name> --dry-run
  py -I load.py --base-url http://localhost:5000/api --user <name> --apply [--only STM-RM-097]
  py -I load.py --base-url https://<prod>/api --user <name> --apply --i-approve-prod

Order: material master entries, solutions, test definitions, items (minimum quantities and the tests a
spec needs assigned), specifications per grade, supplier attributes, not-applicable drafts.
Anything marked "heldBack" in the data files is skipped. The password comes from getpass (or the
RM_LOADER_PASSWORD environment variable for local automation) and is never printed.
"""
import argparse
import getpass
import json
import os
import re
import sys
import urllib.error
import urllib.request
from collections import Counter
from pathlib import Path
from urllib.parse import urlparse

HERE = Path(__file__).resolve().parent
REASON = "RM data setup (Plan 6) per STM"
LOCAL_HOSTS = {"localhost", "127.0.0.1", "::1"}
DEFAULT_SECTION = "Physicochemical Laboratory"
ENTRY_PREFIX = {"Reagent": "RG-", "Indicator": "IND-", "ReferenceStandard": "RS-", "PrimaryStandard": "PS-"}


class ApiError(Exception):
    pass


class Unresolved(Exception):
    """A name reference that cannot be resolved; the object is skipped with this reason."""


# ---------------------------------------------------------------- HTTP (the one function tests replace)

def http(method, url, body=None, token=None):
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            raw = resp.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        msg = raw[:300]
        try:
            j = json.loads(raw)
            msg = j.get("message") or json.dumps(j.get("errors") or j)[:300]
        except ValueError:
            pass
        raise ApiError(f"HTTP {e.code}: {msg}") from None
    except urllib.error.URLError as e:
        raise ApiError(f"connection failed: {e.reason}") from None
    return json.loads(raw) if raw else {}


# ---------------------------------------------------------------- comparison helpers

def norm(v):
    return None if v == "" else v


def same(a, b):
    a, b = norm(a), norm(b)
    if a is None and b is None:
        return True
    if isinstance(a, (list, tuple)) and isinstance(b, (list, tuple)):
        return len(a) == len(b) and all(same(x, y) for x, y in zip(a, b))
    if isinstance(a, bool) or isinstance(b, bool):
        return a == b
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return abs(a - b) < 1e-9
    if isinstance(a, str) and isinstance(b, str):
        return a.strip() == b.strip()
    return a == b


def fmt(v):
    return "None" if v is None else (json.dumps(v, ensure_ascii=False) if isinstance(v, (str, list, tuple)) else str(v))


def low(s):
    return (s or "").strip().lower()


# ---------------------------------------------------------------- the loader

class Loader:
    def __init__(self, base, token, dry_run, data_dir, only=None, out=print):
        self.base, self.token, self.dry, self.only = base.rstrip("/"), token, dry_run, only
        self.dir, self.out, self.counts = Path(data_dir), out, Counter()
        self.catalog = json.loads((self.dir / "catalog.json").read_text(encoding="utf-8"))
        # batch-2 parts: catalog-b2/*.json hold the same lists plus "mapping" (per item), merged in file order
        for part in sorted((self.dir / "catalog-b2").glob("*.json")):
            extra = json.loads(part.read_text(encoding="utf-8"))
            for key, val in extra.items():
                if key == "mapping":
                    self.catalog.setdefault("mapping", {}).update(val)
                elif isinstance(val, list):
                    have = {x.get("code") or x.get("name") for x in self.catalog.setdefault(key, [])}
                    new = [x for x in val if (x.get("code") or x.get("name")) not in have]
                    if key in ("solutions", "materialEntries"):
                        for x in new:        # master data other products may use: batch 2 creates it, never rewrites it
                            x["keepExisting"] = True
                    self.catalog[key] += new
        self.materials = [json.loads(p.read_text(encoding="utf-8"))
                          for p in sorted((self.dir / "materials").glob("*.json"))]
        if only:
            self.materials = [m for m in self.materials if m["itemCode"] == only]
        self._fake_id = 0

    # -- plumbing
    def api(self, method, path, body=None):
        if self.dry and method != "GET":
            raise AssertionError("dry-run must never write")
        r = http(method, self.base + path, body, self.token)
        return r.get("data") if isinstance(r, dict) and "data" in r else r

    def fake_id(self):
        self._fake_id -= 1
        return self._fake_id

    def say(self, label, text, kind=None):
        kind = kind or text.split(" ", 1)[0]
        self.counts[kind] += 1
        self.out(f"{label}: {text}")

    def wanted(self, obj):
        """--only keeps catalog objects whose usedBy names the item (objects without usedBy always stay)."""
        used = obj.get("usedBy")
        return not self.only or used is None or self.only in used

    def held(self, label, obj):
        if obj.get("heldBack"):
            self.say(label, f"skip (held back: {obj['heldBack']})")
            return True
        return False

    def upsert(self, label, existing, want, create, update):
        """existing: dict in the same key space as want, or None. create()/update(diff) do the writing and
        return the new record (None on dry-run for update)."""
        try:
            if existing is None:
                rec = create()
                self.say(label, "create")
                return rec
            diff = {k: (existing.get(k), v) for k, v in want.items() if not same(existing.get(k), v)}
            if not diff:
                self.say(label, "same")
                return None
            update(diff)
            self.say(label, "update {" + ", ".join(f"{k}: {fmt(o)} -> {fmt(n)}" for k, (o, n) in diff.items()) + "}")
        except ApiError as e:
            self.say(label, f"error ({e})", "error")
        except Unresolved as e:
            self.say(label, f"skip ({e})")
        return None

    # -- lookups
    def load_lookups(self):
        g = lambda p: self.api("GET", p) or []
        self.sections = {low(s["sectionName"]): s["sectionId"] for s in g("/org/sections")}
        self.entries = g("/masterdata/material-masters")
        self.solutions = g("/masterdata/solution-masters")
        self.tests = g("/masterdata/test-definitions")
        self.items = g("/items")
        self.hplc = self.icp = None

    def section_id(self, name):
        sid = self.sections.get(low(name or DEFAULT_SECTION))
        if sid is None:
            raise Unresolved(f"section '{name or DEFAULT_SECTION}' not found")
        return sid

    def find_entry(self, ref, code=None):
        if code:
            for e in self.entries:
                if low(e["code"]) == low(code):
                    return e
        ref = (ref or "").strip()
        m = re.search(r"\(([A-Za-z]{2,3}-\d+)\)\s*$", ref)
        if m:
            for e in self.entries:
                if low(e["code"]) == low(m.group(1)):
                    return e
            ref = ref[:m.start()].strip()
        for e in self.entries:
            if low(e["name"]) == low(ref):
                return e
        raise Unresolved(f"material master entry '{ref}' not found")

    def find_solution(self, name):
        for s in self.solutions:
            if low(s["name"]) == low(name):
                return s
        raise Unresolved(f"solution '{name}' not found")

    def find_test(self, code):
        for t in self.tests:
            if t["code"] == code:
                return t
        for t in self.tests:
            if t["code"].strip() == (code or "").strip():
                return t
        return None

    def find_method(self, kind, name):
        attr = "hplc" if kind == "hplc" else "icp"
        if getattr(self, attr) is None:
            setattr(self, attr, self.api("GET", f"/masterdata/{kind}-methods") or [])
        for m in getattr(self, attr):
            if low(m["name"]) == low(name):
                return m
        raise Unresolved(f"{kind.upper()} method '{name}' not found (methods are not created by this loader)")

    # ---------------------------------------------------------------- step 1: material master entries
    def step_entries(self):
        for e in self.catalog.get("materialEntries", []):
            label = f"entry {e.get('name')}"
            if self.held(label, e) or not self.wanted(e):
                continue
            category = e.get("category", "Reagent")
            found = None
            for x in self.entries:
                if low(x["name"]) == low(e["name"]):
                    found = x
            if found and e.get("keepExisting"):
                self.say(label, "same (exists, kept as is)")
                continue
            want = {k: e[k] for k in ("category", "grade", "source", "baseUnit", "workingConcentration", "solvent",
                                       "transitionRangeFrom", "transitionRangeTo", "colourChange", "indicatorUse")
                    if k in e}

            def create(e=e, category=category):
                code = e.get("code") or self.next_code(category)
                body = {"code": code, "name": e["name"], "category": category, "baseUnit": e.get("baseUnit", "Gram"),
                        "sectionId": self.section_id(e.get("section"))}
                body.update(want)
                rec = {"id": self.fake_id(), "code": code, "name": e["name"], "category": category}
                if not self.dry:
                    rec = self.api("POST", "/masterdata/material-masters", body)
                self.entries.append(rec)
                return rec

            def update(diff, found=found):
                body = {k: found.get(k) for k in ("code", "name", "category", "grade", "source", "baseUnit", "sectionId",
                                                  "workingConcentration", "solvent", "transitionRangeFrom",
                                                  "transitionRangeTo", "colourChange", "indicatorUse",
                                                  "materialCategoryId")}
                body.update({k: v for k, (_, v) in diff.items()})
                if not self.dry:
                    self.api("PUT", f"/masterdata/material-masters/{found['id']}", body)

            self.upsert(label, found, want, create, update)

    def next_code(self, category):
        prefix = ENTRY_PREFIX.get(category, "RG-")
        nums = [int(m.group(1)) for x in self.entries
                for m in [re.fullmatch(re.escape(prefix) + r"(\d+)", x["code"])] if m]
        return f"{prefix}{max(nums, default=0) + 1:03d}"

    # ---------------------------------------------------------------- step 2: solutions
    SOLUTION_KEYS = ("type", "shelfLifeValue", "shelfLifeUnit", "storageCondition", "finalVolumeMl", "instructions",
                     "phTarget", "phTolerance", "nominalStrength", "strengthUnit", "standardizationMode",
                     "equivalenceMgPerMl", "blankRequired", "replicateCount", "factorMin", "factorMax",
                     "maxRsdPercent", "validityDays")
    TITRANT_REQUIRED = ("nominalStrength", "strengthUnit", "standardizationMode", "replicateCount", "factorMin",
                        "factorMax", "maxRsdPercent", "validityDays")

    def step_solutions(self):
        sols = [s for s in self.catalog.get("solutions", []) if self.wanted(s)]
        by_name = {s["name"]: s for s in sols}

        def depth(s, seen=()):
            ref = s.get("referenceSolution")
            return 0 if not ref or ref not in by_name or ref in seen else 1 + depth(by_name[ref], seen + (s["name"],))

        for s in sorted(sols, key=depth):
            label = f"solution {s['name']}"
            if self.held(label, s):
                continue
            found = next((x for x in self.solutions if low(x["name"]) == low(s["name"])), None)
            if found and s.get("keepExisting"):
                self.say(label, "same (exists, kept as is)")
                continue
            if "components" not in s and "finalVolumeMl" not in s:       # reference-only record, no recipe
                if found:
                    self.say(label, "same")
                else:
                    self.say(label, "skip (not on the server and the catalog holds no recipe for it)")
                continue
            try:
                want = self.solution_want(s)
            except Unresolved as e:
                self.say(label, f"skip ({e})")
                continue
            existing = self.solution_view(found) if found else None

            def create(want=want, s=s):
                missing = [k for k in ("type", "shelfLifeValue", "shelfLifeUnit", "storageCondition", "finalVolumeMl",
                                       "instructions", "components") if k not in want]
                if want.get("type") == "Titrant":
                    missing += [k for k in self.TITRANT_REQUIRED if k not in want]
                if missing:
                    raise Unresolved("incomplete in the catalog: " + ", ".join(missing))
                rec = {"id": self.fake_id(), "name": s["name"]}
                if not self.dry:
                    rec = self.api("POST", "/masterdata/solution-masters", self.solution_body(s["name"], want, s))
                self.solutions.append(rec)
                return rec

            def update(diff, found=found, want=want, s=s):
                body = self.solution_body(found["name"], self.solution_view(found), s, include_section=False)
                body.update(self.solution_body(s["name"], {k: v for k, (_, v) in diff.items()}, s, partial=True))
                body["reason"] = REASON
                if not self.dry:
                    self.api("PUT", f"/masterdata/solution-masters/{found['id']}", body)

            self.upsert(label, existing, want, create, update)

    def solution_want(self, s):
        want = {k: s[k] for k in self.SOLUTION_KEYS if k in s}
        if s.get("type") == "Titrant":
            d = self.catalog.get("_meta", {}).get("proposedHouseDefaultsForTitrants", {})
            for k in ("replicateCount", "factorMin", "factorMax", "maxRsdPercent", "validityDays", "storageCondition"):
                if k not in want and k in d:
                    want[k] = d[k]
        if "shelfLifeValue" not in want:
            d = self.catalog.get("_meta", {}).get("proposedHouseDefaultsForTitrants", {})
            text = s.get("shelfLife") or (d.get("shelfLife") if s.get("type") == "Titrant" else None) or ""
            m = re.fullmatch(r"\s*(\d+)\s*(hour|day)s?\s*", text, re.I)
            if m:
                want["shelfLifeValue"], want["shelfLifeUnit"] = int(m.group(1)), m.group(2).lower().capitalize() + "s"
        for src, dst in (("standardEntry", "standardEntryId"), ("phAdjustingEntry", "phAdjustingEntryId")):
            if s.get(src):
                want[dst] = self.find_entry(s[src])["id"]
        if s.get("referenceSolution"):
            want["referenceSolutionId"] = self.find_solution(s["referenceSolution"])["id"]
        if "components" in s:
            want["components"] = [(self.find_entry(c.get("entry"), c.get("entryCode"))["id"], c["quantity"], c["unit"])
                                  for c in s["components"]]
        if "instructions" in want:
            want["instructions"] = want["instructions"].strip()
        return want

    def solution_view(self, r):
        v = {k: r.get(k) for k in self.SOLUTION_KEYS + ("standardEntryId", "phAdjustingEntryId", "referenceSolutionId")}
        v["components"] = [(c["materialMasterEntryId"], c["quantity"], c["unit"]) for c in r.get("components", [])]
        return v

    def solution_body(self, name, want, s, include_section=True, partial=False):
        body = {k: v for k, v in want.items() if k != "components"}
        if "components" in want:
            body["components"] = [{"materialMasterEntryId": i, "quantity": q, "unit": u} for i, q, u in want["components"]]
        if not partial:
            body["name"] = name
            body["components"] = body.get("components", [])
            if include_section:
                body["sectionId"] = self.section_id(s.get("section"))
        return body

    # ---------------------------------------------------------------- step 3: test definitions
    TEST_KEYS = ("displayName", "workflowType", "equationType", "physchemArea", "perContainer", "moistureRole",
                 "replicateCount", "requiresSystemSuitability", "methodAbbreviation", "evaluationBasis",
                 "conditionFields", "usesTare", "sstMaxRsdPercent", "sstMinResolution", "sstMaxTailingFactor",
                 "sstMinTheoreticalPlates", "dissolutionS1Offset", "dissolutionS2MinOffset", "dissolutionS3MinOffset",
                 "dissolutionS3MaxBelowS2Min", "disintegrationStage1Units", "disintegrationStage2Units",
                 "disintegrationMaxStage1Failures", "disintegrationMinPassTotal", "wvUnitCount", "wvTabletBand1MaxMg",
                 "wvTabletBand1Percent", "wvTabletBand2MaxMg", "wvTabletBand2Percent", "wvTabletBand3Percent",
                 "wvTabletMaxOutside", "wvCapsuleInnerPercent", "wvCapsuleOuterPercent", "wvCapsuleS1MaxOutside",
                 "wvCapsuleS1MaxForRetest", "wvCapsuleS2ExtraUnits", "wvCapsuleS2MaxOutside", "titrationType",
                 "titrationNonAqueous", "titrationMode", "titrationCalculation", "titrationEquivalencyFactor",
                 "titrationBlankRequired", "titrationExcessVolumeMl", "titrationMaxRsdPercent", "titrationEndpoint",
                 "titrationTempCorrection", "titrationExpansionCoefficient", "uvCalculationMode",
                 "uvSpecificAbsorbance", "uvTotalVolumeMl", "uvStandardVolumeMl")
    TEST_REFS = {"titrantSolution": ("titrantSolutionMasterId", "solution"),
                 "titrationExcessSolution": ("titrationExcessSolutionMasterId", "solution"),
                 "titrationIndicatorEntry": ("titrationIndicatorEntryId", "entry"),
                 "titrationStandardEntry": ("titrationStandardEntryId", "entry"),
                 "uvStandardEntry": ("uvStandardEntryId", "entry"),
                 "hplcMethod": ("hplcMethodId", "hplc"), "icpMethod": ("icpMethodId", "icp")}

    def step_tests(self):
        for t in self.catalog.get("testDefinitions", []):
            label = f"test {t['code'].strip()}"
            if self.held(label, t) or not self.wanted(t):
                continue
            try:
                want = {k: t[k] for k in self.TEST_KEYS if k in t}
                want["sectionId"] = self.section_id(t.get("section"))
                for src, (dst, kind) in self.TEST_REFS.items():
                    if t.get(src):
                        want[dst] = (self.find_solution(t[src]) if kind == "solution" else
                                     self.find_entry(t[src]) if kind == "entry" else
                                     self.find_method(kind, t[src]))["id"]
            except Unresolved as e:
                self.say(label, f"skip ({e})")
                continue
            found = self.find_test(t["code"])

            def create(t=t, want=want):
                body = dict(want, code=t["code"])
                rec = dict(body, id=self.fake_id())
                if not self.dry:
                    rec = self.api("POST", "/masterdata/test-definitions", body)
                self.tests.append(rec)
                return rec

            def update(diff, found=found, t=t):
                body = {"code": found["code"], "displayName": t.get("displayName", found["displayName"]),
                        "changeReason": REASON}
                body.update({k: v for k, (_, v) in diff.items()})
                if not self.dry:
                    self.api("PUT", f"/masterdata/test-definitions/{found['id']}", body)

            self.upsert(label, found, want, create, update)

    # ---------------------------------------------------------------- per-item rows
    def resolve_rows(self, m):
        """Yield (grade, index, row, test_code, skip_reason) for every spec row of a material."""
        mapping = self.catalog.get("mapping", {}).get(m["itemCode"], {})
        cat = {t["code"]: t for t in self.catalog.get("testDefinitions", [])}
        for g in m.get("grades", []):
            for i, row in enumerate(g["rows"]):
                skip = None
                code = mapping.get(row["testCode"])
                if g.get("heldBack"):
                    skip = f"grade held back: {g['heldBack']}"
                elif row.get("heldBack"):
                    skip = f"held back: {row['heldBack']}"
                elif code is None:
                    skip = f"no catalog mapping for {row['testCode']}"
                elif cat.get(code, {}).get("heldBack"):
                    skip = f"test {code.strip()} held back: {cat[code]['heldBack']}"
                elif self.find_test(code) is None:
                    skip = f"test {code.strip()} not found"
                else:
                    code = self.find_test(code)["code"]
                yield g["grade"], i, row, code, skip

    # ---------------------------------------------------------------- step 4: items
    def step_items(self):
        for m in self.materials:
            code = m["itemCode"]
            label = f"item {code}"
            item = next((x for x in self.items if x["code"] == code), None)
            if item is None:
                self.say(label, "skip (item not found)")
                continue
            if item.get("category") not in ("RawMaterial", "PackagingMaterial"):
                self.say(label, f"skip (item category is {item.get('category')})")
                continue
            q = m.get("minQuantity") or {}
            want = {}
            if q and not q.get("heldBack"):
                if q.get("routine") is not None:
                    want["minSampleQuantity"] = q["routine"]
                if q.get("retest") is not None:
                    want["minRetestSampleQuantity"] = q["retest"]
                if want:
                    want["minSampleQuantityUnit"] = q["unit"]
            needed = {}
            for _, _, _, tc, skip in self.resolve_rows(m):
                if not skip:
                    needed[tc] = self.find_test(tc)["displayName"]
            for na in m.get("notApplicable", []):
                tc = self.catalog.get("mapping", {}).get(code, {}).get(na["testCode"])
                if tc and not na.get("heldBack") and self.find_test(tc):
                    needed[self.find_test(tc)["code"]] = self.find_test(tc)["displayName"]
            have = {t["testCode"] for t in item.get("assignedTests", [])}
            add = sorted(set(needed) - have)
            existing = {k: item.get(k) for k in want}
            if add:
                want["assignedTests"] = sorted(have | set(needed))
                existing["assignedTests"] = sorted(have)

            def update(diff, item=item, needed=needed):
                if self.dry:
                    return
                tests = [{"testCode": t["testCode"], "displayName": t["displayName"]} for t in item.get("assignedTests", [])]
                tests += [{"testCode": c, "displayName": needed[c]} for c in sorted(needed)
                          if c not in {t["testCode"] for t in tests}]
                body = {"name": item["name"], "code": item["code"], "category": item["category"],
                        "sopNumber": item.get("sopNumber"), "assignedTests": tests,
                        "minSampleQuantity": item.get("minSampleQuantity"),
                        "minRetestSampleQuantity": item.get("minRetestSampleQuantity"),
                        "minSampleQuantityUnit": item.get("minSampleQuantityUnit")}
                body.update({k: v for k, (_, v) in diff.items() if k != "assignedTests"})
                self.api("PUT", f"/items/{item['id']}", body)
                item["assignedTests"] = tests
                item.update({k: v for k, (_, v) in diff.items() if k != "assignedTests"})

            # an item with nothing to change is "same", not skipped
            self.upsert(label, existing, want, lambda: None, update)

    # ---------------------------------------------------------------- step 5: specifications
    def spec_want(self, row, idx):
        lt = row["limitType"]
        want = {"displayOrder": idx + 1, "limitType": lt, "referenceStandard": row.get("referenceStandard"),
                "lowerLimit": row.get("lower"), "upperLimit": row.get("upper"), "unit": row.get("unit") or "",
                "expectedResultText": row.get("expectedResultText"), "expectedState": row.get("expectedState"),
                "resultBasis": row.get("resultBasis")}
        if row.get("lowerInclusive") is not None:
            want["lowerInclusive"] = row["lowerInclusive"]
        if lt == "CountTiered" and row.get("upper") is not None:
            u = row["upper"]
            want["specLimit"] = str(int(u)) if float(u).is_integer() else str(u)
        return want

    def step_specs(self):
        for m in self.materials:
            code = m["itemCode"]
            item = next((x for x in self.items if x["code"] == code), None)
            rows = list(self.resolve_rows(m))
            cat = {t["code"]: t for t in self.catalog.get("testDefinitions", [])}
            # the API wants the water / loss-on-drying test saved before a dried or anhydrous assay
            rows.sort(key=lambda r: 0 if cat.get(r[3] if len(r) > 3 else "", {}).get("moistureRole") else 1)
            if item is None or item.get("category") not in ("RawMaterial", "PackagingMaterial"):
                self.say(f"specs {code}", f"skip (item not usable; {len(rows)} rows not loaded)")
                continue
            existing = self.api("GET", f"/masterdata/specifications?itemId={item['id']}") or []
            for grade, idx, row, tc, skip in rows:
                label = f"spec {code}/{grade}/{row['parameterName']}"
                if skip:
                    self.say(label, f"skip ({skip})")
                    continue
                want = self.spec_want(row, idx)
                found = next((s for s in existing if s["testCode"] == tc and low(s["parameterName"]) == low(row["parameterName"])
                              and low(s.get("grade")) == low(grade)), None)

                def create(want=want, tc=tc, row=row, grade=grade, item=item):
                    body = dict(want, itemId=item["id"], testCode=tc, parameterName=row["parameterName"], grade=grade)
                    if not self.dry:
                        self.api("POST", "/masterdata/specifications", body)

                def update(diff, found=found, want=want, tc=tc, row=row, grade=grade):
                    body = dict(want, testCode=tc, parameterName=row["parameterName"], grade=grade)
                    if not self.dry:
                        self.api("PUT", f"/masterdata/specifications/{found['id']}", body)

                self.upsert(label, found, want, create, update)

    # ---------------------------------------------------------------- step 6: supplier attributes
    def step_attributes(self):
        for m in self.materials:
            code = m["itemCode"]
            item = next((x for x in self.items if x["code"] == code), None)
            attrs = m.get("supplierAttributes", [])
            if not attrs:
                continue
            if item is None:
                self.say(f"attributes {code}", f"skip (item not found; {len(attrs)} attributes not loaded)")
                continue
            existing = self.api("GET", f"/items/{item['id']}/supplier-attributes") or []
            for i, a in enumerate(attrs):
                label = f"attribute {code}/{a.get('grade') or '-'}/{a['name']}"
                if self.held(label, a):
                    continue
                want = {"statedValue": a["statedValue"], "sortOrder": i}
                found = next((x for x in existing if low(x.get("grade")) == low(a.get("grade")) and low(x["name"]) == low(a["name"])), None)
                body = {"grade": a.get("grade"), "name": a["name"], "statedValue": a["statedValue"], "sortOrder": i}

                def create(body=body, item=item):
                    if not self.dry:
                        self.api("POST", f"/items/{item['id']}/supplier-attributes", dict(body, itemId=item["id"]))

                def update(diff, found=found, body=body, item=item):
                    if not self.dry:
                        self.api("PUT", f"/supplier-attributes/{found['id']}", dict(body, itemId=item["id"]))

                self.upsert(label, found, want, create, update)

    # ---------------------------------------------------------------- step 7: not-applicable drafts
    def step_not_applicable(self):
        for m in self.materials:
            code = m["itemCode"]
            nas = m.get("notApplicable", [])
            if not nas:
                continue
            item = next((x for x in self.items if x["code"] == code), None)
            if item is None:
                self.say(f"not-applicable {code}", f"skip (item not found; {len(nas)} records not loaded)")
                continue
            existing = self.api("GET", f"/items/{item['id']}/not-applicable-tests") or []
            for n in nas:
                label = f"not-applicable {code}/{n['testCode']}/{n.get('grade') or '-'}"
                if self.held(label, n):
                    continue
                tc = self.catalog.get("mapping", {}).get(code, {}).get(n["testCode"])
                test = self.find_test(tc) if tc else None
                if test is None:
                    self.say(label, f"skip (no catalog test for {n['testCode']})")
                    continue
                found = next((x for x in existing if x["testCode"] == test["code"] and low(x.get("grade")) == low(n.get("grade"))
                              and x.get("status") != "Withdrawn"), None)
                body = {"grade": n.get("grade"), "testCode": test["code"], "justification": n["justification"],
                        "criticality": n["criticality"], "immediateAction": n["immediateAction"],
                        "longTermAction": n.get("longTermAction") or "", "reference": n.get("reference")}

                def create(body=body, item=item):
                    if not self.dry:
                        self.api("POST", f"/items/{item['id']}/not-applicable-tests", dict(body, itemId=item["id"]))

                # no update endpoint exists: a record that is already there is left alone, never re-signed
                self.upsert(label, found, {}, create, lambda diff: None)

    # ---------------------------------------------------------------- run
    def run(self):
        self.load_lookups()
        for title, step in (("material master entries", self.step_entries), ("solutions", self.step_solutions),
                            ("test definitions", self.step_tests), ("items", self.step_items),
                            ("specifications", self.step_specs), ("supplier attributes", self.step_attributes),
                            ("not-applicable drafts", self.step_not_applicable)):
            self.out(f"== {title} ==")
            step()
        c = self.counts
        self.out("totals: " + ", ".join(f"{k} {c[k]}" for k in ("create", "update", "same", "skip", "error"))
                 + (" (dry-run, nothing written)" if self.dry else ""))
        return c


# ---------------------------------------------------------------- CLI

def login(base, user, password):
    r = http("POST", base.rstrip("/") + "/auth/login", {"username": user, "password": password})
    token = (r.get("data") or {}).get("token") if isinstance(r, dict) else None
    if not token:
        raise ApiError("login failed: " + str(r.get("message") if isinstance(r, dict) else r))
    return token


def main(argv=None, out=print):
    p = argparse.ArgumentParser(description="Idempotent RM data setup loader (Plan 6).")
    p.add_argument("--base-url", required=True, help="API root, e.g. http://localhost:5000/api")
    p.add_argument("--user", default=os.environ.get("RM_LOADER_USER"), help="login name (or RM_LOADER_USER)")
    mode = p.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true", help="read only; print what would change")
    mode.add_argument("--apply", action="store_true", help="write the changes")
    p.add_argument("--only", help="load one item code (e.g. STM-RM-097)")
    p.add_argument("--i-approve-prod", action="store_true", help="required with --apply on a non-local URL")
    p.add_argument("--data-dir", default=str(HERE), help=argparse.SUPPRESS)
    a = p.parse_args(argv)

    host = urlparse(a.base_url).hostname or ""
    if a.apply and host not in LOCAL_HOSTS and not a.i_approve_prod:
        print(f"error: {host} is not local; --apply needs --i-approve-prod", file=sys.stderr)
        return 2
    if not a.user:
        print("error: --user (or RM_LOADER_USER) is required", file=sys.stderr)
        return 2
    password = os.environ.get("RM_LOADER_PASSWORD") or getpass.getpass(f"Password for {a.user}: ")
    try:
        token = login(a.base_url, a.user, password)
        counts = Loader(a.base_url, token, not a.apply, a.data_dir, a.only, out).run()
    except ApiError as e:
        print(f"error: {e}", file=sys.stderr)
        return 1
    return 1 if counts["error"] else 0


if __name__ == "__main__":
    sys.exit(main())
