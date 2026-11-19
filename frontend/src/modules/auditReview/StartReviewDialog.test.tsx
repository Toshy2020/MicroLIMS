import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { StartReviewDialog } from "./StartReviewDialog";

async function typeDateTime(user: ReturnType<typeof userEvent.setup>, label: RegExp, iso: string) {
  const [date, time] = iso.split("T");
  const [y, m, d] = date.split("-");
  const dateGroup = screen.getByRole("group", { name: label });
  await user.type(within(dateGroup).getByRole("spinbutton", { name: "Day" }), `${d}${m}${y}`);
  const timeInput = screen.getByLabelText(new RegExp(`${label.source}.*time`, "i"));
  await user.clear(timeInput);
  await user.type(timeInput, time.replace(":", ""));
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("StartReviewDialog", () => {
  it("validates title, period bounds and dependent scope fields before calling onSubmit", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(
      <PinnedLightTheme>
        <StartReviewDialog open onClose={() => {}} onSubmit={onSubmit} />
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: "Start review" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/Title is required/i)).toBeTruthy();

    await user.type(screen.getByLabelText(/Title/i), "October Review");
    await typeDateTime(user, /Period from/i, "2026-10-05T10:00");
    await typeDateTime(user, /Period to/i, "2026-10-01T10:00");
    await user.click(screen.getByRole("button", { name: "Start review" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/earlier than period end/i)).toBeTruthy();

    await typeDateTime(user, /Period to/i, "2099-01-01T10:00");
    await user.click(screen.getByRole("button", { name: "Start review" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/cannot be in the future/i)).toBeTruthy();

    await typeDateTime(user, /Period to/i, "2026-10-10T10:00");

    await user.click(screen.getByRole("combobox", { name: /Scope/i }));
    await user.click(await screen.findByRole("option", { name: "Record" }));
    await user.click(screen.getByRole("button", { name: "Start review" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/Record type is required/i)).toBeTruthy();

    await user.type(screen.getByLabelText(/Record type/i), "Sample");
    await user.type(screen.getByLabelText(/Record ID/i), "42");
    await user.click(screen.getByRole("button", { name: "Start review" }));

    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(onSubmit).toHaveBeenCalledWith({
      title: "October Review",
      periodFromUtc: new Date("2026-10-05T10:00").toISOString(),
      periodToUtc: new Date("2026-10-10T10:00").toISOString(),
      scopeKind: "Record",
      entityName: "Sample",
      recordId: "42"
    });
  });
});
