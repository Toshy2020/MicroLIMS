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
