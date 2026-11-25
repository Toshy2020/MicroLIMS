import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { DeviationRequestsPanel } from "./DeviationRequestsPanel";
import type { OosDeviationRequestDetailDto } from "../../../services/oosInvestigationService";

const openRequest: OosDeviationRequestDetailDto = {
  id: 5,
  reason: "Repeated bioburden exceedance on same line",
  requestedByUserId: 9,
  requestedByName: "Quinn QA",
  requestedAtUtc: "2026-11-21T10:00:00Z",
  status: "Open",
  addressedComment: null,
  addressedByUserId: null,
  addressedByName: null,
  addressedAtUtc: null,
  version: 2
};

describe("DeviationRequestsPanel", () => {
  afterEach(() => {
    cleanup();
  });

  it("shows Request Deviation button for QA and submits reason", async () => {
    const user = userEvent.setup();
    const onRequest = vi.fn().mockResolvedValue(undefined);

    render(
      <PinnedLightTheme>
        <DeviationRequestsPanel
          requests={[]}
          investigationVersion={4}
          canRequest={true}
          canAddress={false}
          onRequest={onRequest}
          onAddress={vi.fn()}
        />
      </PinnedLightTheme>
    );

    expect(screen.queryByRole("button", { name: "Address Request" })).toBeNull();
    await user.type(screen.getByLabelText("Deviation Request Reason"), "Escalate to CAPA");
    await user.click(screen.getByRole("button", { name: "Request Deviation" }));

    await waitFor(() => {
      expect(onRequest).toHaveBeenCalledWith("Escalate to CAPA", 4);
    });
  });

  it("shows Address Request action for Section Head and hides Request Deviation", async () => {
    const user = userEvent.setup();
    const onAddress = vi.fn().mockResolvedValue(undefined);

    render(
      <PinnedLightTheme>
        <DeviationRequestsPanel
          requests={[openRequest]}
          investigationVersion={4}
          canRequest={false}
          canAddress={true}
          onRequest={vi.fn()}
          onAddress={onAddress}
        />
      </PinnedLightTheme>
    );

    expect(screen.queryByRole("button", { name: "Request Deviation" })).toBeNull();
    await user.type(screen.getByLabelText("Addressing Comment"), "Deviation DEV-2026-01 opened.");
    await user.click(screen.getByRole("button", { name: "Address Request" }));

    await waitFor(() => {
      expect(onAddress).toHaveBeenCalledWith(5, "Deviation DEV-2026-01 opened.", 2);
    });
  });

  it("shows inline refusal and keeps comment when addressing fails", async () => {
    const user = userEvent.setup();
    const onAddress = vi.fn().mockResolvedValue("Deviation request was already addressed.");

    render(
      <PinnedLightTheme>
        <DeviationRequestsPanel
          requests={[openRequest]}
          investigationVersion={4}
          canRequest={false}
          canAddress={true}
          onRequest={vi.fn()}
          onAddress={onAddress}
        />
      </PinnedLightTheme>
    );

    const commentInput = screen.getByLabelText("Addressing Comment") as HTMLInputElement;
    await user.type(commentInput, "DEV-2026-01");
    await user.click(screen.getByRole("button", { name: "Address Request" }));

    expect(await screen.findByText("Deviation request was already addressed.")).toBeTruthy();
    expect(commentInput.value).toBe("DEV-2026-01");
  });
});
