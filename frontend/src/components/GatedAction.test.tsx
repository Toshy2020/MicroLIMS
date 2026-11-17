import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Button } from "@mui/material";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { actionAvailabilityService } from "../services/actionAvailabilityService";
import { useActionAvailability } from "../hooks/useActionAvailability";
import { GatedAction } from "./GatedAction";

// Network boundary only.
vi.mock("../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn() }
}));

const getAvailability = vi.mocked(actionAvailabilityService.get);

function Harness({ onAct, afterAct }: { onAct: () => void; afterAct?: boolean }) {
  const availability = useActionAvailability("sample", 7, 3);
  return (
    <GatedAction action="review.complete" availability={availability}>
      <Button
        onClick={async () => {
          onAct();
          if (afterAct) availability.refresh();
        }}
      >
        Complete review
      </Button>
    </GatedAction>
  );
}

function mount(ui: React.ReactElement) {
  return render(<PinnedLightTheme>{ui}</PinnedLightTheme>);
}

beforeEach(() => {
  getAvailability.mockReset();
});
afterEach(cleanup);

describe("GatedAction", () => {
  it("allowed: the button works", async () => {
    getAvailability.mockResolvedValue([{ action: "review.complete", allowed: true, reason: null }]);
    const onAct = vi.fn();
    const user = userEvent.setup();
    mount(<Harness onAct={onAct} />);
    await waitFor(() => expect(getAvailability).toHaveBeenCalledWith("sample", 7, 3, expect.any(AbortSignal)));
    const button = await screen.findByRole("button", { name: "Complete review" });
    expect(button.getAttribute("aria-disabled")).toBeNull();
    await user.click(button);
    expect(onAct).toHaveBeenCalledTimes(1);
  });

  it("not allowed: aria-disabled, click does nothing, reason visible and described-by", async () => {
    getAvailability.mockResolvedValue([
      { action: "review.complete", allowed: false, reason: "You performed a test on this sample, so you cannot review it." }
    ]);
    const onAct = vi.fn();
    const user = userEvent.setup();
    mount(<Harness onAct={onAct} />);
    const reason = await screen.findByText("You performed a test on this sample, so you cannot review it.");
    const button = screen.getByRole("button", { name: "Complete review" });
    expect(button.getAttribute("aria-disabled")).toBe("true");
    expect((button as HTMLButtonElement).disabled).toBe(false);
    const describedBy = button.getAttribute("aria-describedby") ?? "";
    expect(describedBy.length).toBeGreaterThan(0);
    expect(document.getElementById(describedBy)?.contains(reason)).toBe(true);
    await user.click(button);
    expect(onAct).not.toHaveBeenCalled();
    button.focus();
    expect(document.activeElement).toBe(button);
  });

  it("loading: the button is enabled and unchanged", () => {
    getAvailability.mockReturnValue(new Promise(() => undefined));
    mount(<Harness onAct={vi.fn()} />);
    const button = screen.getByRole("button", { name: "Complete review" });
    expect(button.getAttribute("aria-disabled")).toBeNull();
    expect(button.getAttribute("aria-describedby")).toBeNull();
  });

  it("failed: the button is enabled and a single notice says reasons are unavailable", async () => {
    getAvailability.mockRejectedValue(new Error("network"));
    const onAct = vi.fn();
    const user = userEvent.setup();
    mount(<Harness onAct={onAct} />);
    const notices = await screen.findAllByText("Reasons are unavailable right now");
    expect(notices).toHaveLength(1);
    const button = screen.getByRole("button", { name: "Complete review" });
    expect(button.getAttribute("aria-disabled")).toBeNull();
    await user.click(button);
    expect(onAct).toHaveBeenCalledTimes(1);
  });

  it("renders the reason as text, never as HTML", async () => {
    getAvailability.mockResolvedValue([
      { action: "review.complete", allowed: false, reason: "<b>bold</b> <img src=x onerror=alert(1)>" }
    ]);
    const { container } = mount(<Harness onAct={vi.fn()} />);
    await screen.findByText("<b>bold</b> <img src=x onerror=alert(1)>");
    expect(container.querySelector("b")).toBeNull();
    expect(container.querySelector("img")).toBeNull();
  });

  it("after the action succeeds availability is refetched", async () => {
    getAvailability.mockResolvedValueOnce([{ action: "review.complete", allowed: true, reason: null }]);
    getAvailability.mockResolvedValueOnce([
      { action: "review.complete", allowed: false, reason: "This sample is already reviewed." }
    ]);
    const user = userEvent.setup();
    mount(<Harness onAct={vi.fn()} afterAct />);
    await waitFor(() => expect(getAvailability).toHaveBeenCalledTimes(1));
    await user.click(await screen.findByRole("button", { name: "Complete review" }));
    expect(await screen.findByText("This sample is already reviewed.")).toBeTruthy();
    expect(getAvailability).toHaveBeenCalledTimes(2);
    expect(screen.getByRole("button", { name: "Complete review" }).getAttribute("aria-disabled")).toBe("true");
  });
});
