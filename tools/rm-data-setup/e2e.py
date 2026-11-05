"""End-to-end check of the 14 loaded raw materials on LOCAL MicroLIMS (Plan 6, Task 7).

For every material and grade: receive one sample, compare the ordered tests with the loaded spec rows,
record a CONFORMING result for every physicochemical order through the same endpoints the screens use,
and (for samples with no micro orders) review, approve and read the certificate. Micro orders are only
checked, never run. Python 3 standard library only. Local only: refuses any URL that is not localhost.

  py -I e2e.py                       # everything
  py -I e2e.py --only STM-RM-097     # one material
  py -I e2e.py --report-only         # rewrite reports/e2e-local.md from the saved state

Credentials come from frontend/.env.e2e.local (never printed). State (sample ids, titrant preparations)
is kept in E:/MicroLIMS/rls-tmp/e2e/state.json so a rerun continues instead of receiving again.
"""
import argparse
import datetime
import json
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path
from urllib.parse import urlparse

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
ENV_FILE = REPO / "frontend" / ".env.e2e.local"
STATE_FILE = Path("E:/MicroLIMS/rls-tmp/e2e/state.json")
REPORT = HERE / "reports" / "e2e-local.md"
BASE = "http://localhost:5000/api"

ENV = dict(l.strip().split("=", 1) for l in ENV_FILE.read_text().splitlines() if "=" in l and not l.startswith("#"))
TOK = {}
STATE = {"samples": {}, "titrants": {}, "results": {}, "problems": []}


# ---------------------------------------------------------------- API

def api(method, path, body=None, who="HEAD"):
    """Returns (status, data, message). data is the unwrapped 'data' of the ApiResponse."""
    if who not in TOK:
        s, d, m = raw("POST", "/auth/login", {"username": ENV[f"E2E_{who}_USER"], "password": ENV[f"E2E_{who}_PASSWORD"]})
        if s >= 300:
            raise SystemExit(f"login failed for {who}: {m}")
        TOK[who] = d["token"]
    return raw(method, path, body, TOK[who])


def raw(method, path, body=None, tok=None):
    req = urllib.request.Request(BASE + path, method=method, data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + tok} if tok else {})})
    try:
        with urllib.request.urlopen(req, timeout=180) as r:
            j = json.loads(r.read() or "null")
            return r.status, (j.get("data", j) if isinstance(j, dict) else j), None
    except urllib.error.HTTPError as e:
        text = e.read().decode("utf-8", "replace")
        msg = text[:400]
        try:
            j = json.loads(text)
            msg = j.get("message") or json.dumps(j.get("errors") or j)[:400]
        except ValueError:
            pass
        return e.code, None, msg


def pw(who):
    return ENV[f"E2E_{who}_PASSWORD"]


def now_iso(minutes_ago=2):
    return (datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(minutes=minutes_ago)).strftime("%Y-%m-%dT%H:%M:%SZ")


def save_state():
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    STATE_FILE.write_text(json.dumps(STATE, indent=1), encoding="utf-8")


def problem(kind, where, text):
    """kind: DATA or SYSTEM (or SETUP for a workflow that could not be set up)."""
    entry = {"kind": kind, "where": where, "text": text}
    if entry not in STATE["problems"]:
        STATE["problems"].append(entry)
    print(f"  !! {kind}: {where}: {text}")


# ---------------------------------------------------------------- catalog / expected tests

def load_data():
    cat = json.loads((HERE / "catalog.json").read_text(encoding="utf-8"))
    mats = [json.loads(p.read_text(encoding="utf-8")) for p in sorted((HERE / "materials").glob("*.json"))]
    return cat, mats


def low(s):
    return (s or "").strip().lower()


def expected_from_files(cat, mat, grade, approved_na):
    """Tests the grade's file rows call for (held-back rows and tests dropped), minus approved not-applicable tests."""
    mapping = cat["mapping"][mat["itemCode"]]
    held_tests = {t["code"] for t in cat["testDefinitions"] if t.get("heldBack")}
    out = set()
    for g in mat["grades"]:
        if low(g["grade"]) != low(grade) or g.get("heldBack"):
            continue
        for row in g["rows"]:
            code = mapping.get(row["testCode"])
            if row.get("heldBack") or code is None or code in held_tests:
                continue
            out.add(code)
    return out - approved_na


# ---------------------------------------------------------------- number helpers

def r(x, n=4):
    return round(float(x), n)


def target_of(spec):
    """A value that conforms to the spec's limit, away from the edges."""
    lt, lo, up = spec["limitType"], spec.get("lowerLimit"), spec.get("upperLimit")
    if lt == "Range":
        return (lo + up) / 2
    if lt == "NotMoreThan":
        return up * 0.5
    if lt == "NotLessThan":
        return lo * 1.05 if lo else 1.0
    raise ValueError(f"no numeric target for limit type {lt}")


# ---------------------------------------------------------------- titrants (solution preparation + standardization)

_MASTERS = {}


def master_of(master_id):
    if not _MASTERS:
        _MASTERS.update({m["id"]: m for m in api("GET", "/masterdata/solution-masters", who="ANALYST")[1]})
    return _MASTERS[master_id]


def titrant_state(master_id):
    """The detail of a prepared, standardized, usable preparation of the titrant master, or None."""
    name = master_of(master_id)["name"]
    s, lst, _ = api("GET", "/solution-preparations?status=Prepared&type=Titrant", who="ANALYST")
    for p in lst or []:
        if p["solutionMasterName"] != name or p.get("effectiveStatus") != "Prepared":
            continue
        s, d, _ = api("GET", f"/solution-preparations/{p['id']}", who="ANALYST")
        cf = d.get("currentFactor") or {}
        if cf.get("factor") and cf.get("state") in ("Valid", "BeforeEachUse"):
            return d
    return None


def ensure_titrant(master_id):
    p = titrant_state(master_id)
    if p:
        return p
    master = master_of(master_id)
    print(f"  titrant: preparing {master['name']}")
    # reuse an unfinished preparation of mine, else start one
    s, mine, _ = api("GET", "/solution-preparations?status=InProgress&type=Titrant", who="ANALYST")
    prep = next((x for x in mine or [] if x["solutionMasterName"] == master["name"]), None)
    if prep is None:
        s, prep, m = api("POST", "/solution-preparations", {"solutionMasterId": master_id, "hplcMethodId": None}, who="ANALYST")
        if s >= 300:
            problem("SYSTEM", f"titrant {master['name']}", f"start preparation refused: {m}")
            return None
    pid = prep["id"]
    s, prep, _ = api("GET", f"/solution-preparations/{pid}", who="ANALYST")
    comps = []
    for c in prep["components"]:
        if c["kind"] != "Material":
            comps.append({"componentId": c["id"], "materialId": None, "quantityUsed": None})
            continue
        s, lots, m = api("GET", f"/solution-preparations/{pid}/components/{c['id']}/lots", who="ANALYST")
        usable = [l for l in lots or [] if l["usable"]]
        if not usable:
            problem("SETUP", f"titrant {master['name']}", f"no usable stock lot of {c['entryName']}")
            return None
        def need(l):   # the quantity in the lot's own unit (a Gram recipe of a liquid is read as mL, density about 1)
            return c["recipeQuantity"] / 1000 if l["unit"] == "Liter" and c["recipeUnit"] in ("Milliliter", "Gram") else c["recipeQuantity"]
        lot = next((l for l in usable if l["unit"] == c["recipeUnit"] and l["quantityRemaining"] >= need(l)), None)             or next((l for l in usable if l["quantityRemaining"] >= need(l)), None)
        if lot is None:
            problem("SETUP", f"titrant {master['name']}", f"no stock lot of {c['entryName']} holds {c['recipeQuantity']} {c['recipeUnit']}")
            return None
        q = need(lot)
        comps.append({"componentId": c["id"], "materialId": lot["materialId"], "quantityUsed": r(q, 6)})
    s, _, m = api("PUT", f"/solution-preparations/{pid}", {"components": comps, "finalVolumeMl": master["finalVolumeMl"], "measuredPh": None}, who="ANALYST")
    if s >= 300:
        problem("SYSTEM", f"titrant {master['name']}", f"save preparation refused: {m}")
        return None
    s, _, m = api("POST", f"/solution-preparations/{pid}/complete", {"password": pw("ANALYST"), "comment": "E2E Plan 6"}, who="ANALYST")
    if s >= 300:
        problem("SYSTEM", f"titrant {master['name']}", f"complete preparation refused: {m}")
        return None
    if not standardize(pid, master):
        return None
    return titrant_state(master_id)


def ensure_primary_lot(entry_id, entry_name):
    """A usable primary standard lot (with purity) of the entry; receives one through Materials Stock if none exists."""
    s, lots, _ = api("GET", "/inventory/materials", who="ANALYST")
    for l in lots or []:
        if l.get("materialMasterEntryId") == entry_id and l.get("materialType") == "PrimaryStandard" and l.get("purity") and l["quantityRemaining"] > 20:
            return l
    body = {"materialType": "PrimaryStandard", "materialName": entry_name, "manufacturerName": "E2E supplier", "batchNumber": f"E2E-PS-{entry_id}",
            "receivingDate": now_iso(), "expiryDate": "2029-12-31T00:00:00Z", "code": None, "location": "Standards shelf", "quantityReceived": 100,
            "unit": "Gram", "minimumStockLevel": None, "atccNumber": None, "organismId": None, "sectionId": 2, "purity": 99.9,
            "materialMasterEntryId": entry_id, "storageCondition": "Room temperature"}
    s, d, m = api("POST", "/inventory/materials", body, who="ANALYST")
    if s >= 300:
        problem("SYSTEM", f"primary standard {entry_name}", f"receiving the lot in Materials Stock refused: {m}")
        return None
    s, lots, _ = api("GET", "/inventory/materials", who="ANALYST")
    return next((l for l in lots if l.get("materialMasterEntryId") == entry_id and l.get("batchNumber") == body["batchNumber"]), None)


def standardize(pid, master):
    snap_mode = master.get("standardizationMode")
    n = master["nominalStrength"]
    blank_req = master.get("blankRequired")
    factors = [1.001, 1.000, 0.999]
    reps = []
    if snap_mode == "PrimaryStandard":
        s, lots, _ = api("GET", f"/solution-preparations/{pid}/standardizations/standard-lots", who="ANALYST")
        lot = next((l for l in lots or [] if l["usable"] and l.get("purity")), None)
        if lot is None:
            entry = next((e for e in api("GET", "/masterdata/material-masters", who="ANALYST")[1] if e["id"] == master["standardEntryId"]), None)
            made = ensure_primary_lot(master["standardEntryId"], entry["name"] if entry else "standard")
            if made is None:
                return False
            s, lots, _ = api("GET", f"/solution-preparations/{pid}/standardizations/standard-lots", who="ANALYST")
            lot = next((l for l in lots or [] if l["usable"] and l.get("purity")), None)
            if lot is None:
                problem("SYSTEM", f"titrant {master['name']}", "the standard lot just received is not offered as a usable primary standard")
                return False
        eq = master["equivalenceMgPerMl"]
        for f in factors:
            w = round(25 * eq * f / (lot["purity"] / 100), 0)
            blank = 0.05 if blank_req else None
            v = w * lot["purity"] / 100 / (eq * f) + (blank or 0)
            reps.append({"standardMaterialId": lot["materialId"], "standardWeightMg": w, "referencePreparationId": None, "referenceVolumeMl": None,
                         "titrantVolumeMl": r(v, 2), "blankMl": blank})
    else:
        ref_prep = ensure_titrant(master["referenceSolutionId"])
        if ref_prep is None:
            problem("SETUP", f"titrant {master['name']}", "its reference solution could not be prepared and standardized")
            return False
        rf = ref_prep["currentFactor"]["factor"]
        rn = json.loads(ref_prep["recipeSnapshotJson"])["nominalStrength"]
        for f in factors:
            blank = 0.05 if blank_req else None
            v = 25 * rf * rn / (n * f) + (blank or 0)
            reps.append({"standardMaterialId": None, "standardWeightMg": None, "referencePreparationId": ref_prep["id"], "referenceVolumeMl": 25,
                         "titrantVolumeMl": r(v, 2), "blankMl": blank})
    s, d, m = api("POST", f"/solution-preparations/{pid}/standardizations", {"replicates": reps, "password": pw("ANALYST"), "comment": "E2E Plan 6", "temperatureC": None}, who="ANALYST")
    if s >= 300:
        problem("SYSTEM", f"titrant {master['name']}", f"standardization refused: {m}")
        return False
    print(f"    standardized: factor {d.get('meanFactor')} passed {d.get('passed')}")
    if not d.get("passed"):
        problem("SYSTEM", f"titrant {master['name']}", f"standardization failed its own limits: {d.get('failureReasons')}")
        return False
    return True


# ---------------------------------------------------------------- recording

class Ctx:
    """Everything the handlers need about one sample."""

    def __init__(self, key, sample_id, item, grade, specs, defs, equipment):
        self.key, self.sample_id, self.item, self.grade = key, sample_id, item, grade
        self.specs, self.defs, self.equipment = specs, defs, equipment
        self.lod = None          # loss on drying percent recorded on this sample (for dried-basis assays)

    def specs_for(self, code):
        rows = [s for s in self.specs if s["testCode"] == code and low(s.get("grade")) == low(self.grade)]
        return sorted(rows, key=lambda s: (s["displayOrder"], s["id"]))


def post_result(oid, kind, body, who="ANALYST"):
    body.update({"password": pw(who), "comment": "E2E Plan 6"})
    s, d, m = api("POST", f"/test-workflow/{oid}/{kind}", body, who=who)
    return s, d, m


def rec_qualitative(ctx, oid, code, d):
    specs = ctx.specs_for(code)
    per = d.get("perContainer")
    params = []
    for sp in specs:
        text = (sp.get("expectedResultText") or sp.get("specLimit") or "complies")[:480]
        if per:
            params.append({"specificationId": sp["id"], "conforms": True, "observation": None,
                           "containers": [{"index": i, "conforms": True, "observation": text} for i in (1, 2, 3)]})
        else:
            params.append({"specificationId": sp["id"], "conforms": True, "observation": text})
    body = {"analysedAt": now_iso(), "equipmentId": None, "parameters": params}
    if per:
        body["containerCount"] = 3
    return post_result(oid, "record-qualitative-result", body)


def rec_measurement(ctx, oid, code, d):
    reps = d.get("replicateCount") or 1
    params = [{"specificationId": sp["id"], "readings": [r(target_of(sp), 3)] * reps} for sp in ctx.specs_for(code)]
    return post_result(oid, "record-measurement-result", {"analysedAt": now_iso(), "equipmentId": None, "parameters": params})


def rec_gravimetric(ctx, oid, code, d):
    reps_n = d.get("replicateCount") or 1
    loss = d["equationType"] == "GravimetricLoss"
    tare = bool(d.get("usesTare"))
    params = []
    for sp in ctx.specs_for(code):
        pct = target_of(sp) if sp["limitType"] != "NotMoreThan" else sp["upperLimit"] * 0.5
        container = 10.0 if tare else None
        initial = 11.0 if tare else 1.0
        w1 = 1.0
        w2 = w1 * (1 - pct / 100) if loss else w1 * pct / 100
        final = (10.0 if tare else 0.0) + w2
        params.append({"specificationId": sp["id"], "replicates": [{"container": container, "initial": initial, "final": r(final, 5)}] * reps_n})
        if code == "RM-LOD":
            ctx.lod = pct
            STATE["results"][ctx.key]["lod"] = pct
    return post_result(oid, "record-gravimetric-result", {"analysedAt": now_iso(), "equipmentId": None, "conditions": {}, "parameters": params})


def rec_disintegration(ctx, oid, code, d):
    sp = ctx.specs_for(code)[0]
    lim = sp["upperLimit"]
    units = [r(lim * f, 1) for f in (0.5, 0.55, 0.6, 0.65, 0.5, 0.7)]
    return post_result(oid, "record-disintegration-result", {"analysedAt": now_iso(), "equipmentId": None, "conditions": None, "unitMinutes": units})


def rec_uv(ctx, oid, code, d):
    mode = d["uvCalculationMode"]
    vol, a1 = d.get("uvTotalVolumeMl"), d.get("uvSpecificAbsorbance")
    reps_n = d.get("replicateCount") or 1
    params = []
    for sp in ctx.specs_for(code):
        if mode == "AbsorbanceLimit":
            params.append({"specificationId": sp["id"], "wavelengthNm": 700, "referenceAbsorbance": 0.2,
                           "replicates": [{"weightG": None, "absorbance": 0.1}] * reps_n})
        elif mode == "SpecificAbsorbanceValue":
            m = re.search(r"at (\d+)-(\d+) nm", sp["parameterName"])
            wl = (int(m.group(1)) + int(m.group(2))) / 2 if m else 290
            w = 2.5      # solution S: 2.5 g in 50 mL, then 1:50 and 1:100 -> total volume 250000 mL
            params.append({"specificationId": sp["id"], "wavelengthNm": wl, "referenceAbsorbance": None,
                           "replicates": [{"weightG": w, "absorbance": r(target_of(sp) * w * 100 / vol, 4)}] * reps_n})
        elif mode == "SpecificAbsorbanceAssay":
            w = 0.1
            asis = target_of(sp)
            if sp.get("resultBasis") == "PercentDriedBasis":
                asis = target_of(sp) * (100 - (ctx.lod or 0)) / 100
            params.append({"specificationId": sp["id"], "wavelengthNm": 444, "referenceAbsorbance": None,
                           "replicates": [{"weightG": w, "absorbance": r(asis * a1 * w / vol, 4)}] * reps_n})
        else:
            raise ValueError(f"UV mode {mode} not handled by e2e.py")
    body = {"analysedAt": now_iso(), "equipmentId": ctx.equipment["uv"], "standardMaterialId": None, "standardWeightG": None, "standardAbsorbance": None, "parameters": params}
    return post_result(oid, "record-uv-result", body)


def rec_titration(ctx, oid, code, d):
    s, tc, m = api("GET", f"/test-workflow/{oid}/titration-context", who="ANALYST")
    if s >= 300:
        return s, None, f"titration-context refused: {m}"
    for need in ("titrantSolutionMasterId", "titrationExcessSolutionMasterId"):
        if d.get(need) and not ensure_titrant(d[need]):
            return 0, None, f"could not set up titrant for master {d[need]}"
    s, tc, m = api("GET", f"/test-workflow/{oid}/titration-context", who="ANALYST")
    pick = lambda lst: next((p for p in lst if p["usable"]), None)
    tp, ep = pick(tc["titrantPreparations"]), pick(tc["excessPreparations"])
    if tp is None:
        return 0, None, f"no usable titrant preparation offered: {[p.get('blockReason') for p in tc['titrantPreparations']]}"
    n, f, feq = tc["titrant"]["nominalStrength"], tp["factor"], tc["equivalencyFactor"]
    specs = tc["specifications"]
    spec_rows = ctx.specs_for(code)
    base = spec_rows[0]
    pct = target_of(base)
    basis = base.get("resultBasis")
    asis_pct = pct * (100 - (ctx.lod or 0)) / 100 if basis == "PercentDriedBasis" else pct
    blank = 0.05 if tc["blankRequired"] else None
    residual = tc["mode"] == "Residual"
    if residual:
        if ep is None:
            return 0, None, "no usable excess titrant preparation offered"
        en, ef, evol = tc["excessTitrant"]["nominalStrength"], ep["factor"], tc["excessVolumeMl"]
        w = round(0.85 * evol * en * ef * feq / (asis_pct / 100), 1)
        mg = w * asis_pct / 100
        vol = (evol * en * ef - mg / feq) / (n * f)
    else:
        mg_target = 25 * n * f * feq
        w = round(mg_target / (asis_pct / 100), 1)
        mg = w * asis_pct / 100
        vol = mg / (n * f * feq) + (blank or 0)
    body = {"analysedAt": now_iso(), "equipmentId": None, "titrantPreparationId": tp["preparationId"],
            "excessPreparationId": ep["preparationId"] if residual else None, "blankVolumeMl": blank, "titrationTemperatureC": None,
            "lossOnDryingPercent": None, "averageUnitWeightMg": None, "standards": None, "specificationIds": [x["specificationId"] for x in specs],
            "replicates": [{"sampleWeightMg": w, "titrantVolumeMl": r(vol, 2)}] * tc["replicateCount"]}
    return post_result(oid, "record-titration-result", body)


HANDLERS = {"Qualitative": rec_qualitative, "Measurement": rec_measurement, "Gravimetric": rec_gravimetric,
            "Disintegration": rec_disintegration, "UvSpectrophotometry": rec_uv, "Titration": rec_titration}


# ---------------------------------------------------------------- one sample

def lod_from_state(res):
    return res.get("lod")


def receive(item, grade, qty_text, key):
    sid = STATE["samples"].get(key)
    if sid:
        return sid
    s, labs, m = api("GET", f"/samples/receipt-labs?itemId={item['id']}")
    targets = [l["sectionId"] for l in labs or [] if l["testCount"] > 0]
    body = {"itemId": item["id"], "causeOfTestingId": 1, "sampleQuantity": qty_text, "sampledBy": "QC E2E",
            "batchNumber": f"RM-E2E-{item['code']}-{grade}", "controlNumber": f"CN-E2E-{item['code']}-{grade}", "mfgDate": None, "expDate": None,
            "productionStage": None, "targetSectionIds": targets, "grade": grade}
    s, d, m = api("POST", "/samples", body)
    if s >= 300:
        problem("SYSTEM", f"{key} receive", f"receiving refused: {m}")
        return None
    STATE["samples"][key] = d["sampleId"]
    save_state()
    return d["sampleId"]


def run_sample(cat, mat, grade, items_by_code, defs, equipment, na_by_item):
    code = mat["itemCode"]
    key = f"{code}|{grade}"
    item = items_by_code[code]
    print(f"== {key} ({item['name']})")
    mq = mat.get("minQuantity") or {}
    qty = f"{mq.get('routine') or 10} {mq.get('unit') or 'g'}"
    sid = receive(item, grade, qty, key)
    res = STATE["results"].setdefault(key, {"item": item["name"], "grade": grade})
    if sid is None:
        res["received"] = False
        return
    res["sampleId"] = sid
    s, summ, m = api("GET", f"/samples/{sid}/summary", who="HEAD2")
    orders = [o for o in summ["testOrders"] if not o["isSuperseded"]]
    ordered = sorted(o["testCode"] for o in orders)
    s, specs, m = api("GET", f"/masterdata/specifications?itemId={item['id']}")
    rows = [x for x in specs if low(x.get("grade")) == low(grade) or x.get("grade") is None]
    exp_server = {x["testCode"] for x in rows}
    approved_na = {n["testCode"] for n in na_by_item.get(item["id"], []) if n["status"] == "Approved" and (n.get("grade") is None or low(n["grade"]) == low(grade))}
    exp_server -= approved_na
    exp_file = expected_from_files(cat, mat, grade, approved_na)
    phys = [o for o in orders if o["sectionId"] == 2]
    micro = [o for o in orders if o["sectionId"] != 2]
    res.update(ordered_count=len(ordered), physchem_orders=len(phys), micro_orders=len(micro), expected_count=len(exp_server),
               missing=sorted(exp_server - set(ordered)), extra=sorted(set(ordered) - exp_server), file_vs_server=sorted(exp_file ^ exp_server))
    if res["missing"] or res["extra"]:
        problem("DATA" if res["missing"] else "SYSTEM", f"{key} ordered tests", f"missing {res['missing']} extra {res['extra']}")
    if res["file_vs_server"]:
        problem("DATA", f"{key} loaded rows", f"tests that differ between the material file and the server spec rows: {res['file_vs_server']}")
    # micro: spec rows and limits are the file rows
    mapping = cat["mapping"][code]
    for o in micro:
        tr = [x for x in rows if x["testCode"] == o["testCode"]]
        if not tr:
            problem("DATA", f"{key} micro {o['testCode']}", "ordered but no spec row for this grade")
            continue
        for x in tr:
            if x["limitType"] == "CountTiered" and x.get("upperLimit") is None:
                problem("DATA", f"{key} micro {o['testCode']}", f"count row {x['parameterName']!r} has no limit")
            if x["limitType"] == "PresenceAbsence" and not x.get("expectedState"):
                problem("DATA", f"{key} micro {o['testCode']}", f"presence row {x['parameterName']!r} has no expected state (Absence)")
    res["micro_rows"] = {o["testCode"]: [(x["parameterName"], x["limitType"], x.get("upperLimit"), x.get("expectedState"), x.get("expectedResultText")) for x in rows if x["testCode"] == o["testCode"]] for o in micro}

    STATE["problems"] = [p for p in STATE["problems"] if not p["where"].startswith(key)]
    ctx = Ctx(key, sid, item, grade, specs, defs, equipment)
    done = res.setdefault("recorded", {})
    order_ids = {o["testCode"]: o["testOrderId"] for o in phys}
    # moisture tests first: dried-basis assays read the sample's own LOD
    sequence = sorted(phys, key=lambda o: (0 if o["testCode"] == "RM-LOD" else 1 if defs[o["testCode"]]["workflowType"] in ("Gravimetric", "Measurement") else 2, o["testCode"]))
    for o in sequence:
        c = o["testCode"]
        d = defs[c]
        if done.get(c) == "ok":
            if c == "RM-LOD":
                ctx.lod = lod_from_state(res)
            continue
        h = HANDLERS.get(d["workflowType"])
        if h is None:
            done[c] = f"no handler for {d['workflowType']}"
            problem("SETUP", f"{key} {c}", f"e2e.py has no handler for workflow {d['workflowType']}")
            continue
        try:
            s, out, m = h(ctx, o["testOrderId"], c, d)
        except Exception as e:  # a handler that cannot build a body is a script problem, not a system one
            done[c] = f"script error: {e}"
            problem("SETUP", f"{key} {c}", f"script could not build the result: {e}")
            continue
        if s and s < 300:
            done[c] = "ok"
            summary = (out or {}).get("outcomeSummary") if isinstance(out, dict) else None
            print(f"  {c}: recorded ({summary})")
            if summary and re.search(r"out of|does not|OOS|fail", str(summary), re.I):
                problem("DATA", f"{key} {c}", f"conforming input judged: {summary}")
        else:
            long_rows = [x for x in ctx.specs_for(c) if len(x.get("specLimit") or x.get("expectedResultText") or "") > 100]
            if long_rows and "unexpected error" in (m or ""):
                m = (f"{m} (HTTP {s}; ErrorLogs: value too long for type character varying(100) - ParameterResults.SpecLimit is 100 characters, "
                     f"the spec text of {long_rows[0]['parameterName']!r} is {len(long_rows[0].get('specLimit') or long_rows[0]['expectedResultText'])})")
            done[c] = f"refused: {m}"
            print(f"  {c}: REFUSED {m}")
            problem("SYSTEM", f"{key} {c}", f"result refused: {m}")
        save_state()
    return ctx


# ---------------------------------------------------------------- review, approval, certificate

def finish_sample(item, mat, ctx):
    key = ctx.key
    res = STATE["results"][key]
    sid = ctx.sample_id
    s, summ, m = api("GET", f"/samples/{sid}/summary", who="HEAD2")
    if res.get("micro_orders"):
        res["approved"] = "n/a (micro orders not run)"
    else:
        pending = [o["testCode"] for o in summ["testOrders"] if not o["isSuperseded"] and o["currentStep"] not in ("Ready", "Reviewed", "Approved")]
        if res.get("approved") == "yes":
            pass
        elif pending:
            res["approved"] = f"no: results missing for {pending}"
        else:
            mq = mat.get("minQuantity") or {}
            s, _, m = api("POST", f"/samples/{sid}/review/complete",
                          {"password": pw("REVIEWER"), "comment": "E2E review", "sectionId": 2, "retentionQuantity": mq.get("retention") or 50, "retentionQuantityUnit": mq.get("unit") or "g"}, who="REVIEWER")
            if s >= 300:
                res["approved"] = f"no: review refused: {m}"
                problem("SYSTEM", f"{key} review", f"review refused: {m}")
            else:
                s, _, m = api("POST", f"/samples/{sid}/approval/decide", {"password": pw("HEAD"), "decision": "Approve", "comment": "E2E approval", "sectionId": 2}, who="HEAD")
                if s >= 300:
                    res["approved"] = f"no: approval refused: {m}"
                    problem("SYSTEM", f"{key} approval", f"approval refused: {m}")
                else:
                    res["approved"] = "yes"
    check_certificate(item, mat, ctx, res)
    save_state()


def check_certificate(item, mat, ctx, res):
    sid = ctx.sample_id
    s, summ, m = api("GET", f"/samples/{sid}/summary" + ("?forCertificate=true" if res.get("approved") == "yes" else ""), who="HEAD2")
    if s >= 300:
        res["coa"] = f"no: certificate refused: {m}"
        problem("SYSTEM", f"{ctx.key} certificate", f"certificate refused: {m}")
        return
    sample = summ["certificate"]["sample"]
    rows = (sample.get("simple") or {}).get("rows") or []
    supporting = sample.get("supportingRows") or []
    res["coa_rows"] = len(rows)
    res["coa_supporting"] = [f"{x['testDisplayName']}: {x['result']}" for x in supporting]
    recorded = {c for c, v in (res.get("recorded") or {}).items() if v == "ok"}
    notes, bad, unrecorded = [], [], []
    for sp in ctx.specs:
        if low(sp.get("grade")) != low(ctx.grade) or sp["testCode"] not in ctx.defs or ctx.defs[sp["testCode"]].get("sectionId") != 2:
            continue
        if sp["testCode"] not in recorded:
            unrecorded.append(f"{sp['testCode']}/{sp['parameterName']}")
            continue
        hit = [x for x in rows if low(x.get("testDisplayName")) == low(sp["parameterName"]) and x["testCode"].split(":")[0] == sp["testCode"]]
        if len(hit) != 1:
            bad.append(f"{sp['testCode']}/{sp['parameterName']} printed {len(hit)} times")
        elif hit[0].get("noResult") or hit[0].get("limitsNotConfigured") or not hit[0].get("conform") or not hit[0].get("result") or hit[0]["result"] == "—":
            bad.append(f"{sp['testCode']}/{sp['parameterName']} printed as {hit[0].get('result')!r} conform={hit[0].get('conform')}")
    na_rows = [x for x in supporting if x["kind"] == "NotApplicable"]
    st_rows = [x for x in supporting if x["kind"] == "SupplierStated"]
    exp_na = len([n for n in mat.get("notApplicable", []) if n.get("grade") is None or low(n["grade"]) == low(ctx.grade)])
    exp_attr = len([a for a in mat.get("supplierAttributes", []) if a.get("grade") is None or low(a["grade"]) == low(ctx.grade)])
    if bad:
        notes.append(f"spec rows not printed with a conforming result: {bad}")
    if len(na_rows) != exp_na or any(x["result"].split(" (")[0] != "Not applicable" for x in na_rows):
        notes.append(f"expected {exp_na} 'Not applicable' rows, the certificate has {[x['testDisplayName'] + ': ' + x['result'] for x in na_rows]}")
    if len(st_rows) != exp_attr or any("(stated by supplier)" not in x["result"] for x in st_rows):
        notes.append(f"expected {exp_attr} 'stated by supplier' rows, the certificate has {len(st_rows)}")
    if unrecorded:
        notes.append(f"{len(unrecorded)} spec rows have no result because their orders were refused: {unrecorded}")
    res["coa_scope"] = "approved sample" if res.get("approved") == "yes" else "unapproved sample, read from the summary"
    res["coa"] = "yes" if not notes else "no: " + "; ".join(notes)
    res["coa_unrecorded"] = len(unrecorded)
    res["coa_ok_for_recorded"] = not (bad or len(na_rows) != exp_na or len(st_rows) != exp_attr)
    if bad or len(na_rows) != exp_na or len(st_rows) != exp_attr:
        problem("DATA", f"{ctx.key} certificate", "; ".join(n for n in notes if "no result because" not in n)[:700])


# ---------------------------------------------------------------- report

def short_approved(r_):
    a = r_.get("approved", "-")
    return a if not a.startswith("no:") else "no (" + a[4:].split(":")[0] + ")"


def short_coa(r_):
    if r_.get("coa") == "yes":
        return "yes"
    if r_.get("coa_ok_for_recorded"):
        return f"no: {r_.get('coa_unrecorded')} spec row(s) have no result (order refused); all other rows, not-applicable and supplier rows ok"
    return "no: " + (r_.get("coa") or "-")[4:]


def write_report():
    L = ["# End-to-end check on local LIMSV2 (Plan 6, Task 7)", "",
         f"Run {datetime.datetime.now().strftime('%Y-%m-%d %H:%M')} against {BASE} by `tools/rm-data-setup/e2e.py` (E2E test accounts, local only).", "",
         "One sample per material and grade (batch `RM-E2E-<code>-<grade>`), received for every laboratory that has tests. "
         "Ordered = the test orders the receiving created; expected = the grade's loaded spec rows' tests minus approved not-applicable tests "
         "(rows held back are not loaded, so those tests are not expected). Micro orders are only checked (spec rows and limits), never run; "
         "so a sample with micro orders is not reviewed or approved, and its certificate is read from the sample summary while unapproved.", "",
         "| Material | Grade | Sample | Ordered | Expected | Ordered = expected | Physchem results recorded | Micro orders | Approved | CoA rows ok |", "|---|---|---|---|---|---|---|---|---|---|"]
    for key, r_ in STATE["results"].items():
        rec = r_.get("recorded", {})
        ok = sum(1 for v in rec.values() if v == "ok")
        diff = "yes" if not (r_.get("missing") or r_.get("extra")) else f"NO (missing {r_.get('missing')}, extra {r_.get('extra')})"
        L.append(f"| {r_['item']} | {r_['grade']} | {r_.get('sampleId', '-')} | {r_.get('ordered_count', '-')} | {r_.get('expected_count', '-')} | {diff} | {ok} of {r_.get('physchem_orders', '-')} | {r_.get('micro_orders', '-')} | {short_approved(r_)} | {short_coa(r_)} |")
    L += ["", "## Problems", ""]
    for kind, title in (("DATA", "DATA problems (our catalog or spec rows are wrong)"), ("SYSTEM", "SYSTEM problems (the app refuses something valid)"), ("SETUP", "Workflows that could not be completed here")):
        items = [p for p in STATE["problems"] if p["kind"] == kind]
        L.append(f"### {title}")
        L.append("")
        L += [f"- **{p['where']}**: {p['text']}" for p in items] or ["- none"]
        L.append("")
    L += ["## Per-sample detail", ""]
    for key, r_ in STATE["results"].items():
        L.append(f"### {key}")
        for c, v in (r_.get("recorded") or {}).items():
            if v != "ok":
                L.append(f"- {c}: {v}")
        if r_.get("coa_supporting"):
            L.append(f"- certificate supporting rows: {r_['coa_supporting']}")
        L.append(f"- certificate: {r_.get('coa', '-')} ({r_.get('coa_scope', '-')})")
        L.append("")
    REPORT.write_text("\n".join(L) + "\n", encoding="utf-8")


# ---------------------------------------------------------------- main

def main():
    global BASE
    ap = argparse.ArgumentParser()
    ap.add_argument("--only")
    ap.add_argument("--report-only", action="store_true")
    ap.add_argument("--base-url", default=BASE)
    a = ap.parse_args()
    if urlparse(a.base_url).hostname not in ("localhost", "127.0.0.1"):
        raise SystemExit("e2e.py runs against a local API only")
    BASE = a.base_url
    if STATE_FILE.exists():
        STATE.update(json.loads(STATE_FILE.read_text(encoding="utf-8")))
    if a.report_only:
        write_report()
        return
    cat, mats = load_data()
    s, items, _ = api("GET", "/items")
    items_by_code = {i["code"]: i for i in items}
    s, dl, _ = api("GET", "/masterdata/test-definitions")
    defs = {d["code"]: d for d in dl}
    s, eq, _ = api("GET", "/masterdata/equipment/configured-summary", who="ANALYST")
    uv = next((e["id"] for e in eq if e.get("type") == "UvVis" and (e.get("sectionId") == 2 or (e.get("section") or {}).get("code") == "FP")), None)
    equipment = {"uv": uv}
    na_by_item = {}
    for m in mats:
        na_by_item[items_by_code[m["itemCode"]]["id"]] = api("GET", f"/items/{items_by_code[m['itemCode']]['id']}/not-applicable-tests")[1] or []
    STATE["problems"] = [p for p in STATE["problems"] if not p["where"].startswith(("titrant", "primary standard"))]
    for mat in mats:
        if a.only and mat["itemCode"] != a.only:
            continue
        for g in mat["grades"]:
            ctx = run_sample(cat, mat, g["grade"], items_by_code, defs, equipment, na_by_item)
            if ctx is not None:
                finish_sample(items_by_code[mat["itemCode"]], mat, ctx)
    save_state()
    write_report()


if __name__ == "__main__":
    main()
