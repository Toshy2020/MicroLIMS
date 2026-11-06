"""Merge duplicate RM test definitions (same general test, only the item's limits differ).

  py -I merge_tests.py catalog                         rewrite catalog.json + catalog-b2/*.json
  py -I merge_tests.py db --db LIMSV2 [--dry-run]      apply to a local database
  py -I merge_tests.py db --target-url URL [--dry-run] apply to another database (production)

Each group gets one shared code. References (specifications, not-applicable records, sample tests,
orders, result records, workload weights) move to it, the member definitions are deleted. A group
whose members differ in any setting other than code/name is refused. Idempotent.
"""
import argparse, glob, json, os, subprocess, sys

PSQL = r"C:/Program Files/PostgreSQL/18/bin/psql"
HERE = os.path.dirname(os.path.abspath(__file__))

# new code: (display name, members)
MERGES = {
    "RM-REFIDX": ("Refractive index", ["RM-REFIDX-141", "RM-REFR-002", "RM-REFIDX-080", "RM-RI-087", "RM-REFR-123", "RM-REFR-093"]),
    "RM-RELDENS": ("Relative density / specific gravity", ["RM-RELDENS-002", "RM-RELDEN-080", "RM-RELDENS-093", "RM-SG-139", "RM-SG-087"]),
    "RM-LT-AS": ("Arsenic (limit test)", ["RM-LT-AS-036", "RM-LT-AS-050", "RM-LT-AS-067", "RM-LT-AS-078", "RM-LT-AS-125", "RM-LT-AS-127"]),
    "RM-LT-PB-AAS": ("Lead (limit test by AAS)", ["RM-LT-PB-030", "RM-LT-PB-050", "RM-LT-PB-125"]),
    "RM-LT-PB": ("Lead (limit test)", ["RM-LT-PB-067", "RM-LT-PB-078"]),
    "RM-BA": ("Barium", ["RM-BA-127", "RM-LT-BA-078", "RM-BA-145"]),
    "RM-ASH": ("Total ash", ["RM-ASH-130", "RM-ASH-114", "RM-ASH-119", "RM-ASH-132", "RM-ASH-073"]),
    "RM-ID-FILM": ("Identification (film formation)", ["RM-ID-FILM-130", "RM-ID-FILM-114", "RM-ID-FILM-119", "RM-ID-FILM-132", "RM-ID-FILM-073"]),
    "RM-DISP": ("Dispersion", ["RM-DISP-130", "RM-DISP-119", "RM-DISP-132", "RM-DISP-073"]),
    "RM-VISC": ("Viscosity", ["RM-VISC-049", "RM-VISC-CMC-004", "RM-VISC-143", "RM-VISC-125"]),
    "RM-LOI": ("Loss on ignition", ["RM-LOI-127", "RM-LOI-SIL-065", "RM-LOI-107", "RM-LOI-058"]),
    "RM-AIS": ("Acid-insoluble substances", ["RM-ACIDINS-CA", "RM-AIS-127", "RM-AIS-058"]),
    "RM-PSD": ("Particle size distribution", ["RM-PSD-CACO3-078", "RM-PSD-127", "RM-PSD-058"]),
    "RM-AV": ("Acid value", ["RM-AV-080", "RM-AV-139", "RM-AV-149"]),
    "RM-PV": ("Peroxide value", ["RM-PV-139", "RM-PV-149"]),
    "RM-ACIDALK-VOL": ("Acidity or alkalinity (titration volume)", ["RM-ACID-ATOC", "RM-ACID-ALK-MG", "RM-ACID-049", "RM-TIT-ACIDALK-084", "RM-ACID-KSORB-047", "RM-TIT-SLS-ALK"]),
    "RM-ACIDALK": ("Acidity or alkalinity", ["RM-ACID-001", "RM-ACID-067", "RM-ACIDALK-036"]),
    "RM-ID-SO4": ("Identification (reaction of sulfate)", ["RM-ID-SO4-SLS", "RM-ID-SO4-SMB", "RM-ID-SO4-BP-067", "RM-ID-SO4-USP-067", "RM-ID-SO4-030"]),
    "RM-ID-ZN": ("Identification (reaction of zinc)", ["RM-ID-ZN-BP-067", "RM-ID-ZN-050", "RM-ID-ZN-USP-067"]),
    "RM-ID-MG": ("Identification (reaction of magnesium)", ["RM-ID-MG-107", "RM-ID-MG-058", "RM-ID-CR-MG"]),
    "RM-ID-CA": ("Identification (reaction of calcium)", ["RM-ID-CA", "RM-ID-CA-036"]),
    "RM-WSS": ("Water-soluble substances", ["RM-WAT-SOL", "RM-WSUB-024"]),
    "RM-DE": ("Dextrose equivalent", ["RM-TIT-DE-129", "RM-DE-061"]),
    "RM-SO2": ("Sulfur dioxide", ["RM-SO2-129", "RM-SO2-061"]),
    "RM-CARB": ("Carbonates", ["RM-CARB-107", "RM-CARB-127"]),
    "RM-OXAL": ("Oxalate / oxalic acid (limit test)", ["RM-OXAL-048", "RM-LT-OXAL-035"]),
    "RM-TIT-CA-EDTA": ("Assay of calcium (complexometric EDTA titration)", ["RM-TIT-CADC-142", "RM-TIT-CAGLY-036"]),
}
OLD_TO_NEW = {m: new for new, (_, ms) in MERGES.items() for m in ms}
REF_COLUMNS = ["Specifications", "NotApplicableTests", "SampleTests", "TestOrders", "ResultRecords",
               "WorkloadWeights", "WorkloadWeightHistories"]


def catalog():
    files = [os.path.join(HERE, "catalog.json")] + sorted(glob.glob(os.path.join(HERE, "catalog-b2", "*.json")))
    defs = {}
    for f in files:
        for t in json.load(open(f, encoding="utf-8")).get("testDefinitions", []):
            defs.setdefault(t["code"], t)
    merged = []
    for new, (name, members) in MERGES.items():
        found = [defs[m] for m in members if m in defs] or ([defs[new]] if new in defs else [])
        if not found:
            sys.exit(f"{new}: no member in the catalog")
        keep = dict(found[0])
        keep.update(code=new, displayName=name, usedBy=sorted({u for d in found for u in d.get("usedBy", [])}),
                    sources=[s for d in found for s in d.get("sources", [])],
                    notes=f"Shared test; the item specification holds the limits. Procedure notes by former code: "
                          + " | ".join(f"{d['code']}: {d.get('notes') or '-'}" for d in found))
        merged.append(keep)
    for f in files:
        c = json.load(open(f, encoding="utf-8"))
        tds = []
        for t in c.get("testDefinitions", []):
            if t["code"] in OLD_TO_NEW or t["code"] in MERGES:
                continue
            tds.append(t)
        if "testDefinitions" in c:
            c["testDefinitions"] = tds
        for item, mp in (c.get("mapping") or {}).items():
            for k, v in list(mp.items()):
                if isinstance(v, str) and v in OLD_TO_NEW:
                    mp[k] = OLD_TO_NEW[v]
        json.dump(c, open(f, "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=2)
        open(f, "a", encoding="utf-8").write("\n")
    out = os.path.join(HERE, "catalog-b2", "z-merged.json")
    json.dump({"testDefinitions": merged}, open(out, "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=2)
    print(f"catalog: {len(merged)} shared definitions in catalog-b2/z-merged.json, {len(OLD_TO_NEW)} members removed")


def q(s):
    return "'" + s.replace("'", "''") + "'"


def psql(args, sql):
    r = subprocess.run([PSQL, *args, "-v", "ON_ERROR_STOP=1", "-At", "-c", sql], capture_output=True, text=True, encoding="utf-8")
    if r.returncode:
        sys.exit(f"psql failed: {r.stderr.strip()[:600]}")
    return r.stdout.strip()


def db(args, dry):
    ign = {"Id", "Code", "DisplayName", "IsActive", "xmin"}
    rows = json.loads(psql(args, "select coalesce(json_agg(t),'[]') from \"TestDefinitions\" t where \"Code\" like 'RM-%'"))
    by = {r["Code"]: r for r in rows}
    sql = ["BEGIN;"]
    for new, (name, members) in MERGES.items():
        present = [m for m in members if m in by and m != new]
        # Item assignments keep their own copy of the name, and an item that had two members (BP and USP
        # versions) would now list the test twice. Both are fixed every run (idempotent).
        tidy = [f'UPDATE "SampleTests" SET "DisplayName"={q(name)} WHERE "TestCode"={q(new)};',
                f'DELETE FROM "SampleTests" a USING "SampleTests" b WHERE a."TestCode"={q(new)} AND b."TestCode"={q(new)} '
                f'AND a."ItemId" = b."ItemId" AND a."Id" > b."Id";']
        if not present:
            if new in by:
                print(f"{new}: done already")
                sql.extend(tidy)
            continue
        sigs = {json.dumps({k: v for k, v in by[m].items() if k not in ign}, sort_keys=True) for m in present}
        if new in by and new not in members:
            sigs.add(json.dumps({k: v for k, v in by[new].items() if k not in ign}, sort_keys=True))
        if len(sigs) > 1:
            diff = sorted({k for m in present for k in by[m] if k not in ign and by[m][k] != by[present[0]][k]})
            sys.exit(f"{new}: members differ in {diff}; refusing")
        keeper = new if new in by else present[0]
        others = [m for m in present if m != keeper]
        lst = ",".join(q(m) for m in others + ([keeper] if keeper != new else []))
        sql.append(f'UPDATE "TestDefinitions" SET "Code"={q(new)}, "DisplayName"={q(name)} WHERE "Code"={q(keeper)};')
        for t in REF_COLUMNS:
            sql.append(f'UPDATE "{t}" SET "TestCode"={q(new)} WHERE "TestCode" IN ({lst});')
        sql.extend(tidy)
        if others:
            ol = ",".join(q(m) for m in others)
            sql.append(f'DELETE FROM "TestWorkflowSteps" WHERE "TestDefinitionId" IN (SELECT "Id" FROM "TestDefinitions" WHERE "Code" IN ({ol}));')
            sql.append(f'DELETE FROM "TestDefinitions" WHERE "Code" IN ({ol});')
        print(f"{new}: {len(present)} -> 1 (kept row of {keeper})")
    sql.append('SELECT count(*) FROM "TestDefinitions" WHERE "Code" LIKE \'RM-%\';')
    sql.append("ROLLBACK;" if dry else "COMMIT;")
    path = os.path.join(HERE, "reports", "merge-tests.sql")
    open(path, "w", encoding="utf-8").write("\n".join(sql) + "\n")
    r = subprocess.run([PSQL, *args, "-q", "-v", "ON_ERROR_STOP=1", "-At", "-f", path], capture_output=True, text=True, encoding="utf-8")
    if r.returncode:
        sys.exit(f"FAILED (nothing changed): {r.stderr.strip()[:800]}")
    print(("DRY RUN (rolled back)" if dry else "APPLIED") + f": RM tests now {r.stdout.strip()}")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["catalog", "db"])
    ap.add_argument("--db")
    ap.add_argument("--target-url")
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    if a.mode == "catalog":
        catalog()
    else:
        if not (a.db or a.target_url):
            sys.exit("--db or --target-url")
        args = [a.target_url] if a.target_url else ["-h", "localhost", "-U", "postgres", "-d", a.db]
        db(args, a.dry_run)
