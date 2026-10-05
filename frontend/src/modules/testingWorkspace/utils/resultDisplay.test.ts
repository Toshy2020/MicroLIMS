import { describe, expect, it } from "vitest";
import { decimalsOf, withoutUnit } from "./resultDisplay";

describe("withoutUnit", () => {
  it("drops a trailing unit shown in its own column", () => expect(withoutUnit("99.88 %", "%")).toBe("99.88"));
  it("keeps text whose unit differs", () => expect(withoutUnit("496.16 mg", "%")).toBe("496.16 mg"));
  it("keeps text when there is no unit", () => expect(withoutUnit("Complies", null)).toBe("Complies"));
});

describe("decimalsOf", () => {
  it("counts decimals of the reported value", () => expect(decimalsOf("99.88 %")).toBe(2));
  it("handles integers and three decimals", () => {
    expect(decimalsOf("100 %")).toBe(0);
    expect(decimalsOf("0.125")).toBe(3);
  });
  it("defaults to 2 without a number", () => expect(decimalsOf("Not detected")).toBe(2));
});
