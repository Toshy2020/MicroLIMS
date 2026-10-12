import { describe, it, expect, afterEach, vi } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { SampleCardView } from "./SampleCardView";
import type { SampleCard } from "./types/workspaceTypes";

vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => ({ permissions: [] }) }));

afterEach(() => cleanup());

const card = (over: Partial<SampleCard>): SampleCard => ({
  sampleId: 95, referenceNumber: "FP0926016", displayName: "Smoke Test Item", category: "FinishedProduct",
  departmentId: null, machineId: null, waterDepartmentId: null, productionStage: null, causeOfTesting: "Routine",
  batchNumber: "SMOKE-AAS", controlNumber: "SMOKE-AAS", status: "Received", preparationStatus: "Prepared",
  receivedAt: "2026-10-10T10:51:00Z", sampleQuantity: null, sampledBy: "IPQA", mfgDate: null, expDate: null,
  waterSamplingPointCode: null, waterSamplingPointLocation: null, assignedTests: [],
  ...over
} as SampleCard);

const renderCards = (samples: SampleCard[], selectedSampleId: number | null = null) => render(
  <ThemeProvider theme={lightTheme}>
    <SampleCardView samples={samples} selectedSampleId={selectedSampleId} onSelectSample={() => {}} onNeedsPreparationClick={() => {}} onLifecycleBadgeClick={() => {}} />
  </ThemeProvider>
);

describe("SampleCardView due times", () => {
  it("shows the due line and a red edge on overdue cards, even when selected", () => {
    const now = Date.now();
    renderCards([
      card({ sampleId: 95, dueAt: new Date(now - 26 * 3600e3).toISOString(), isOverdue: true }),
      card({ sampleId: 88, referenceNumber: "WT0926008", dueAt: new Date(now + 21.5 * 3600e3).toISOString(), isOverdue: false })
    ], 95);
    expect(screen.getByText("Overdue by 26 h")).toBeTruthy();
    expect(screen.getByText("Due in 21 h")).toBeTruthy();
    const overdueCard = screen.getByText("FP0926016").closest('[role="button"]') as HTMLElement;
    const hex = lightTheme.custom.status.detected.text.replace("#", "");
    const rgb = [0, 2, 4].map((i) => parseInt(hex.slice(i, i + 2), 16)).join(", ");
    expect(getComputedStyle(overdueCard).borderLeftColor).toBe(`rgb(${rgb})`);
  });

  it("shows no due line for closed samples", () => {
    renderCards([card({ status: "Approved", dueAt: null, isOverdue: false })]);
    expect(screen.queryByText(/Due in|Overdue by/)).toBeNull();
  });
});
