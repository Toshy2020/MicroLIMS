import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { NumericEntryField } from "./NumericEntryField";
import { BenchKeypad, RegistryBenchKeypad } from "./BenchKeypad";
import { renderWithTheme } from "./benchTestUtils";

afterEach(() => {
  cleanup();
  localStorage.clear();
});

function Form({ onNextA }: { onNextA?: () => void }) {
  const [a, setA] = useState("");
  const [b, setB] = useState("");
  return (
    <>
      <NumericEntryField label="Plate A" value={a} onChange={setA} kind="count" onNext={onNextA} />
      <NumericEntryField label="Plate B" value={b} onChange={setB} kind="decimal" />
      <RegistryBenchKeypad />
      <output data-testid="a">{a}</output>
      <output data-testid="b">{b}</output>
    </>
  );
}

function LimitedDecimal() {
  const [v, setV] = useState("");
  return (
    <>
      <NumericEntryField label="Reading" value={v} onChange={setV} kind="decimal" maxLength={2} />
      <RegistryBenchKeypad />
      <output data-testid="v">{v}</output>
    </>
  );
}

describe("BenchKeypad (props)", () => {
  it("calls the handlers and hides the decimal key unless allowed", async () => {
    const user = userEvent.setup();
    const onDigit = vi.fn();
    const onBackspace = vi.fn();
    const onNext = vi.fn();
    const onMinus = vi.fn();
    const { rerender } = renderWithTheme(<BenchKeypad onDigit={onDigit} onBackspace={onBackspace} onNext={onNext} allowDecimal={false} />, { bench: true });
    expect(screen.queryByRole("button", { name: "Decimal point" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Minus sign" })).toBeNull();
    await user.click(screen.getByRole("button", { name: "7" }));
    await user.click(screen.getByRole("button", { name: "Backspace" }));
    await user.click(screen.getByRole("button", { name: "Next" }));
    expect(onDigit).toHaveBeenCalledWith("7");
    expect(onBackspace).toHaveBeenCalledTimes(1);
    expect(onNext).toHaveBeenCalledTimes(1);
    rerender(<BenchKeypad onDigit={onDigit} onBackspace={onBackspace} onNext={onNext} onMinus={onMinus} allowDecimal />);
    expect(screen.getByRole("button", { name: "Decimal point" })).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Minus sign" }));
    expect(onMinus).toHaveBeenCalledTimes(1);
  });

  it("keys are at least 48 px with 8 px gaps and have accessible names", () => {
    renderWithTheme(<BenchKeypad onDigit={() => {}} onBackspace={() => {}} onNext={() => {}} onMinus={() => {}} allowDecimal />, { bench: true });
    const keys = screen.getAllByRole("button");
    expect(keys.length).toBe(14);
    for (const key of keys) {
      const style = getComputedStyle(key);
      expect(parseInt(style.minHeight, 10)).toBeGreaterThanOrEqual(48);
      expect(parseInt(style.minWidth, 10)).toBeGreaterThanOrEqual(48);
      expect((key.getAttribute("aria-label") ?? key.textContent ?? "").length).toBeGreaterThan(0);
    }
    const group = screen.getByRole("group", { name: "Number keypad" });
    expect(parseInt(getComputedStyle(group).gap, 10)).toBeGreaterThanOrEqual(8);
  });
});

describe("RegistryBenchKeypad", () => {
  it("digits go to the focused field and Backspace removes one", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Form />, { bench: true });
    await user.click(screen.getByLabelText("Plate A"));
    await user.click(screen.getByRole("button", { name: "1" }));
    await user.click(screen.getByRole("button", { name: "2" }));
    await user.click(screen.getByRole("button", { name: "5" }));
    expect(screen.getByTestId("a").textContent).toBe("125");
    await user.click(screen.getByRole("button", { name: "Backspace" }));
    expect(screen.getByTestId("a").textContent).toBe("12");
    expect(screen.getByTestId("b").textContent).toBe("");
  });

  it("does not take focus from the field", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Form />, { bench: true });
    const a = screen.getByLabelText("Plate A");
    await user.click(a);
    await user.click(screen.getByRole("button", { name: "3" }));
    expect(document.activeElement).toBe(a);
  });

  it("Next calls onNext and moves focus to the next field in DOM order", async () => {
    const user = userEvent.setup();
    const onNextA = vi.fn();
    renderWithTheme(<Form onNextA={onNextA} />, { bench: true });
    await user.click(screen.getByLabelText("Plate A"));
    await user.click(screen.getByRole("button", { name: "Next" }));
    expect(onNextA).toHaveBeenCalledTimes(1);
    expect(document.activeElement).toBe(screen.getByLabelText("Plate B"));
    await user.click(screen.getByRole("button", { name: "9" }));
    expect(screen.getByTestId("b").textContent).toBe("9");
  });

  it("offers the decimal key only for a decimal field", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Form />, { bench: true });
    await user.click(screen.getByLabelText("Plate A"));
    expect(screen.queryByRole("button", { name: "Decimal point" })).toBeNull();
    await user.click(screen.getByLabelText("Plate B"));
    await user.click(screen.getByRole("button", { name: "Decimal point" }));
    expect(screen.getByTestId("b").textContent).toBe(".");
  });

  it("offers a Minus sign key only for a decimal field and toggles a leading minus", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Form />, { bench: true });
    await user.click(screen.getByLabelText("Plate A"));
    expect(screen.queryByRole("button", { name: "Minus sign" })).toBeNull();
    await user.click(screen.getByLabelText("Plate B"));
    await user.click(screen.getByRole("button", { name: "2" }));
    await user.click(screen.getByRole("button", { name: "0" }));
    await user.click(screen.getByRole("button", { name: "Decimal point" }));
    await user.click(screen.getByRole("button", { name: "5" }));
    await user.click(screen.getByRole("button", { name: "Minus sign" }));
    expect(screen.getByTestId("b").textContent).toBe("-20.5");
    expect(document.activeElement).toBe(screen.getByLabelText("Plate B"));
    await user.click(screen.getByRole("button", { name: "Minus sign" }));
    expect(screen.getByTestId("b").textContent).toBe("20.5");
  });

  it("the Minus sign key respects the field's maximum length", async () => {
    const user = userEvent.setup();
    renderWithTheme(<LimitedDecimal />, { bench: true });
    await user.click(screen.getByLabelText("Reading"));
    await user.click(screen.getByRole("button", { name: "1" }));
    await user.click(screen.getByRole("button", { name: "2" }));
    await user.click(screen.getByRole("button", { name: "Minus sign" }));
    expect(screen.getByTestId("v").textContent).toBe("12");
  });

  it("is absent outside bench mode", () => {
    renderWithTheme(<Form />, { bench: false });
    expect(screen.queryByRole("group", { name: "Number keypad" })).toBeNull();
  });

  it("is hidden until a numeric field has focus, then hidden again on blur", async () => {
    const user = userEvent.setup();
    renderWithTheme(
      <>
        <input aria-label="Free text" />
        <Form />
      </>,
      { bench: true }
    );
    expect(screen.queryByRole("group", { name: "Number keypad" })).toBeNull();
    await user.click(screen.getByLabelText("Plate A"));
    expect(screen.getByRole("group", { name: "Number keypad" })).toBeTruthy();
    await user.click(screen.getByLabelText("Free text"));
    expect(screen.queryByRole("group", { name: "Number keypad" })).toBeNull();
    await user.click(screen.getByLabelText("Plate B"));
    expect(screen.getByRole("group", { name: "Number keypad" })).toBeTruthy();
    await user.tab();
    expect(screen.queryByRole("group", { name: "Number keypad" })).toBeNull();
  });

  it("hardware typing still works with the keypad present", async () => {
    const user = userEvent.setup();
    renderWithTheme(<Form />, { bench: true });
    await user.type(screen.getByLabelText("Plate A"), "42");
    await user.click(screen.getByRole("button", { name: "7" }));
    expect(screen.getByTestId("a").textContent).toBe("427");
  });
});
