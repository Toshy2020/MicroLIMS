import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DateField } from "./DateField";
import { changeDateField } from "./DateField.testUtils";

afterEach(cleanup);

function Controlled() {
  const [v, setV] = useState("");
  return <DateField label="Mfg" value={v} onChange={setV} />;
}

describe("DateField incomplete date feedback", () => {
  it("reports a complete date and shows no incomplete-date message", () => {
    const onChange = vi.fn();
    render(<DateField label="Mfg" value="" onChange={onChange} />);
    changeDateField("Mfg", "2026-03-15");
    expect(onChange).toHaveBeenLastCalledWith("2026-03-15");
    expect(screen.queryByText(/complete date/i)).toBeNull();
  });

  async function typeIntoSection(label: string, keys: string) {
    const section = screen.getByRole("spinbutton", { name: label });
    await userEvent.click(section);
    await userEvent.keyboard(keys);
  }
  const leaveField = () => userEvent.click(document.body);

  it("shows a message instead of silently dropping a date with the day left out", async () => {
    const onChange = vi.fn();
    render(<DateField label="Mfg" value="" onChange={onChange} />);
    await typeIntoSection("Month", "03");
    await typeIntoSection("Year", "2026");
    await leaveField();
    expect(onChange).not.toHaveBeenCalledWith("2026-03-01");
    expect(screen.getByText(/complete date/i)).toBeTruthy();
  });

  it("clears the message once the missing part is filled in", async () => {
    render(<Controlled />);
    await typeIntoSection("Month", "03");
    await leaveField();
    expect(screen.getByText(/complete date/i)).toBeTruthy();
    await typeIntoSection("Day", "15");
    await typeIntoSection("Year", "2026");
    await leaveField();
    expect(screen.queryByText(/complete date/i)).toBeNull();
  });

  it("reports a half-typed date while still focused, before any blur (Enter-to-submit)", async () => {
    const onIncompleteChange = vi.fn();
    render(<DateField label="Mfg" value="" onChange={() => {}} onIncompleteChange={onIncompleteChange} />);
    await typeIntoSection("Month", "03");
    expect(onIncompleteChange).toHaveBeenLastCalledWith(true);
    expect(screen.getByText(/complete date/i)).toBeTruthy();
  });

  it("reports incomplete when one section of a complete date is erased", async () => {
    const onIncompleteChange = vi.fn();
    function Holder() {
      const [v, setV] = useState("2026-03-15");
      return <DateField label="Mfg" value={v} onChange={setV} onIncompleteChange={onIncompleteChange} />;
    }
    render(<Holder />);
    await userEvent.click(screen.getByRole("spinbutton", { name: "Day" }));
    await userEvent.keyboard("{Backspace}");
    expect(onIncompleteChange).toHaveBeenLastCalledWith(true);
    expect(onIncompleteChange).not.toHaveBeenCalledWith(false);
    expect(screen.getByText(/complete date/i)).toBeTruthy();
  });

  it("clears the incomplete state when a complete date is set from outside", async () => {
    const onIncompleteChange = vi.fn();
    const { rerender } = render(<DateField label="Mfg" value="" onChange={() => {}} onIncompleteChange={onIncompleteChange} />);
    await typeIntoSection("Month", "03");
    expect(onIncompleteChange).toHaveBeenLastCalledWith(true);
    rerender(<DateField label="Mfg" value="2026-03-15" onChange={() => {}} onIncompleteChange={onIncompleteChange} />);
    expect(onIncompleteChange).toHaveBeenLastCalledWith(false);
    expect(screen.queryByText(/complete date/i)).toBeNull();
  });
});
