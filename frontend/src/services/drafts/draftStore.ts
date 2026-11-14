import { openDB, type DBSchema } from "idb";

// Unsaved entries kept on this tablet. A draft is never a record: nothing here
// is sent to the server except by the user pressing Save on the open form.
// A draft holds only the fields its form's allow-list names (see useBenchDraft);
// never a password, signature or token.
export interface Draft<T> {
  // `${userId}:${formKey}:${recordKey}`
  id: string;
  userId: number;
  formKey: string;
  recordKey: string;
  // The version of the record the draft was started against; null for a new record.
  recordVersion: number | null;
  title: string;
  route: string;
  values: T;
  // Stated by the tablet's clock, stored verbatim, never corrected.
  startedAtDevice: string;
  updatedAtDevice: string;
  state: "open" | "rejected";
  rejectReason?: string;
}

interface DraftDb extends DBSchema {
  drafts: { key: string; value: Draft<unknown>; indexes: { byUser: number } };
}

const DB_NAME = "microlims-drafts";
const STORE = "drafts";

export function draftId(userId: number, formKey: string, recordKey: string): string {
  return `${userId}:${formKey}:${recordKey}`;
}

// Opens per operation and closes after it: no connection outlives a call, so a
// blocked or failing browser store can never leave the app holding a handle.
async function withDb<R>(work: (db: Awaited<ReturnType<typeof open>>) => Promise<R>): Promise<R> {
  const db = await open();
  try {
    return await work(db);
  } finally {
    db.close();
  }
}

function open() {
  return openDB<DraftDb>(DB_NAME, 1, {
    upgrade(db) {
      const store = db.createObjectStore(STORE, { keyPath: "id" });
      store.createIndex("byUser", "userId");
    }
  });
}

const listeners = new Set<() => void>();
function emitChange() {
  listeners.forEach((l) => l());
}

export const draftStore = {
  // Called after any write so menus and counters refresh.
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },

  async put<T>(draft: Draft<T>): Promise<void> {
    await withDb((db) => db.put(STORE, draft as Draft<unknown>));
    emitChange();
  },

  // Only the signed-in user's own draft is ever returned.
  async get<T>(userId: number, formKey: string, recordKey: string): Promise<Draft<T> | undefined> {
    const found = await withDb((db) => db.get(STORE, draftId(userId, formKey, recordKey)));
    return found && found.userId === userId ? (found as Draft<T>) : undefined;
  },

  async listForUser(userId: number): Promise<Draft<unknown>[]> {
    const rows = await withDb((db) => db.getAllFromIndex(STORE, "byUser", userId));
    return rows.filter((d) => d.userId === userId).sort((a, b) => b.updatedAtDevice.localeCompare(a.updatedAtDevice));
  },

  // How many drafts belong to other users. A number only: no title, route or value.
  async countOtherUsers(userId: number): Promise<number> {
    const keys = await withDb((db) => db.getAllKeysFromIndex(STORE, "byUser"));
    return keys.length - (await withDb((db) => db.countFromIndex(STORE, "byUser", userId)));
  },

  async remove(id: string): Promise<void> {
    await withDb((db) => db.delete(STORE, id));
    emitChange();
  },

  // Removes drafts last changed more than `hours` before `nowDevice`; returns how many.
  async purgeOlderThan(hours: number, nowDevice: Date): Promise<number> {
    const cutoff = nowDevice.getTime() - hours * 3_600_000;
    const removed = await withDb(async (db) => {
      const tx = db.transaction(STORE, "readwrite");
      let count = 0;
      for (const draft of await tx.store.getAll()) {
        const updated = Date.parse(draft.updatedAtDevice);
        if (Number.isNaN(updated) || updated < cutoff) {
          await tx.store.delete(draft.id);
          count += 1;
        }
      }
      await tx.done;
      return count;
    });
    if (removed > 0) emitChange();
    return removed;
  },

  // False when the browser has no usable IndexedDB (private mode, blocked, policy).
  async available(): Promise<boolean> {
    try {
      await withDb(async () => undefined);
      return true;
    } catch {
      return false;
    }
  }
};
