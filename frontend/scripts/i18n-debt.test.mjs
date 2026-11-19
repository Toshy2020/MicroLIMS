import { describe, expect, it } from "vitest";
import { compare, measureText, moduleOf, totalsByModule } from "./i18n-debt.mjs";

describe("i18n debt ratchet", () => {
  it("names the module a file belongs to", () => {
    expect(moduleOf("src/modules/myWork/MyWork.tsx")).toBe("modules/myWork");
    expect(moduleOf("src/modules/myWork/tabs/Deep.tsx")).toBe("modules/myWork");
    expect(moduleOf("src/components/EmptyState.tsx")).toBe("components");
    expect(moduleOf("src/layouts/deep/Thing.tsx")).toBe("layouts");
    expect(moduleOf("src/App.tsx")).toBe("(root)");
  });

  it("counts hard-coded JSX text and visible props, not code or styling strings", async () => {
    const source = `
      export function A({ n }: { n: number }) {
        return (
          <section aria-label="Sample list" data-testid="a">
            <h1>Samples</h1>
            <Button variant="contained" onClick={() => go("save")} sx={{ display: "flex" }}>Save</Button>
            <TextField label="Sample number" placeholder="Scan here" helperText={\`Row \${n}\`} />
            <p>{n}</p>
            <p>{t("common:actions.save")}</p>
          </section>
        );
      }`;
    // aria-label, h1, Button text, label, placeholder, helperText template
    expect(await measureText("src/modules/myWork/A.tsx", source)).toBe(6);
  });

  it("counts nothing in tests or plain .ts files", async () => {
    expect(await measureText("src/modules/myWork/A.test.tsx", "export const x = <p>Hello world</p>;")).toBe(0);
    expect(await measureText("src/modules/myWork/a.ts", "export const x = 'Hello world';")).toBe(0);
  });

  it("totals per module", () => {
    expect(totalsByModule([
      { file: "src/modules/a/X.tsx", count: 2 },
      { file: "src/modules/a/Y.tsx", count: 3 },
      { file: "src/components/Z.tsx", count: 1 },
      { file: "src/components/Q.tsx", count: 0 }
    ])).toEqual({ "modules/a": 5, components: 1 });
  });

  it("fails on any module that rises or appears, and passes when counts fall", () => {
    const baseline = { total: 10, modules: { "modules/a": 6, components: 4 } };
    expect(compare({ total: 8, modules: { "modules/a": 5, components: 3 } }, baseline)).toEqual([]);
    expect(compare({ total: 11, modules: { "modules/a": 7, components: 4 } }, baseline))
      .toEqual([{ module: "modules/a", baseline: 6, current: 7 }]);
    expect(compare({ total: 11, modules: { "modules/a": 6, components: 4, "modules/new": 1 } }, baseline))
      .toEqual([{ module: "modules/new", baseline: 0, current: 1 }]);
  });
});
