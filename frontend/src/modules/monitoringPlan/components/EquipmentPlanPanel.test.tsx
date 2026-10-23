import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EquipmentPlanPanel } from "./EquipmentPlanPanel";
import { EquipmentPlanService } from "../services/EquipmentPlanService";
import type { EquipmentPlanPreview, EquipmentPlanRevision } from "../types";

let currentUserId = 5;

vi.mock("../../../contexts/AuthContext", () => ({
  useHasPermission: () => true,
  useAuth: () => ({ fullName: "Test User", username: "testuser", role: "SectionHead", userId: currentUserId })
}));

vi.mock("../services/EquipmentPlanService", () => ({
  EquipmentPlanService: {
    getRevisions: vi.fn(),
    getPreview: vi.fn(),
    generateDraft: vi.fn(),
    approve: vi.fn()
  }
}));

const draft: EquipmentPlanRevision = {
  id: 12,
  version: 1,
  year: 2026,
  revisionNumber: 1,
  status: "Draft",
  reason: null,
  effectiveFrom: "2026-10-25",
  generatedAt: "2026-10-23T09:00:00Z",
  generatedByUserId: 5,
  approvedAt: null,
  taskCount: 15
};

const preview: EquipmentPlanPreview = {
  revision: draft,
  totals: [{ taskType: "Calibration", instruments: 2, tasks: 5 }],
  toRetire: [],
  toReschedule: [],
  exceptions: [],
  tasks: []
};

describe("EquipmentPlanPanel", () => {
  beforeEach(() => {
    currentUserId = 5;
    vi.mocked(EquipmentPlanService.getRevisions).mockResolvedValue([draft]);
    vi.mocked(EquipmentPlanService.getPreview).mockResolvedValue(preview);
    vi.mocked(EquipmentPlanService.approve).mockResolvedValue(draft);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("disables Approve for the generating user", async () => {
    currentUserId = 5; // same as draft.generatedByUserId
    render(<EquipmentPlanPanel year={2026} onApproved={vi.fn()} />);

    const signBtn = (await screen.findByRole("button", { name: "Continue to sign" })) as HTMLButtonElement;
    expect(signBtn.disabled).toBe(true);
    expect(screen.getByText("The plan must be approved by someone other than the person who generated it.")).toBeTruthy();
  });

  it("enables Approve for a different user and submits approval with signature", async () => {
    currentUserId = 99; // different from draft.generatedByUserId (5)
    const onApproved = vi.fn();
    render(<EquipmentPlanPanel year={2026} onApproved={onApproved} />);

    const signBtn = (await screen.findByRole("button", { name: "Continue to sign" })) as HTMLButtonElement;
    expect(signBtn.disabled).toBe(false);

    fireEvent.click(signBtn);

    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(screen.getByText(/I am approving the 2026 equipment calibration and maintenance plan/)).toBeTruthy();

    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "secret123" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => {
      expect(EquipmentPlanService.approve).toHaveBeenCalledWith(12, "secret123", undefined);
    });
    await waitFor(() => {
      expect(onApproved).toHaveBeenCalled();
    });
  });
});
