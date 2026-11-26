import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("../../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/apiClient")>();
  return { ...actual, apiClient: http };
});

import { ActionsPanel } from "./ActionsPanel";
import { actionDetail, link, refusal } from "../testFixtures";
import type { QualityActionDetailDto, QualityLinkDto, QualityRecordType } from "../../../services/qualityService";

interface PanelOptions {
  type?: QualityRecordType;
  actions?: QualityActionDetailDto[];
  links?: QualityLinkDto[] | null;
  currentUserId?: number | null;
  isCancelled?: boolean;
  onChanged?: () => Promise<void>;
  onRequestExtension?: (action: QualityActionDetailDto) => void;
}

function renderPanel(options: PanelOptions = {}) {
  const onChanged = options.onChanged ?? vi.fn().mockResolvedValue(undefined);
  const onRequestExtension = options.onRequestExtension ?? vi.fn();
  render(
    <PinnedLightTheme>
      <ActionsPanel
        record={{ id: 5, type: options.type ?? "Capa", version: 9, isCancelled: options.isCancelled ?? false }}
        actions={options.actions ?? [actionDetail()]}
        links={options.links === undefined ? [link({ id: 31, number: "S-2026-0010", label: "Amoxicillin 500mg" })] : options.links}
        currentUserId={options.currentUserId === undefined ? 7 : options.currentUserId}
        onChanged={onChanged}
        onRequestExtension={onRequestExtension}
      />
    </PinnedLightTheme>
  );
  return { onChanged, onRequestExtension };
}

describe("ActionsPanel", () => {
  beforeEach(() => {
    http.post.mockResolvedValue({ data: { data: {} } });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("lists each action with its kind, responsible person, due date and state in words", () => {
    renderPanel({
      actions: [
        actionDetail({ id: 11, kind: "Corrective", description: "Recalibrate the incubator probe", responsibleName: "Ann Analyst", dueDate: "2026-12-05", isOverdue: true }),
        actionDetail({ id: 12, kind: "Preventive", description: "Add a daily temperature check", responsibleName: "Bob Bench", isOverdue: false })
      ],
      currentUserId: 99
    });

    const items = screen.getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByText("Recalibrate the incubator probe")).toBeTruthy();
    expect(within(items[0]).getByText("Corrective")).toBeTruthy();
    expect(within(items[0]).getByText(/Ann Analyst/)).toBeTruthy();
    expect(within(items[0]).getByText("Due 2026-12-05")).toBeTruthy();
    expect(within(items[0]).getByText("Open")).toBeTruthy();
    expect(within(items[0]).getByText("Overdue")).toBeTruthy();
    expect(within(items[1]).getByText("Preventive")).toBeTruthy();
    expect(within(items[1]).queryByText("Overdue")).toBeNull();
  });

  it("shows who completed an action, when (with the time zone) and the evidence", () => {
    renderPanel({
      actions: [
        actionDetail({
          doneAtUtc: "2026-11-25T09:30:00Z",
          doneByUserId: 7,
          doneByName: "Ann Analyst",
          evidence: "Probe recalibrated, certificate attached."
        })
      ]
    });

    expect(screen.getByText("Completed")).toBeTruthy();
    expect(screen.getByText(/Completed by Ann Analyst · .*25 Nov 2026.*\d{2}:\d{2}\s+\S+/)).toBeTruthy();
    expect(screen.getByText("Probe recalibrated, certificate attached.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Complete action/ })).toBeNull();
  });

  it("offers Complete only to the responsible person, on an open CAPA or change-control action", () => {
    renderPanel({ currentUserId: 99 });
    expect(screen.queryByRole("button", { name: /Complete action/ })).toBeNull();
    cleanup();

    renderPanel({ type: "Deviation" });
    expect(screen.queryByRole("button", { name: /Complete action/ })).toBeNull();
    cleanup();

    renderPanel({ isCancelled: true });
    expect(screen.queryByRole("button", { name: /Complete action/ })).toBeNull();
    cleanup();

    renderPanel({ type: "ChangeControl" });
    expect(screen.getByRole("button", { name: "Complete action: Recalibrate the incubator probe" })).toBeTruthy();
  });

  it("completes a CAPA action with its evidence and an evidence link, sending the record version", async () => {
    const user = userEvent.setup();
    const { onChanged } = renderPanel();

    await user.click(screen.getByRole("button", { name: "Complete action: Recalibrate the incubator probe" }));
    const dialog = await screen.findByRole("dialog");
    const submit = within(dialog).getByRole("button", { name: "Complete action" });
    expect((submit as HTMLButtonElement).disabled).toBe(true);

    await user.type(within(dialog).getByLabelText(/^Evidence/, { selector: "textarea" }), "Probe recalibrated.");
    await user.click(within(dialog).getByRole("combobox", { name: "Evidence link (optional)" }));
    await user.click(await screen.findByRole("option", { name: /S-2026-0010/ }));
    await user.click(submit);

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/capas/actions/11/complete",
        { evidence: "Probe recalibrated.", evidenceLinkId: 31 },
        { headers: { "If-Match": '"9"' } }
      );
    });
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("completes a change-control task on its own route, without an evidence link", async () => {
    const user = userEvent.setup();
    renderPanel({ type: "ChangeControl", actions: [actionDetail({ id: 12, kind: "ChangeTask", description: "Update the SOP" })] });

    await user.click(screen.getByRole("button", { name: "Complete action: Update the SOP" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Evidence/, { selector: "textarea" }), "SOP v4 issued.");
    await user.click(within(dialog).getByRole("button", { name: "Complete action" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/change-controls/tasks/12/complete",
        { evidence: "SOP v4 issued.", evidenceLinkId: null },
        { headers: { "If-Match": '"9"' } }
      );
    });
  });

  it("keeps the dialog open and shows the server's refusal when completing fails", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("The CAPA plan must be approved first."));
    const { onChanged } = renderPanel();

    await user.click(screen.getByRole("button", { name: "Complete action: Recalibrate the incubator probe" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Evidence/, { selector: "textarea" }), "Probe recalibrated.");
    await user.click(within(dialog).getByRole("button", { name: "Complete action" }));

    expect(await within(dialog).findByText("The CAPA plan must be approved first.")).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(onChanged).not.toHaveBeenCalled();
  });

  it("lets the responsible person ask for an extension of an open action", async () => {
    const user = userEvent.setup();
    const action = actionDetail();
    const { onRequestExtension } = renderPanel({ actions: [action] });

    await user.click(screen.getByRole("button", { name: "Request an extension for action: Recalibrate the incubator probe" }));

    expect(onRequestExtension).toHaveBeenCalledWith(action);
  });

  it("shows an empty state, and an unavailable state when the links needed for evidence failed to load", async () => {
    renderPanel({ actions: [] });
    expect(screen.getByText("This record has no actions.")).toBeTruthy();
    cleanup();

    const user = userEvent.setup();
    renderPanel({ links: null });
    await user.click(screen.getByRole("button", { name: "Complete action: Recalibrate the incubator probe" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).queryByRole("combobox", { name: "Evidence link (optional)" })).toBeNull();
  });
});
