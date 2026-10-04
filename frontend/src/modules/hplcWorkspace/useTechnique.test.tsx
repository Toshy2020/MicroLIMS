import { describe, expect, it } from "vitest";
import { renderHook } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { ReactNode } from "react";
import { useTechnique, getTechniqueRoutes } from "./useTechnique";

describe("useTechnique", () => {
  it("resolves GC context when location is under /gc-workspace", () => {
    const wrapper = ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={["/gc-workspace/5/run/10"]}>
        {children}
      </MemoryRouter>
    );

    const { result } = renderHook(() => useTechnique(), { wrapper });

    expect(result.current.technique).toBe("Gc");
    expect(result.current.label).toBe("GC");
    expect(result.current.basePath).toBe("/gc-workspace");
    expect(result.current.routes.instrument(5)).toBe("/gc-workspace/5");
    expect(result.current.routes.run(5, 10)).toBe("/gc-workspace/5/run/10");
    expect(result.current.routes.newRun(5)).toBe("/gc-workspace/5/new-run");
  });

  it("resolves HPLC context by default when location is under /hplc-workspace", () => {
    const wrapper = ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={["/hplc-workspace/2/run/4/samples"]}>
        {children}
      </MemoryRouter>
    );

    const { result } = renderHook(() => useTechnique(), { wrapper });

    expect(result.current.technique).toBe("Hplc");
    expect(result.current.label).toBe("HPLC");
    expect(result.current.basePath).toBe("/hplc-workspace");
    expect(result.current.routes.instrument(2)).toBe("/hplc-workspace/2");
    expect(result.current.routes.run(2, 4)).toBe("/hplc-workspace/2/run/4");
    expect(result.current.routes.history(2)).toBe("/hplc-workspace/2/history");
  });

  it("getTechniqueRoutes maps all sub-routes accurately", () => {
    const routes = getTechniqueRoutes("/gc-workspace");
    expect(routes.root).toBe("/gc-workspace");
    expect(routes.sst(1, 2)).toBe("/gc-workspace/1/run/2/sst");
    expect(routes.samples(1, 2)).toBe("/gc-workspace/1/run/2/samples");
    expect(routes.evidence(1, 2)).toBe("/gc-workspace/1/run/2/evidence");
    expect(routes.sampleEntry(1, 2, 3)).toBe("/gc-workspace/1/run/2/sample/3");
    expect(routes.qualificationEntry(1, 2, 3)).toBe("/gc-workspace/1/run/2/qualification/3");
  });
});
