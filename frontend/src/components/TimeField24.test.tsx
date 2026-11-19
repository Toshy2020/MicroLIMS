import { useState } from "react";
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TimeField24 } from "./TimeField24";

function Harness({ initial }: { initial: string }) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <TimeField24 value={value} onChange={setValue} ariaLabel="Analysis time" />
      <output aria-label="stored">{value}</output>
    </>
  );
}

describe("TimeField24", () => {
  it("shows the stored time on the 24-hour clock", () => {
    render(<Harness initial="14:05" />);
    expect((screen.getByLabelText("Analysis time") as HTMLInputElement).value).toBe("14:05");
  });

  it("commits a complete valid time, adding the colon", async () => {
    const user = userEvent.setup();
    render(<Harness initial="" />);
    await user.type(screen.getByLabelText("Analysis time"), "0930");
    expect((screen.getByLabelText("Analysis time") as HTMLInputElement).value).toBe("09:30");
    expect(screen.getByLabelText("stored").textContent).toBe("09:30");
  });

  it("does not commit an impossible time and flags it", async () => {
    const user = userEvent.setup();
    render(<Harness initial="08:00" />);
    const input = screen.getByLabelText("Analysis time") as HTMLInputElement;
    await user.clear(input);
    await user.type(input, "2575");
    expect(screen.getByLabelText("stored").textContent).toBe("08:00");
    expect(input.getAttribute("aria-invalid")).toBe("true");
  });
});
