import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { draftId, draftStore } from "./draftStore";
import { makeDraft, resetDraftDb } from "./draftTestUtils";

beforeEach(resetDraftDb);
afterEach(() => vi.restoreAllMocks());

describe("draftStore", () => {
  it("put/get round trip", async () => {
    const draft = makeDraft({ userId: 7, values: { colonies: "12" } });
    await draftStore.put(draft);

    expect(await draftStore.get(7, "count-entry", "plate-1")).toEqual(draft);
    expect(draft.id).toBe(draftId(7, "count-entry", "plate-1"));
    expect(await draftStore.get(7, "count-entry", "other-plate")).toBeUndefined();
  });

  it("listForUser returns only that user's drafts, and get never returns another user's draft", async () => {
    await draftStore.put(makeDraft({ userId: 1, recordKey: "a", values: { v: "mine" } }));
    await draftStore.put(makeDraft({ userId: 2, recordKey: "b", values: { v: "theirs" } }));
    await draftStore.put(makeDraft({ userId: 2, recordKey: "a", values: { v: "theirs too" } }));

    const mine = await draftStore.listForUser(1);
    expect(mine.map((d) => d.recordKey)).toEqual(["a"]);
    expect(mine.every((d) => d.userId === 1)).toBe(true);

    expect(await draftStore.get(1, "count-entry", "b")).toBeUndefined();
    expect(await draftStore.listForUser(3)).toEqual([]);
  });

  it("countOtherUsers returns a count and no values", async () => {
    await draftStore.put(makeDraft({ userId: 1, recordKey: "a", values: { v: "mine" } }));
    await draftStore.put(makeDraft({ userId: 2, recordKey: "b", values: { v: "secret" } }));
    await draftStore.put(makeDraft({ userId: 3, recordKey: "c", values: { v: "secret" } }));

    const count = await draftStore.countOtherUsers(1);

    expect(count).toBe(2);
    expect(typeof count).toBe("number");
  });

  it("purge removes drafts older than the retention and reports how many", async () => {
    const now = new Date("2031-03-05T12:00:00.000Z");
    await draftStore.put(makeDraft({ userId: 1, recordKey: "old", values: {}, updatedAtDevice: "2031-03-04T11:59:00.000Z" }));
    await draftStore.put(makeDraft({ userId: 2, recordKey: "older", values: {}, updatedAtDevice: "2031-03-01T00:00:00.000Z" }));
    await draftStore.put(makeDraft({ userId: 1, recordKey: "fresh", values: {}, updatedAtDevice: "2031-03-04T12:30:00.000Z" }));

    const removed = await draftStore.purgeOlderThan(24, now);

    expect(removed).toBe(2);
    expect((await draftStore.listForUser(1)).map((d) => d.recordKey)).toEqual(["fresh"]);
    expect(await draftStore.listForUser(2)).toEqual([]);
  });

  it("remove deletes one draft", async () => {
    const draft = makeDraft({ userId: 1, values: {} });
    await draftStore.put(draft);
    await draftStore.remove(draft.id);
    expect(await draftStore.get(1, draft.formKey, draft.recordKey)).toBeUndefined();
  });

  it("available() false when IndexedDB throws", async () => {
    expect(await draftStore.available()).toBe(true);
    vi.spyOn(indexedDB, "open").mockImplementation(() => {
      throw new DOMException("blocked", "SecurityError");
    });
    expect(await draftStore.available()).toBe(false);
  });
});
