"""Copy the RM data setup rows from local LIMSV2 to production Neon with SQL (development phase, no API).

The loader created and the E2E check validated these rows on local; this copies exactly those rows,
translating every foreign key through its natural key on the target. One transaction; --dry-run rolls back.

    py -I copy_to_prod.py --target-url "<postgres url>" --as-user mali [--dry-run]

Local connection: backend/MicroLIMS.API/appsettings.Development.json, database LIMSV2; the pre-load
backup restored as LIMSV2_preload marks which rows the loader added.
"""
import argparse, json, os, re, subprocess, sys

PSQL = r"C:/Program Files/PostgreSQL/18/bin/psql"
ROOT = os.path.dirname(os.path.abspath(__file__))
import glob
# every material file's item (batch 1 and 2); rows already on the target are skipped by natural key
ITEMS = sorted({json.load(open(f, encoding='utf-8'))['itemCode'] for f in glob.glob(os.path.join(ROOT, 'materials', '*.json'))})


def local_env():
    cfg = json.load(open(os.path.join(ROOT, '../../backend/MicroLIMS.API/appsettings.Development.json'), encoding='utf-8-sig'))
    pw = re.search(r'Password=([^;]+)', cfg['ConnectionStrings']['Default']).group(1)
    return dict(os.environ, PGPASSWORD=pw)


def psql_json(sql, db=None, url=None):
    """Run a query returning one json value."""
    args = [PSQL, '-tA', '-c', sql] + ([url] if url else ['-h', 'localhost', '-U', 'postgres', '-d', db])
    env = local_env() if db else dict(os.environ, PGOPTIONS='-c default_transaction_read_only=on')
    out = subprocess.run(args, capture_output=True, text=True, encoding='utf-8', env=env)
    if out.returncode:
        sys.exit(f'psql failed: {out.stderr.strip()[:400]}')
    return json.loads(out.stdout.strip() or 'null')


def rows(sql, db='LIMSV2', url=None):
    return psql_json(f'select coalesce(json_agg(t), \'[]\') from ({sql}) t', db=db, url=url) or []


def lit(v):
    return 'NULL' if v is None else "'" + str(v).replace("'", "''") + "'"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--target-url', required=True)
    ap.add_argument('--as-user', required=True, help='target username recorded as creator')
    ap.add_argument('--dry-run', action='store_true')
    ap.add_argument('--baseline-db', default='LIMSV2_preload', help='restore of LIMSV2 from before the load being copied')
    ap.add_argument('--exclude-spec-ids', default='', help='comma list of local Specifications ids not to copy')
    a = ap.parse_args()
    T = a.target_url
    ITEMS_ALL = list(ITEMS)
    loc_have = {r['Code'] for r in rows('select "Code" from "Items"')}
    items_in = ','.join(lit(c) for c in ITEMS_ALL if c in loc_have)
    exclude_specs = {int(x) for x in a.exclude_spec_ids.split(',') if x.strip()}

    # ---- local: what the loader added (ids absent from the pre-load restore) ----
    def new_ids(table):
        before = {r['Id'] for r in rows(f'select "Id" from "{table}"', db=a.baseline_db)}
        return [r for r in rows(f'select * from "{table}" order by "Id"') if r['Id'] not in before]

    tests = new_ids('TestDefinitions')
    sols = new_ids('SolutionMasters')
    entries_new = new_ids('MaterialMasterEntries')
    loc_entries = {r['Id']: r for r in rows('select * from "MaterialMasterEntries"')}
    loc_sols = {r['Id']: r for r in rows('select * from "SolutionMasters"')}
    loc_sections = {r['Id']: r['Code'] for r in rows('select "Id","Code" from "DocumentSections"')}
    loc_cats = {r['Id']: r['Name'] for r in rows('select "Id","Name" from "MaterialCategories"')}
    loc_items = {r['Id']: r['Code'] for r in rows(f'select "Id","Code" from "Items" where "Code" in ({items_in})')}
    item_ids = ','.join(str(i) for i in loc_items)

    # ---- target lookups (read-only) ----
    t_entries = {(r['Name'] or '').lower(): r for r in rows('select "Id","Code","Name" from "MaterialMasterEntries"', db=None, url=T)}
    t_entry_codes = {r['Code'] for r in t_entries.values()}
    t_sols = {r['Name'].lower() for r in rows('select "Name" from "SolutionMasters"', db=None, url=T)}
    t_tests = {r['Code'] for r in rows('select "Code" from "TestDefinitions"', db=None, url=T)}
    t_items = {r['Code']: r for r in rows(f'select "Id","Code","Category" from "Items" where "Code" in ({items_in})', db=None, url=T)}
    t_specs = {(r['Code'], r['TestCode'], r['ParameterName'], r['Grade']) for r in rows(
        f'select i."Code", s."TestCode", s."ParameterName", s."Grade" from "Specifications" s join "Items" i on i."Id"=s."ItemId" where i."Code" in ({items_in})', db=None, url=T)}
    t_attrs = {(r['Code'], r['Name'], r['Grade']) for r in rows(
        f'select i."Code", a."Name", a."Grade" from "SupplierStatedAttributes" a join "Items" i on i."Id"=a."ItemId" where i."Code" in ({items_in})', db=None, url=T)}
    t_nas = {(r['Code'], r['TestCode'], r['Grade']) for r in rows(
        f'select i."Code", n."TestCode", n."Grade" from "NotApplicableTests" n join "Items" i on i."Id"=n."ItemId" where i."Code" in ({items_in}) and n."Status" <> 3', db=None, url=T)}
    t_user = rows(f'select "Id" from "Users" where "Username"={lit(a.as_user)}', db=None, url=T)
    if not t_user:
        sys.exit(f'target user {a.as_user} not found')
    uid = t_user[0]['Id']
    missing_items = [c for c in ITEMS_ALL if c in loc_have and c not in t_items]
    if missing_items:
        print(f'items missing on target (skipped): {missing_items}')
    loc_items = {k: v for k, v in loc_items.items() if v in t_items}
    item_ids = ','.join(str(i) for i in loc_items)
    # Rows already on the target (an earlier API load) are left alone; only missing rows are copied.

    # ---- which solutions / entries must be copied ----
    sol_copy = {s['Id']: s for s in sols}
    def need_sol(sid):
        if sid and sid not in sol_copy and loc_sols[sid]['Name'].lower() not in t_sols:
            sol_copy[sid] = loc_sols[sid]
    for t in tests:
        need_sol(t.get('TitrantSolutionMasterId')); need_sol(t.get('TitrationExcessSolutionMasterId'))
    for s in list(sol_copy.values()):
        need_sol(s.get('ReferenceSolutionId'))
    sol_copy = {k: v for k, v in sol_copy.items() if v['Name'].lower() not in t_sols}
    comps = rows(f'select * from "SolutionComponents" where "SolutionMasterId" in ({",".join(map(str, sol_copy)) or "0"}) order by "Id"')
    for c in comps:
        need_sol(c.get('SourceSolutionMasterId'))

    entry_copy = {e['Id']: e for e in entries_new if (e['Name'] or '').lower() not in t_entries}
    def need_entry(eid):
        if eid and (loc_entries[eid]['Name'] or '').lower() not in t_entries:
            entry_copy[eid] = loc_entries[eid]
    for t in tests:
        for f in ('TitrationIndicatorEntryId', 'TitrationStandardEntryId', 'UvStandardEntryId'):
            need_entry(t.get(f))
    for s in sol_copy.values():
        need_entry(s.get('StandardEntryId')); need_entry(s.get('PhAdjustingEntryId'))
    for c in comps:
        need_entry(c.get('MaterialMasterEntryId'))
    # codes are unique: give a colliding copied entry the next free number of its prefix
    for e in entry_copy.values():
        if e['Code'] in t_entry_codes:
            m = re.match(r'([A-Za-z]+-)(\d+)$', e['Code'] or '')
            pre, n = (m.group(1), int(m.group(2))) if m else ((e['Code'] or 'X') + '-', 0)
            while f'{pre}{n:03d}' in t_entry_codes:
                n += 1
            e['Code'] = f'{pre}{n:03d}'
        t_entry_codes.add(e['Code'])

    tests = [t for t in tests if t['Code'] not in t_tests]

    # ---- SQL ----
    def entry_ref(eid):
        return 'NULL' if not eid else f'(select "Id" from "MaterialMasterEntries" where lower("Name")=lower({lit(loc_entries[eid]["Name"])}) order by "Id" limit 1)'
    def sol_ref(sid):
        return 'NULL' if not sid else f'(select "Id" from "SolutionMasters" where lower("Name")=lower({lit(loc_sols[sid]["Name"])}) order by "Id" limit 1)'
    def section_ref(sid):
        return 'NULL' if not sid else f'(select "Id" from "DocumentSections" where "Code"={lit(loc_sections[sid])})'
    def cat_ref(cid):
        return 'NULL' if not cid else f'(select "Id" from "MaterialCategories" where "Name"={lit(loc_cats[cid])})'
    def item_ref(iid):
        return f'(select "Id" from "Items" where "Code"={lit(loc_items[iid])})'

    t_cols = {}
    def target_cols(table):
        if table not in t_cols:
            t_cols[table] = {r['column_name'] for r in rows(
                f"select column_name from information_schema.columns where table_name={lit(table)} and table_schema='public'", db=None, url=T)}
        return t_cols[table]

    skipped_cols = set()
    def insert(table, row, overrides):
        have = target_cols(table)      # the target may be one migration behind: copy only columns both sides have
        skipped_cols.update(f'{table}.{c}' for c in row if c not in have and c not in ('Id', 'xmin'))
        cols = [c for c in row if c not in ('Id', 'xmin') and c in have]
        sel = [overrides.get(c, f'r."{c}"') for c in cols]
        js = json.dumps(row, ensure_ascii=False)
        assert '$j$' not in js
        return (f'insert into "{table}" ({", ".join(chr(34)+c+chr(34) for c in cols)}) select {", ".join(sel)} '
                f'from json_populate_record(null::"{table}", $j${js}$j$::json) r;')

    sql = ['begin;']
    for e in entry_copy.values():
        sql.append(insert('MaterialMasterEntries', e, {'SectionId': section_ref(e.get('SectionId')), 'MaterialCategoryId': cat_ref(e.get('MaterialCategoryId')),
                                                     'CreatedByUserId': str(uid), 'LastModifiedByUserId': str(uid), 'Code': lit(e['Code'])}))
    for s in sol_copy.values():
        sql.append(insert('SolutionMasters', s, {'SectionId': section_ref(s.get('SectionId')), 'StandardEntryId': entry_ref(s.get('StandardEntryId')),
                                                 'PhAdjustingEntryId': entry_ref(s.get('PhAdjustingEntryId')), 'ReferenceSolutionId': 'NULL',
                                                 'CreatedByUserId': str(uid), 'LastModifiedByUserId': str(uid)}))
    for s in sol_copy.values():
        if s.get('ReferenceSolutionId'):
            sql.append(f'update "SolutionMasters" set "ReferenceSolutionId"={sol_ref(s["ReferenceSolutionId"])} where lower("Name")=lower({lit(s["Name"])});')
    for c in comps:
        sql.append(insert('SolutionComponents', c, {'SolutionMasterId': sol_ref(c['SolutionMasterId']), 'MaterialMasterEntryId': entry_ref(c.get('MaterialMasterEntryId')),
                                                    'SourceSolutionMasterId': sol_ref(c.get('SourceSolutionMasterId'))}))
    for t in tests:
        sql.append(insert('TestDefinitions', t, {'SectionId': section_ref(t.get('SectionId')), 'TitrantSolutionMasterId': sol_ref(t.get('TitrantSolutionMasterId')),
                                                 'TitrationExcessSolutionMasterId': sol_ref(t.get('TitrationExcessSolutionMasterId')),
                                                 'TitrationIndicatorEntryId': entry_ref(t.get('TitrationIndicatorEntryId')),
                                                 'TitrationStandardEntryId': entry_ref(t.get('TitrationStandardEntryId')),
                                                 'UvStandardEntryId': entry_ref(t.get('UvStandardEntryId')), 'HplcMethodId': 'NULL', 'IcpMethodId': 'NULL'}))
    for it in rows(f'select "Id","MinSampleQuantity","MinRetestSampleQuantity","MinSampleQuantityUnit" from "Items" where "Id" in ({item_ids})'):
        sql.append(f'update "Items" set "MinSampleQuantity"={lit(it["MinSampleQuantity"])}, "MinRetestSampleQuantity"={lit(it["MinRetestSampleQuantity"])}, '
                   f'"MinSampleQuantityUnit"={lit(it["MinSampleQuantityUnit"])} where "Code"={lit(loc_items[it["Id"]])};')
    t_assigned = {(r['Code'], r['TestCode']) for r in rows(f'select i."Code", st."TestCode" from "SampleTests" st join "Items" i on i."Id"=st."ItemId" where i."Code" in ({items_in})', db=None, url=T)}
    for st in rows(f'select * from "SampleTests" where "ItemId" in ({item_ids}) order by "Id"'):
        if (loc_items[st['ItemId']], st['TestCode']) not in t_assigned:
            sql.append(insert('SampleTests', st, {'ItemId': item_ref(st['ItemId'])}))
    specs = [r for r in rows(f'select * from "Specifications" where "ItemId" in ({item_ids}) order by "Id"')
             if r['Id'] not in exclude_specs and (loc_items[r['ItemId']], r['TestCode'], r['ParameterName'], r['Grade']) not in t_specs]
    for sp in specs:
        sql.append(insert('Specifications', sp, {'ItemId': item_ref(sp['ItemId']), 'HplcMethodAnalyteId': 'NULL', 'HplcMethodImpurityId': 'NULL', 'IcpMethodElementId': 'NULL'}))
    attrs = [r for r in rows(f'select * from "SupplierStatedAttributes" where "ItemId" in ({item_ids}) order by "Id"')
             if (loc_items[r['ItemId']], r['Name'], r['Grade']) not in t_attrs]
    for at in attrs:
        sql.append(insert('SupplierStatedAttributes', at, {'ItemId': item_ref(at['ItemId'])}))
    nas = [r for r in rows(f'select * from "NotApplicableTests" where "ItemId" in ({item_ids}) order by "Id"')
             if (loc_items[r['ItemId']], r['TestCode'], r['Grade']) not in t_nas]
    for na in nas:   # unsigned drafts: the real Section Head and QC Manager sign them in the app
        sql.append(insert('NotApplicableTests', na, {'ItemId': item_ref(na['ItemId']), 'CreatedByUserId': str(uid), 'Status': '0',
                                                     'HeadSignatureId': 'NULL', 'FinalSignatureId': 'NULL'}))
    sql.append('rollback;' if a.dry_run else 'commit;')
    if skipped_cols:
        print('columns not on the target (left out):', ', '.join(sorted(skipped_cols)))

    path = os.path.join(ROOT, 'reports', 'prod-copy.sql')
    open(path, 'w', encoding='utf-8').write('\n'.join(sql) + '\n')
    print(f'entries {len(entry_copy)}, solutions {len(sol_copy)}, components {len(comps)}, tests {len(tests)}, '
          f'items {len(loc_items)}, specs {len(specs)}, attributes {len(attrs)}, not-applicable {len(nas)}')
    out = subprocess.run([PSQL, '-v', 'ON_ERROR_STOP=1', '-q', '-f', path, T], capture_output=True, text=True, encoding='utf-8')
    print(('DRY RUN (rolled back): ' if a.dry_run else 'APPLIED: ') + ('ok' if out.returncode == 0 else 'FAILED'))
    if out.returncode:
        print(out.stderr.strip()[:1500]); sys.exit(1)


if __name__ == '__main__':
    main()
