import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ExtensionsPanel } from "./ExtensionsPanel";
import { actionDetail, extension } from "../testFixtures";
import type { QualityActionDetailDto, QualityExtensionDto } from "../../../services/qualityService";

interface Options {
  extensions?: QualityExtensionDto[] | null;
  actions?: QualityActionDetailDto[];
  canRequest?: boolean;
  canDecide?: boolean;
  onRequest?: () => void;
  onDecide?: (extension: QualityExtensionDto) => void;
}

function renderPanel(options: Options = {}) {
  const onRequest = options.onRequest ?? vi.fn();
  const onDecide = options.onDecide ?? vi.fn();
  render(
    <PinnedLightTheme>
      <ExtensionsPanel
        extensions={options.extensions === undefined ? [] : options.extensions}
        actions={options.actions ?? []}
        canRequest={options.canRequest ?? false}
        canDecide={options.canDecide ?? false}
        onRequest={onRequest}
        onDecide={onDecide}
      />
    </PinnedLightTheme>
  );
  return { onRequest, onDecide };
}

describe("ExtensionsPanel", () => {
  afterEach(() => {
    cleanup();
  });

  it("lists a pending request with its date, scope and reason, and says it awaits a decision", () => {
    renderPanel({
      extensions: [extension({ requestedDueDate: "2026-12-20", reason: "Supplier delivers the probe late", actionId: null })],
      canDecide: false
    });

    const item = screen.getByRole("listitem");
    expect(within(item).getByText("Pending decision")).toBeTruthy();
    expect(within(item).getByText("New due date requested: 2026-12-20")).toBeTruthy();
    expect(within(item).getByText("For the record")).toBeTruthy();
    expect(within(item).getByText("Supplier delivers the probe late")).toBeTruthy();
  });

  it("names the action a request is for", () => {
    renderPanel({
      extensions: [extension({ actionId: 11 })],
      actions: [actionDetail({ id: 11, description: "Recalibrate the incubator probe" })]
    });

    expect(screen.getByText("For action: Recalibrate the incubator probe")).toBeTruthy();
  });

  it("shows a decided request with its decision, time with the time zone and comment", () => {
    renderPanel({
      extensions: [
        extension({ decision: "Refused", decidedByUserId: 9, decidedAtUtc: "2026-11-23T14:00:00Z", decisionComment: "No more delay allowed." })
      ]
    });

    const item = screen.getByRole("listitem");
    expect(within(item).getByText(/^Refused · .*23 Nov 2026.*\d{2}:\d{2}\s+\S+/)).toBeTruthy();
    expect(within(item).getByText("No more delay allowed.")).toBeTruthy();
    expect(within(item).queryByRole("button", { name: /Decide/ })).toBeNull();
  });

  it("lets an approver decide a pending request", async () => {
    const user = userEvent.setup();
    const pending = extension({ requestedDueDate: "2026-12-20" });
    const { onDecide } = renderPanel({ extensions: [pending], canDecide: true });

    await user.click(screen.getByRole("button", { name: "Decide the extension request to 2026-12-20" }));

    expect(onDecide).toHaveBeenCalledWith(pending);
  });

  it("lets the owner request an extension", async () => {
    const user = userEvent.setup();
    const { onRequest } = renderPanel({ canRequest: true });

    await user.click(screen.getByRole("button", { name: "Request extension" }));

    expect(onRequest).toHaveBeenCalledTimes(1);
  });

  it("is read-only without the permissions: nothing to request or decide", () => {
    renderPanel({ extensions: [extension()], canRequest: false, canDecide: false });

    expect(screen.queryByRole("button", { name: "Request extension" })).toBeNull();
    expect(screen.queryByRole("button", { name: /^Decide/ })).toBeNull();
  });

  it("shows the empty state and the unavailable state", () => {
    renderPanel({ extensions: [] });
    expect(screen.getByText("No extension requests.")).toBeTruthy();
    cleanup();

    renderPanel({ extensions: null });
    expect(screen.getByText("Extension requests could not be loaded.")).toBeTruthy();
  });
});
