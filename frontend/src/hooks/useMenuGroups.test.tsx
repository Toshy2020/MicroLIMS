import { renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { useMenuGroups } from "./useMenuGroups";

vi.mock("../contexts/AuthContext", () => ({
  useAuth: () => ({ role: "Analyst", permissions: [] })
}));
vi.mock("../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: () => Promise.resolve([{ sectionCode: "FP", physchemAreas: ["rmpm"] }])
  }
}));

describe("useMenuGroups", () => {
  it("does not offer the FP Workspace to an rmpm-only user", async () => {
    const { result } = renderHook(() => useMenuGroups());
    const labels = () =>
      result.current.flatMap((g) => g.items.flatMap((i) => i.children?.map((c) => c.label) ?? []));
    await waitFor(() => expect(labels()).toContain("RM & PM Workspace"));
    expect(labels()).not.toContain("FP Workspace");
  });
});
