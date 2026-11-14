# Third-Party Package Qualification: idb

**Status:** Draft - awaiting approval

**Prepared by:** ____________________  **Date:** ____________
**Reviewed / Approved by:** ____________________  **Date:** ____________

## Package Details

- **Package Name:** `idb`
- **Version:** `8.0.4` (exact pinned version in `frontend/package.json` and `frontend/package-lock.json`; released 2026-10-06 on the npm registry)
- **Licence:** `ISC`
- **Source:** npm registry (`https://registry.npmjs.org/idb/-/idb-8.0.4.tgz`)
- **Project Reference:** `frontend/package.json`; imported only by `frontend/src/services/drafts/draftStore.ts`

### Transitive Components

- None. `idb` has no runtime dependencies.

## Intended Use

- A small promise wrapper over the browser's IndexedDB, used by the on-tablet draft store (database `microlims-drafts`, store `drafts`) that keeps a bench analyst's unsaved entries when the connection drops or the page is interrupted.
- A draft holds only the form fields on an explicit per-form allow-list. It never holds a password, electronic-signature input or token.

## Excluded / Not Used For

- **GMP Impact Assessment:** Convenience storage of unsaved input on the device. A draft is never a laboratory record, is never submitted automatically, and takes no part in any calculation, specification evaluation, workflow transition, approval or audit trail. The record exists only when the analyst presses Save on the open form and the backend accepts it; the backend then holds the regulated data and its audit trail.
- **Risk - data left on a shared tablet:** Drafts are stored under the signed-in user's id and are listed and opened only for that user. Another user on the same tablet is told only that "Another user has unsaved entries on this tablet" (a count, no content). Drafts older than the retention (`bench.draftRetentionHours`, default 24, range 1-72) are removed when the application starts and the user is told how many. The device's browser storage is not encrypted by the application; tablets are expected to be managed devices with a screen lock (an operational control for the site's SOP).
- **Risk - stale data overwriting a newer record:** A draft stores the record version the form was opened with, and Save sends that version. The server's 409 keeps the draft and is never retried automatically with a newer version.
- Not used to store, cache or replay any API response.

## Verification

- Verified in Vitest against an in-memory IndexedDB (`fake-indexeddb`):
  - `frontend/src/services/drafts/draftStore.test.ts`: `put/get round trip`, `listForUser returns only that user's drafts, and get never returns another user's draft`, `countOtherUsers returns a count and no values`, `purge removes drafts older than the retention and reports how many`, `available() false when IndexedDB throws`
  - `frontend/src/hooks/useBenchDraft.test.tsx`: `fields outside pick() are never stored`, `draft for another record or another user is not offered`, `storage unavailable -> hook is inert and reports it`
  - `frontend/src/components/bench/DraftsMenu.test.tsx`: `expired drafts are purged at start and the user is told how many`

## Manual Verification Required Before Go-Live (Not Verified Automatically)

- [ ] On the chosen tablet model and browser, confirm drafts survive a page reload and a browser restart, and that a private-browsing window reports storage as unavailable instead of failing silently.
- [ ] Confirm the site SOP requires a screen lock on bench tablets.
