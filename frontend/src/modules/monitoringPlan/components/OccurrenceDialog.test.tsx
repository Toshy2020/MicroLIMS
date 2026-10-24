import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { OccurrenceDialog } from "./OccurrenceDialog";
import { MonitoringPlanService } from "../services/MonitoringPlanService";
import type { OccurrenceDetail } from "../types";
import { dateFieldValue } from "../../../components/DateField.testUtils";

vi.mock("../../../contexts/AuthContext", () => ({
  useHasPermission: () => true,
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));
vi.mock("../services/MonitoringPlanService", () => ({
  MonitoringPlanService: { getDetail: vi.fn(), skip: vi.fn(), changeWindow: vi.fn() }
}));

const detail: OccurrenceDetail = {
  summary: {
    id: 5, version: 3, category: "Water", target: "SP 206", group: "Water Treatment Unit", periodKey: "2026-W43",
    targetDate: null, windowStart: "2026-10-25", windowEnd: "2026-10-29", originalWindowEnd: "2026-10-29",
    status: "Open", late: false, itemsTotal: 1, itemsPerformed: 0, manual: false
  },
  frequencySnapshot: "Weekly (own)", originalTargetDate: null,
  items: [{ itemId: 1, label: "TAMC", progress: "None", sampleReference: null }], events: []
};

const renderDialog = (onChanged = vi.fn()) =>
  render(<ThemeProvider theme={lightTheme}><OccurrenceDialog occurrenceId={5} onClose={() => {}} onChanged={onChanged} /></ThemeProvider>);

describe("OccurrenceDialog", () => {
  beforeEach(() => { vi.mocked(MonitoringPlanService.getDetail).mockResolvedValue(detail); });
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it("skips an occurrence through the shared signature dialog with both signatures", async () => {
    vi.mocked(MonitoringPlanService.skip).mockResolvedValue({} as never);
    const onChanged = vi.fn();
    renderDialog(onChanged);

    expect(await screen.findByText("SP 206 – Week 43, 2026")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Skip" }));
    fireEvent.change(screen.getByLabelText(/^Reason/), { target: { value: "Room closed for validation" } });
    fireEvent.click(screen.getByRole("button", { name: "Continue to sign" }));

    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(screen.getByText(/I am skipping SP 206 \(Week 43, 2026\)/)).toBeTruthy();
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "pw-1" } });
    fireEvent.change(screen.getByLabelText(/^Second signer username/), { target: { value: "approver" } });
    fireEvent.change(screen.getByLabelText(/^Second signer password/), { target: { value: "pw-2" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(MonitoringPlanService.skip).toHaveBeenCalledWith(5, 3, {
      reason: "Room closed for validation", password: "pw-1", secondSignerUsername: "approver", secondSignerPassword: "pw-2"
    }));
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
  });

  it("starts a window change from the current dates", async () => {
    renderDialog();
    await screen.findByText("SP 206 – Week 43, 2026");

    fireEvent.click(screen.getByRole("button", { name: "Change window" }));

    expect(dateFieldValue(/^New window end/)).toBe("2026-10-29");
    expect((screen.getByRole("button", { name: "Continue to sign" }) as HTMLButtonElement).disabled).toBe(true);
  });
});
