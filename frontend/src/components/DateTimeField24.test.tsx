import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { DateTimeField24 } from "./DateTimeField24";

function Harness({ initial }: { initial: string }) {
  const [value, setValue] = useState(initial);
  return (
    <PinnedLightTheme>
      <DateTimeField24
        value={value}
        onChange={setValue}
        label="Analysis date"
        timeAriaLabel="Analysis time"
        required
      />
      <output aria-label="stored">{value}</output>
    </PinnedLightTheme>
  );
}

describe("DateTimeField24", () => {
  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it("shows a stored value on the 24-hour clock", () => {
    render(<Harness initial="2026-10-05T14:05" />);
    const dateGroup = screen.getByRole("group", { name: /Analysis date/i });
    expect(within(dateGroup).getByRole("spinbutton", { name: "Day" }).textContent).toBe("05");
    expect(within(dateGroup).getByRole("spinbutton", { name: "Month" }).textContent).toBe("10");
    expect(within(dateGroup).getByRole("spinbutton", { name: "Year" }).textContent).toBe("2026");
    expect((screen.getByLabelText("Analysis time") as HTMLInputElement).value).toBe("14:05");
  });

  it("typing a date then 1430 yields YYYY-MM-DDT14:30", async () => {
    const user = userEvent.setup();
    render(<Harness initial="" />);

    const dateGroup = screen.getByRole("group", { name: /Analysis date/i });
    await user.type(within(dateGroup).getByRole("spinbutton", { name: "Day" }), "05102026");
    expect(screen.getByLabelText("stored").textContent).toBe("2026-10-05T00:00");

    const timeInput = screen.getByLabelText("Analysis time");
    await user.clear(timeInput);
    await user.type(timeInput, "1430");
    expect((screen.getByLabelText("Analysis time") as HTMLInputElement).value).toBe("14:30");
    expect(screen.getByLabelText("stored").textContent).toBe("2026-10-05T14:30");
  });

  it("defaults the date part to today when only the time is typed", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(2026, 9, 11, 9, 15, 0));
    const user = userEvent.setup();
    render(<Harness initial="" />);

    await user.type(screen.getByLabelText("Analysis time"), "1645");
    expect(screen.getByLabelText("stored").textContent).toBe("2026-10-11T16:45");
  });

  it("does not commit an impossible time and flags it", async () => {
    const user = userEvent.setup();
    render(<Harness initial="2026-10-05T08:00" />);

    const timeInput = screen.getByLabelText("Analysis time") as HTMLInputElement;
    await user.clear(timeInput);
    await user.type(timeInput, "2575");
    expect(screen.getByLabelText("stored").textContent).toBe("2026-10-05T08:00");
    expect(timeInput.getAttribute("aria-invalid")).toBe("true");
  });

  it("clears the combined value when the date is cleared and preserves time when date changes", async () => {
    const user = userEvent.setup();
    render(<Harness initial="2026-10-05T14:30" />);

    const dateGroup = screen.getByRole("group", { name: /Analysis date/i });
    await user.type(within(dateGroup).getByRole("spinbutton", { name: "Day" }), "12");
    expect(screen.getByLabelText("stored").textContent).toBe("2026-10-12T14:30");

    await user.click(screen.getByRole("button", { name: "Clear" }));
    expect(screen.getByLabelText("stored").textContent).toBe("");
    expect((screen.getByLabelText("Analysis time") as HTMLInputElement).value).toBe("");
  });
});
