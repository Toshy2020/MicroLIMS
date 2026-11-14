# Third-Party Package Qualification: fake-indexeddb

**Status:** Draft - awaiting approval

**Prepared by:** ____________________  **Date:** ____________
**Reviewed / Approved by:** ____________________  **Date:** ____________

## Package Details

- **Package Name:** `fake-indexeddb`
- **Version:** `6.2.6` (exact pinned version in `frontend/package.json` and `frontend/package-lock.json`; released 2026-10-16 on the npm registry)
- **Licence:** `Apache-2.0`
- **Source:** npm registry (`https://registry.npmjs.org/fake-indexeddb/-/fake-indexeddb-6.2.6.tgz`)
- **Project Reference:** `frontend/package.json` (`devDependencies` only); imported by `frontend/src/setupTests.ts`

### Transitive Components

- None at runtime. It is installed as a development dependency and is not part of the production bundle.

## Intended Use

- Provides an in-memory IndexedDB implementation to the Vitest (jsdom) test environment so the on-tablet draft store and its hook are tested against a real IndexedDB API rather than a hand-written stub.

## Excluded / Not Used For

- **GMP Impact Assessment:** Test tooling only. It is not shipped to users, never runs in production, and holds no laboratory data. A defect in it can only cause a test to fail or pass incorrectly, which is covered by reviewing the tests and by the mutation checks recorded in the change.
- Not imported by any file under `frontend/src` other than `setupTests.ts`; the production build does not include it.

## Verification

- Exercised by every test that touches the draft store: `frontend/src/services/drafts/draftStore.test.ts`, `frontend/src/hooks/useBenchDraft.test.tsx`, `frontend/src/components/bench/DraftsMenu.test.tsx`, `frontend/src/layouts/MainLayout.test.tsx`.
- Confirmed in `npm run build` that the production bundle does not reference it.

## Manual Verification Required Before Go-Live (Not Verified Automatically)

- [ ] Real-browser behaviour of IndexedDB (quota, private mode, eviction) is not covered by this tool; see the `idb` qualification note.
