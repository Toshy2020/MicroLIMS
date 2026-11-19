import { draftId, type Draft } from "./draftStore";

// Clears the fake IndexedDB between tests. The store closes every connection
// after each operation, so deleting the database is never blocked.
export function resetDraftDb(): Promise<void> {
  return new Promise((resolve) => {
    const request = indexedDB.deleteDatabase("microlims-drafts");
    request.onsuccess = () => resolve();
    request.onerror = () => resolve();
    request.onblocked = () => resolve();
  });
}

export function makeDraft<T>(overrides: Partial<Draft<T>> & { userId: number; values: T }): Draft<T> {
  const formKey = overrides.formKey ?? "count-entry";
  const recordKey = overrides.recordKey ?? "plate-1";
  return {
    id: draftId(overrides.userId, formKey, recordKey),
    formKey,
    recordKey,
    recordVersion: 3,
    title: "Plate 1 count",
    route: "/testing/plate-1",
    startedAtDevice: "2031-03-04T05:00:00.000Z",
    updatedAtDevice: "2031-03-04T05:10:00.000Z",
    state: "open",
    ...overrides
  };
}

// Every stored draft exactly as IndexedDB holds it, so a test can prove what is (and is not) on the tablet.
export function rawStoredDrafts(): Promise<unknown[]> {
  return new Promise((resolve, reject) => {
    const open = indexedDB.open("microlims-drafts");
    open.onerror = () => reject(open.error);
    open.onsuccess = () => {
      const db = open.result;
      if (!db.objectStoreNames.contains("drafts")) {
        db.close();
        resolve([]);
        return;
      }
      const all = db.transaction("drafts").objectStore("drafts").getAll();
      all.onerror = () => {
        db.close();
        reject(all.error);
      };
      all.onsuccess = () => {
        db.close();
        resolve(all.result);
      };
    };
  });
}

// A signed-in analyst, the way AuthProvider reads it from storage.
export function signInForTest(userId: number): void {
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_user_id", String(userId));
}
