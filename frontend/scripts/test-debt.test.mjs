import { describe, expect, it } from "vitest";
import { compare, isBoundaryMock, measureFile } from "./test-debt.mjs";

describe("test-debt ratchet", () => {
  it("treats services, the HTTP client and packages as allowed mock boundaries", () => {
    for (const target of ["../../services/apiClient", "./services/HplcMethodService", "../services/masterDataOptions", "./documentControlService", "sonner", "recharts", "@mui/x-charts"]) {
      expect(isBoundaryMock(target)).toBe(true);
    }
  });

  it("treats project components, hooks and contexts as violations", () => {
    for (const target of ["../../hooks/useTestDefinitions", "../components/SignatureDialog", "../../context/AuthContext", "./tabs/ControlChartTab"]) {
      expect(isBoundaryMock(target)).toBe(false);
    }
  });

  it("counts each metric only where it applies", () => {
    const test = `
      vi.mock("../../services/apiClient");
      vi.mock("../hooks/useThing", () => ({}));
      fireEvent.click(a); fireEvent.change(b, x);
      const v: any = 1; const w = v as any;`;
    expect(measureFile("src/a/Thing.test.tsx", test)).toEqual({
      fireEventCalls: 2, projectModuleMocks: 1, anyInSource: 0, anyInTests: 2
    });

    const source = `export function f(x: any): any[] { return [x as any]; } // fireEvent.click(no)`;
    expect(measureFile("src/a/thing.ts", source)).toEqual({
      fireEventCalls: 0, projectModuleMocks: 0, anyInSource: 3, anyInTests: 0
    });
  });

  it("does not count words that merely contain 'any'", () => {
    expect(measureFile("src/a.ts", "const many = company; // anything").anyInSource).toBe(0);
  });

  it("fails only on an increase, and names the metric", () => {
    const baseline = { fireEventCalls: 10, projectModuleMocks: 5, anyInSource: 3, anyInTests: 1 };
    expect(compare({ fireEventCalls: 10, projectModuleMocks: 4, anyInSource: 3, anyInTests: 0 }, baseline)).toEqual([]);
    expect(compare({ fireEventCalls: 11, projectModuleMocks: 5, anyInSource: 3, anyInTests: 1 }, baseline))
      .toEqual([{ metric: "fireEventCalls", baseline: 10, current: 11 }]);
  });
});
