import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AddProfileVersionDialog } from "./AddProfileVersionDialog";
import { workflowProfileService } from "../services/workflowProfileService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { WorkflowProfileVersion } from "../types/workflowProfileTypes";

vi.mock("../services/workflowProfileService", () => ({
  workflowProfileService: { create: vi.fn(), signDraft: vi.fn() }
}));

// The real dialog is covered by its own tests; here only the confirm hand-off matters.
vi.mock("../../../components/SignatureDialog", async () => {
  const React = await import("react");
  return {
    SignatureDialog: (props: { open: boolean; meaningStatement: string; onConfirm: (password: string) => Promise<void> }) =>
      props.open
        ? React.createElement(
            "div",
            null,
            React.createElement("p", null, props.meaningStatement),
            React.createElement("button", { onClick: () => void props.onConfirm("pw-123") }, "confirm-sign")
          )
        : null
  };
});

const draft = (overrides: Partial<WorkflowProfileVersion> = {}): WorkflowProfileVersion => ({
  id: 7,
  workflowType: "CountTest",
  testCode: "TAMC",
  testName: "Total Aerobic Microbial Count",
  workUnits: 1.5,
  targetAnalystDays: null,
  effectiveFrom: "2026-10-20",
  status: "Draft",
  reasonForChange: "Imported from legacy workload weight",
  electronicSignatureId: null,
  createdByUserId: 0,
  createdByName: "System (legacy import)",
  createdAt: "2026-10-01T00:00:00Z",
  signedAt: null,
  version: 4,
  ...overrides
});

function renderDialog(props: Partial<React.ComponentProps<typeof AddProfileVersionDialog>> = {}) {
  const onClose = vi.fn();
  const onDone = vi.fn();
  render(
    <PinnedLightTheme>
      <AddProfileVersionDialog open onClose={onClose} onDone={onDone} workflowType="CountTest" testCode={null} {...props} />
    </PinnedLightTheme>
  );
  return { onClose, onDone };
}

const continueButton = () => screen.getByRole("button", { name: "Continue to sign" }) as HTMLButtonElement;

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-11T10:00:00Z"));
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.clearAllMocks();
});

describe("AddProfileVersionDialog", () => {
  it("keeps Continue to sign disabled until a value and a reason are entered", async () => {
    renderDialog();
    expect(continueButton().disabled).toBe(true);

    await userEvent.type(screen.getByLabelText(/Work units/i), "2");
    expect(continueButton().disabled).toBe(true);

    await userEvent.type(screen.getByLabelText(/Reason for change/i), "SOP time study");
    expect(continueButton().disabled).toBe(false);
  });

  it("defaults a new version to today", () => {
    renderDialog();
    expect((screen.getByLabelText(/Effective from/i) as HTMLInputElement).value).toBe("2026-10-11");
  });

  it("prefills today when the draft date is in the past", () => {
    renderDialog({ draft: draft({ effectiveFrom: "2026-10-01" }), testCode: "TAMC" });
    expect((screen.getByLabelText(/Effective from/i) as HTMLInputElement).value).toBe("2026-10-11");
  });

  it("keeps a draft date that is still in the future", () => {
    renderDialog({ draft: draft({ effectiveFrom: "2026-10-20" }), testCode: "TAMC" });
    expect((screen.getByLabelText(/Effective from/i) as HTMLInputElement).value).toBe("2026-10-20");
  });

  it("creates a version after the signature step and reports done", async () => {
    vi.mocked(workflowProfileService.create).mockResolvedValue(draft());
    const { onClose, onDone } = renderDialog();

    await userEvent.type(screen.getByLabelText(/Work units/i), "2.5");
    await userEvent.type(screen.getByLabelText(/Target analyst days/i), "10");
    await userEvent.type(screen.getByLabelText(/Reason for change/i), "SOP time study");
    await userEvent.click(continueButton());
    expect(screen.getByText(/Count Test type default/)).toBeTruthy();
    await userEvent.click(screen.getByText("confirm-sign"));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(workflowProfileService.create).toHaveBeenCalledWith({
      workflowType: "CountTest",
      testCode: null,
      workUnits: 2.5,
      targetAnalystDays: 10,
      effectiveFrom: "2026-10-11",
      reasonForChange: "SOP time study",
      password: "pw-123"
    });
    expect(onClose).toHaveBeenCalled();
  });

  it("signs a draft with the version it was loaded at", async () => {
    vi.mocked(workflowProfileService.signDraft).mockResolvedValue(draft({ status: "Effective" }));
    const { onDone } = renderDialog({ draft: draft(), testCode: "TAMC" });

    await userEvent.type(screen.getByLabelText(/Reason for change/i), "Reviewed against the SOP");
    await userEvent.click(continueButton());
    await userEvent.click(screen.getByText("confirm-sign"));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(workflowProfileService.signDraft).toHaveBeenCalledWith(7, 4, {
      workUnits: 1.5,
      targetAnalystDays: null,
      effectiveFrom: "2026-10-20",
      reasonForChange: "Reviewed against the SOP",
      password: "pw-123"
    });
    expect(workflowProfileService.create).not.toHaveBeenCalled();
  });
});
