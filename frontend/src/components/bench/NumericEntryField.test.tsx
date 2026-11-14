import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NumericEntryField, parseCount, parseDecimal } from "./NumericEntryField";
import { renderWithTheme } from "./benchTestUtils";

afterEach(() => {
  cleanup();
  localStorage.clear();
});

function Harness({ kind, initial = "", onNext, maxLength }: { kind: "count" | "decimal"; initial?: string; onNext?: () => void; maxLength?: number }) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <NumericEntryField label="Plate 1" value={value} onChange={setValue} kind={kind} onNext={onNext} maxLength={maxLength} />
      <output data-testid="value">{value}</output>
    </>
  );
}

describe("parseCount / parseDecimal", () => {
  it("parseCount returns null unless the text is only digits", () => {
    expect(parseCount("250")).toBe(250);
    expect(parseCount("0")).toBe(0);
    for (const bad of ["1.5", "-1", "1e3", " 12", "", "abc"]) expect(parseCount(bad)).toBeNull();
  });
  it("parseDecimal accepts digits with one optional fraction", () => {
    expect(parseDecimal("0.5")).toBe(0.5);
    expect(parseDecimal("12")).toBe(12);
    for (const bad of ["1.2.3", "-1", ".5", "1.", "1e3", " 1", "x"]) expect(parseDecimal(bad)).toBeNull();
  });
});

describe("NumericEntryField", () => {
  it.each([["1.5"], ["-1"], ["1e3"], [" 12"], ["abc"]])("count rejects %j with an alert", async (text) => {
    const user = userEvent.setup();
    renderWithTheme(<Harness kind="count" />, { bench: false });
    await user.type(screen.getByLabelText("Plate 1"), text);
    expect(screen.getByRole("alert").textContent).toMatch(/whole number/i);
  });

  it.each([["0"], ["250"]])("count accepts %j", async (text) => {
    const user = userEvent.setup();
    renderWithTheme(<Harness kind="count" />, { bench: false });
    await user.type(screen.getByLabelText("Plate 1"), text);
    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("decimal accepts 0.5 and rejects 1.2.3", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Harness kind="decimal" />, { bench: false });
    const input = screen.getByLabelText("Plate 1");
    await user.type(input, "0.5");
    expect(screen.queryByRole("alert")).toBeNull();
    await user.clear(input);
    await user.type(input, "1.2.3");
    expect(screen.getByRole("alert").textContent).toMatch(/number/i);
  });

  it("passes the value through as typed, with no rounding or leading-zero trimming", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Harness kind="decimal" />, { bench: false });
    await user.type(screen.getByLabelText("Plate 1"), "007.500");
    expect(screen.getByTestId("value").textContent).toBe("007.500");
  });

  it("shows a server-supplied error as an alert", () => {
    renderWithTheme(
      <NumericEntryField label="Plate 1" value="5" onChange={() => {}} kind="count" error="Count out of range" />,
      { bench: false }
    );
    expect(screen.getByRole("alert").textContent).toBe("Count out of range");
  });

  it("uses numeric / decimal input modes", () => {
    renderWithTheme(
      <>
        <NumericEntryField label="A" value="" onChange={() => {}} kind="count" />
        <NumericEntryField label="B" value="" onChange={() => {}} kind="decimal" />
      </>,
      { bench: false }
    );
    expect(screen.getByLabelText("A").getAttribute("inputmode")).toBe("numeric");
    expect(screen.getByLabelText("B").getAttribute("inputmode")).toBe("decimal");
  });

  it("selects all text on focus", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Harness kind="count" initial="123" />, { bench: false });
    const input = screen.getByLabelText("Plate 1") as HTMLInputElement;
    await user.click(input);
    expect(input.selectionStart).toBe(0);
    expect(input.selectionEnd).toBe(3);
  });

  it("limits length to 7 by default and honours maxLength", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Harness kind="count" />, { bench: false });
    await user.type(screen.getByLabelText("Plate 1"), "123456789");
    expect(screen.getByTestId("value").textContent).toBe("1234567");
    cleanup();
    renderWithTheme(<Harness kind="count" maxLength={3} />, { bench: false });
    await user.type(screen.getByLabelText("Plate 1"), "123456");
    expect(screen.getByTestId("value").textContent).toBe("123");
  });

  it("Enter calls onNext", async () => {
    const user = userEvent.setup();
    const onNext = vi.fn();
    renderWithTheme(<Harness kind="count" onNext={onNext} />, { bench: false });
    await user.type(screen.getByLabelText("Plate 1"), "4{Enter}");
    expect(onNext).toHaveBeenCalledTimes(1);
  });
});
