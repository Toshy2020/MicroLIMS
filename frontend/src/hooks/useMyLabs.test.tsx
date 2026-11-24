import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { resetSharedMyLabsRequests, useMyLabs } from "./useMyLabs";

const auth = { role: "Analyst", permissions: [] as string[], username: "sec.head" as string | null };
vi.mock("../contexts/AuthContext", () => ({ useAuth: () => auth }));

const getMySections = vi.fn();
vi.mock("../services/laboratorySectionService", () => ({
  laboratorySectionService: { getMySections: (...a: unknown[]) => getMySections(...a) }
}));

describe("useMyLabs", () => {
  beforeEach(() => {
    getMySections.mockReset();
    resetSharedMyLabsRequests();
    auth.role = "Analyst";
    auth.permissions = [];
    auth.username = "sec.head";
    window.localStorage.clear();
  });

  it("shows the last known laboratories on the first render while the fresh answer loads, then replaces them", async () => {
    window.localStorage.setItem("microlims.myLabs.v2:sec.head", JSON.stringify({ savedAt: Date.now(), labs: [{ sectionCode: "MICRO", physchemAreas: [] }] }));
    getMySections.mockReturnValue(new Promise(() => {}));
    const { result } = renderHook(() => useMyLabs());
    expect(result.current.loading).toBe(true);
    expect(result.current.codes).toEqual(["MICRO"]);

    resetSharedMyLabsRequests(); // the first request above never settles; start a new load
    getMySections.mockResolvedValue([{ sectionCode: "FP", physchemAreas: [] }]);
    const second = renderHook(() => useMyLabs());
    await waitFor(() => expect(second.result.current.codes).toEqual(["FP"]));
    expect(JSON.parse(window.localStorage.getItem("microlims.myLabs.v2:sec.head") ?? "{}").labs).toEqual([{ sectionCode: "FP", physchemAreas: [] }]);
  });

  // The server answers Sections.ViewAll with every section for reading; the hook mirrors the permission, not the role.
  it("ignores and removes a corrupt, malformed or expired cache entry", () => {
    getMySections.mockReturnValue(new Promise(() => {}));
    const key = "microlims.myLabs.v2:sec.head";
    for (const bad of ["{not json", JSON.stringify({ savedAt: Date.now(), labs: [null] }), JSON.stringify({ savedAt: Date.now() - 13 * 3600 * 1000, labs: [{ sectionCode: "FP" }] }), JSON.stringify([{ sectionCode: "FP" }])]) {
      window.localStorage.setItem(key, bad);
      const { result, unmount } = renderHook(() => useMyLabs());
      expect(result.current.codes).toEqual([]);
      unmount();
    }
  });

  it("drops the kept answer and shows no laboratories when the fresh request fails", async () => {
    window.localStorage.setItem("microlims.myLabs.v2:sec.head", JSON.stringify({ savedAt: Date.now(), labs: [{ sectionCode: "MICRO", physchemAreas: [] }] }));
    getMySections.mockRejectedValue(new Error("403"));
    const { result } = renderHook(() => useMyLabs());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.codes).toEqual([]);
    expect(window.localStorage.getItem("microlims.myLabs.v2:sec.head")).toBeNull();
  });

  it("does not show one user's kept laboratories to another user", () => {
    window.localStorage.setItem("microlims.myLabs.v2:sec.head", JSON.stringify({ savedAt: Date.now(), labs: [{ sectionCode: "MICRO", physchemAreas: [] }] }));
    getMySections.mockReturnValue(new Promise(() => {}));
    auth.username = "someone.else";
    const { result } = renderHook(() => useMyLabs());
    expect(result.current.codes).toEqual([]);
  });

  it("shares one request between components that ask at the same time, and asks again on the next load", async () => {
    getMySections.mockResolvedValue([{ sectionCode: "FP", physchemAreas: [] }]);
    const a = renderHook(() => useMyLabs());
    const b = renderHook(() => useMyLabs());
    await waitFor(() => expect(a.result.current.loading).toBe(false));
    await waitFor(() => expect(b.result.current.loading).toBe(false));
    expect(getMySections).toHaveBeenCalledTimes(1);

    const c = renderHook(() => useMyLabs());
    await waitFor(() => expect(c.result.current.loading).toBe(false));
    expect(getMySections).toHaveBeenCalledTimes(2);
  });

  it("shows both laboratories to a user holding Sections.ViewAll, whatever the role and whatever the endpoint returns", async () => {
    getMySections.mockResolvedValue([]);
    auth.role = "Analyst";
    auth.permissions = ["Sections.ViewAll"];

    const { result } = renderHook(() => useMyLabs());
    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.codes).toEqual(["MICRO", "FP"]);
    expect(result.current.physchemAreas).toEqual(["fp", "rmpm"]);
  });

  it("does not give a System Administrator every laboratory once the role no longer holds Sections.ViewAll", async () => {
    getMySections.mockResolvedValue([{ sectionCode: "MICRO", physchemAreas: [] }]);
    auth.role = "SystemAdministrator";
    auth.permissions = ["Users.Manage", "Roles.Manage"];

    const { result } = renderHook(() => useMyLabs());
    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.codes).toEqual(["MICRO"]);
    expect(result.current.physchemAreas).toEqual([]);
  });

  it("follows the server's sections and physicochemical areas for an ordinary member", async () => {
    getMySections.mockResolvedValue([{ sectionCode: "FP", physchemAreas: ["rmpm"] }]);

    const { result } = renderHook(() => useMyLabs());
    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.codes).toEqual(["FP"]);
    expect(result.current.physchemAreas).toEqual(["rmpm"]);
  });

  it("falls back to nothing when the request fails and the user holds no Sections.ViewAll", async () => {
    getMySections.mockRejectedValue(new Error("boom"));

    const { result } = renderHook(() => useMyLabs());
    await waitFor(() => expect(result.current.loading).toBe(false));

    expect(result.current.codes).toEqual([]);
    expect(result.current.labs).toEqual([]);
  });

  it("does not write the cache when the user logs out before the answer arrives", async () => {
    let resolve!: (v: unknown) => void;
    getMySections.mockReturnValue(new Promise((r) => { resolve = r; }));
    const { rerender } = renderHook(() => useMyLabs());
    auth.username = null;
    rerender();
    resolve([{ sectionCode: "MICRO", physchemAreas: [] }]);
    await new Promise((r) => setTimeout(r, 20));
    expect(window.localStorage.getItem("microlims.myLabs.v2:sec.head")).toBeNull();
  });

  it("does not let a late, older request overwrite the cache written by a newer one", async () => {
    vi.useFakeTimers();
    try {
      let resolveOld!: (v: unknown) => void;
      getMySections.mockReturnValueOnce(new Promise((r) => { resolveOld = r; }));
      renderHook(() => useMyLabs());
      vi.setSystemTime(Date.now() + 20_000); // past the share window: a newer request starts
      getMySections.mockResolvedValueOnce([{ sectionCode: "FP", physchemAreas: [] }]);
      renderHook(() => useMyLabs());
      await vi.advanceTimersByTimeAsync(10);
      resolveOld([{ sectionCode: "MICRO", physchemAreas: [] }]);
      await vi.advanceTimersByTimeAsync(10);
    } finally {
      vi.useRealTimers();
    }
    const cached = JSON.parse(window.localStorage.getItem("microlims.myLabs.v2:sec.head") ?? "{}");
    expect(cached.labs).toEqual([{ sectionCode: "FP", physchemAreas: [] }]);
  });
});
