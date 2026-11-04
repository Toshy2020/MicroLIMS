import { renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useMyLabs } from "./useMyLabs";

const auth = { role: "Analyst", permissions: [] as string[] };
vi.mock("../contexts/AuthContext", () => ({ useAuth: () => auth }));

const getMySections = vi.fn();
vi.mock("../services/laboratorySectionService", () => ({
  laboratorySectionService: { getMySections: (...a: unknown[]) => getMySections(...a) }
}));

describe("useMyLabs", () => {
  beforeEach(() => {
    getMySections.mockReset();
    auth.role = "Analyst";
    auth.permissions = [];
  });

  // The server answers Sections.ViewAll with every section for reading; the hook mirrors the permission, not the role.
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
});
