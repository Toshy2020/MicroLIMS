import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PlanRevisionsPanel } from "./PlanRevisionsPanel";
import { MonitoringPlanService } from "../services/MonitoringPlanService";
import type { PlanPreview, PlanRevision } from "../types";

vi.mock("../../../contexts/AuthContext", () => ({
  useHasPermission: () => true,
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));
vi.mock("../services/MonitoringPlanService", () => ({
  MonitoringPlanService: { getRevisions: vi.fn(), getPreview: vi.fn(), generateDraft: vi.fn(), approve: vi.fn() }
}));

const draft: PlanRevision = {
  id: 9, version: 1, year: 2026, revisionNumber: 1, status: "Draft", reason: null, effectiveFrom: "2026-10-25",
  generatedAt: "2026-10-23T09:00:00Z", approvedAt: null, occurrenceCount: 40
};
const preview: PlanPreview = { revision: draft, totals: [], toRetire: [], toReschedule: [], exceptions: [] };

describe("PlanRevisionsPanel", () => {
  beforeEach(() => {
    vi.mocked(MonitoringPlanService.getRevisions).mockResolvedValue([draft]);
    vi.mocked(MonitoringPlanService.getPreview).mockResolvedValue(preview);
    vi.mocked(MonitoringPlanService.approve).mockResolvedValue(draft);
  });
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it("approves the plan through the shared signature dialog, saying what is being signed", async () => {
    const onApproved = vi.fn();
    render(<PlanRevisionsPanel year={2026} onApproved={onApproved} />);

    fireEvent.click(await screen.findByRole("button", { name: "Continue to sign" }));

    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(screen.getByText(/I am approving the 2026 monitoring plan \(revision 1\), which starts on 2026-10-25 with 40 new occurrence\(s\)/)).toBeTruthy();
    // A first approval needs one signature.
    expect(screen.queryByText("Second signature")).toBeNull();
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "pw-1" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(MonitoringPlanService.approve).toHaveBeenCalledWith(9, "pw-1", undefined));
    await waitFor(() => expect(onApproved).toHaveBeenCalled());
  });
});
