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

  it("groups no-interval exceptions under a summary line with Show toggle while keeping other exception kinds visible", async () => {
    const previewWithExceptions: EquipmentPlanPreview = {
      ...preview,
      exceptions: [
        {
          kind: "NoInterval",
          taskType: null,
          equipmentInventoryId: 101,
          equipmentCode: "EQ-001",
          message: "No calibration or maintenance interval configured.",
          target: "EQ-001"
        },
        {
          kind: "NoInterval",
          taskType: null,
          equipmentInventoryId: 102,
          equipmentCode: "EQ-002",
          message: "No calibration or maintenance interval configured.",
          target: "EQ-002"
        },
        {
          kind: "NoBaseline",
          taskType: "Calibration",
          equipmentInventoryId: 103,
          equipmentCode: "EQ-003",
          message: "No baseline calibration date entered.",
          target: "EQ-003"
        },
        {
          kind: "RetiredInstrument",
          taskType: null,
          equipmentInventoryId: 104,
          equipmentCode: "EQ-004",
          message: "Instrument EQ-004 is retired.",
          target: "EQ-004"
        }
      ]
    };

    vi.mocked(EquipmentPlanService.getPreview).mockResolvedValue(previewWithExceptions);

    render(<EquipmentPlanPanel year={2026} onApproved={vi.fn()} />);

    // Non-interval exceptions are visible directly
    expect(await screen.findByText("EQ-003 · Calibration: No baseline calibration date entered.")).toBeTruthy();
    expect(screen.getByText("EQ-004 · Instrument: Instrument EQ-004 is retired.")).toBeTruthy();

    // Summary line for no-interval exceptions
    expect(screen.getByText("2 instruments have no calibration or maintenance interval")).toBeTruthy();

    // Initially collapsed: EQ-001 and EQ-002 are not visible
    expect(screen.queryByText("EQ-001 · Instrument: No calibration or maintenance interval configured.")).toBeNull();

    // Click "Show"
    const showToggle = screen.getByRole("button", { name: "Show" });
    fireEvent.click(showToggle);

    // Now expanded
    expect(screen.getByText("EQ-001 · Instrument: No calibration or maintenance interval configured.")).toBeTruthy();
    expect(screen.getByText("EQ-002 · Instrument: No calibration or maintenance interval configured.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Hide" })).toBeTruthy();
  });

  it("renders PossibleDuplicate group labeled 'Possible duplicate instruments (not planned)' expanded by default", async () => {
    const previewWithDuplicates: EquipmentPlanPreview = {
      ...preview,
      exceptions: [
        {
          kind: "PossibleDuplicate",
          taskType: "Calibration",
          equipmentInventoryId: 201,
          equipmentCode: "EQ-DUP-01",
          message: "Possible duplicate of EQ-001 (same type, model and serial).",
          target: "EQ-DUP-01"
        }
      ]
    };

    vi.mocked(EquipmentPlanService.getPreview).mockResolvedValue(previewWithDuplicates);

    render(<EquipmentPlanPanel year={2026} onApproved={vi.fn()} />);

    // Group header is rendered
    expect(
      await screen.findByText("Possible duplicate instruments (not planned)")
    ).toBeTruthy();

    // Expanded by default
    expect(
      screen.getByText("EQ-DUP-01 · Calibration: Possible duplicate of EQ-001 (same type, model and serial).")
    ).toBeTruthy();

    // Has a Hide button
    const hideBtn = screen.getByRole("button", { name: "Hide" });
    fireEvent.click(hideBtn);

    // Now hidden
    expect(
      screen.queryByText("EQ-DUP-01 · Calibration: Possible duplicate of EQ-001 (same type, model and serial).")
    ).toBeNull();
    expect(screen.getByRole("button", { name: "Show" })).toBeTruthy();
  });
});
