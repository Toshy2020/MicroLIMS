import { act, renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../contexts/AuthContext";
import { draftStore } from "../services/drafts/draftStore";
import { makeDraft, resetDraftDb } from "../services/drafts/draftTestUtils";
import { useBenchDraft } from "./useBenchDraft";

vi.mock("../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

interface Form {
  colonies: string;
  note: string;
  password: string;
}

const EMPTY: Form = { colonies: "", note: "", password: "" };
const pick = (v: Form): Partial<Form> => ({ colonies: v.colonies, note: v.note });

function signIn(userId: number) {
  localStorage.setItem("microlims_token", "h.e30=.s");
  localStorage.setItem("microlims_user_id", String(userId));
}

function wrapper({ children }: { children: ReactNode }) {
  return (
    <AuthProvider>
      <MemoryRouter>{children}</MemoryRouter>
    </AuthProvider>
  );
}

interface Props {
  values: Form;
  recordKey?: string;
  recordVersion?: number | null;
  enabled?: boolean;
}

function renderDraft(initial: Props) {
  return renderHook(
    (p: Props) =>
      useBenchDraft<Form>({
        formKey: "count-entry",
        recordKey: p.recordKey ?? "plate-1",
        recordVersion: p.recordVersion === undefined ? 3 : p.recordVersion,
        title: "Plate 1 count",
        values: p.values,
        pick,
        enabled: p.enabled ?? true
      }),
    { wrapper, initialProps: initial }
  );
}

function storedDraft(userId = 7, recordKey = "plate-1") {
  return draftStore.get<Partial<Form>>(userId, "count-entry", recordKey);
}

async function wait(ms: number) {
  await act(async () => {
    await new Promise((r) => setTimeout(r, ms));
  });
}

beforeEach(async () => {
  localStorage.clear();
  await resetDraftDb();
  signIn(7);
});
afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  localStorage.clear();
});

describe("useBenchDraft", () => {
  it("saves picked fields only after a change", async () => {
    const { rerender } = renderDraft({ values: EMPTY });
    await wait(600);
    expect(await storedDraft()).toBeUndefined();

    rerender({ values: { ...EMPTY, colonies: "42" } });
    await waitFor(async () => expect((await storedDraft())?.values).toEqual({ colonies: "42", note: "" }));
    const saved = await storedDraft();
    expect(saved?.userId).toBe(7);
    expect(saved?.title).toBe("Plate 1 count");
    expect(saved?.state).toBe("open");
  });

  it("fields outside pick() are never stored", async () => {
    const { rerender } = renderDraft({ values: EMPTY });
    rerender({ values: { colonies: "5", note: "n", password: "Sup3rSecret!" } });

    await waitFor(async () => expect(await storedDraft()).toBeDefined());
    const saved = await storedDraft();
    expect(Object.keys(saved?.values ?? {}).sort()).toEqual(["colonies", "note"]);
    expect(JSON.stringify(saved)).not.toContain("Sup3rSecret!");
  });

  it("restored draft is offered, not applied, until acceptRestore", async () => {
    await draftStore.put(makeDraft({ userId: 7, values: { colonies: "17", note: "kept" } }));

    const { result } = renderDraft({ values: EMPTY });
    await waitFor(() => expect(result.current.restored).toEqual({ colonies: "17", note: "kept" }));
    expect(result.current.restoredStartedAt).toBe("2031-03-04T05:00:00.000Z");

    let offered: Partial<Form> | null = null;
    act(() => {
      offered = result.current.acceptRestore();
    });
    expect(offered).toEqual({ colonies: "17", note: "kept" });
    expect(result.current.restored).toBeNull();
  });

  it("does not overwrite an offered draft while the analyst has not chosen", async () => {
    await draftStore.put(makeDraft({ userId: 7, values: { colonies: "17", note: "kept" } }));
    const { result, rerender } = renderDraft({ values: EMPTY });
    await waitFor(() => expect(result.current.restored).not.toBeNull());

    rerender({ values: { ...EMPTY, colonies: "99" } });
    await wait(700);

    expect((await storedDraft())?.values).toEqual({ colonies: "17", note: "kept" });
  });

  it("draft for another record or another user is not offered", async () => {
    await draftStore.put(makeDraft({ userId: 7, recordKey: "plate-2", values: { colonies: "1" } }));
    await draftStore.put(makeDraft({ userId: 8, recordKey: "plate-1", values: { colonies: "2" } }));

    const { result } = renderDraft({ values: EMPTY });
    await waitFor(() => expect(result.current.storageAvailable).toBe(true));
    await wait(50);

    expect(result.current.restored).toBeNull();
    expect(result.current.restoredStartedAt).toBeNull();
  });

  it("onSubmitted removes the draft and later renders do not recreate it", async () => {
    const { result, rerender } = renderDraft({ values: EMPTY });
    rerender({ values: { ...EMPTY, colonies: "8" } });
    await waitFor(async () => expect(await storedDraft()).toBeDefined());

    await act(async () => {
      await result.current.onSubmitted();
    });
    expect(await storedDraft()).toBeUndefined();

    rerender({ values: EMPTY });
    await wait(700);
    expect(await storedDraft()).toBeUndefined();
  });

  it("onRejected keeps it with state \"rejected\" and the reason", async () => {
    const { result, rerender } = renderDraft({ values: EMPTY });
    rerender({ values: { ...EMPTY, colonies: "8" } });
    await waitFor(async () => expect(await storedDraft()).toBeDefined());

    await act(async () => {
      await result.current.onRejected("This record was changed by someone else.");
    });

    const kept = await storedDraft();
    expect(kept?.state).toBe("rejected");
    expect(kept?.rejectReason).toBe("This record was changed by someone else.");
    expect(kept?.values).toEqual({ colonies: "8", note: "" });
  });

  it("discard removes the draft and clears the offer", async () => {
    await draftStore.put(makeDraft({ userId: 7, values: { colonies: "17" } }));
    const { result } = renderDraft({ values: EMPTY });
    await waitFor(() => expect(result.current.restored).not.toBeNull());

    await act(async () => {
      await result.current.discard();
    });

    expect(result.current.restored).toBeNull();
    expect(await storedDraft()).toBeUndefined();
  });

  it("submitHeaders empty under 5 min, present after", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2031-03-04T10:00:00.000Z"));
    const { result, rerender } = renderDraft({ values: EMPTY });
    expect(result.current.submitHeaders()).toEqual({});

    rerender({ values: { ...EMPTY, colonies: "1" } });
    expect(result.current.submitHeaders()).toEqual({});

    vi.setSystemTime(new Date("2031-03-04T10:04:59.000Z"));
    expect(result.current.submitHeaders()).toEqual({});

    vi.setSystemTime(new Date("2031-03-04T10:05:01.000Z"));
    expect(result.current.submitHeaders()).toEqual({ "X-Draft-Started-At": "2031-03-04T10:00:00.000Z" });
  });

  it("recordVersion stored is the one the form opened with, even if the prop changes later", async () => {
    const { rerender } = renderDraft({ values: EMPTY, recordVersion: 3 });
    rerender({ values: { ...EMPTY, colonies: "1" }, recordVersion: 3 });
    await waitFor(async () => expect(await storedDraft()).toBeDefined());

    rerender({ values: { ...EMPTY, colonies: "2" }, recordVersion: 4 });
    await waitFor(async () => expect((await storedDraft())?.values).toEqual({ colonies: "2", note: "" }));

    expect((await storedDraft())?.recordVersion).toBe(3);
  });

  it("a restored draft keeps the version it was started against and reports it for the save", async () => {
    await draftStore.put(makeDraft({ userId: 7, recordVersion: 2, values: { colonies: "17" } }));
    const { result } = renderDraft({ values: EMPTY, recordVersion: 5 });
    await waitFor(() => expect(result.current.restored).not.toBeNull());

    expect(result.current.draftRecordVersion).toBe(2);
  });

  it("storage unavailable → hook is inert and reports it", async () => {
    vi.spyOn(indexedDB, "open").mockImplementation(() => {
      throw new DOMException("blocked", "SecurityError");
    });
    const { result, rerender } = renderDraft({ values: EMPTY });
    await waitFor(() => expect(result.current.storageAvailable).toBe(false));

    rerender({ values: { ...EMPTY, colonies: "1" } });
    await wait(600);

    expect(result.current.restored).toBeNull();
    expect(result.current.submitHeaders()).toEqual({});
    await expect(result.current.onSubmitted()).resolves.toBeUndefined();
  });

  it("is inert when not enabled", async () => {
    const { rerender } = renderDraft({ values: EMPTY, enabled: false });
    rerender({ values: { ...EMPTY, colonies: "1" }, enabled: false });
    await wait(600);
    expect(await draftStore.listForUser(7)).toEqual([]);
  });

  it("a form that is discarded, reset and reopened does not save the blank form", async () => {
    const { result, rerender } = renderDraft({ values: EMPTY });
    rerender({ values: { ...EMPTY, colonies: "9" } });
    await waitFor(async () => expect(await storedDraft()).toBeDefined());

    await act(async () => {
      await result.current.discard();
    });
    // The form closes and is reset in the same step, then reopens.
    rerender({ values: EMPTY, enabled: false });
    rerender({ values: EMPTY, enabled: true });
    await wait(700);

    expect(await storedDraft()).toBeUndefined();
  });
});
