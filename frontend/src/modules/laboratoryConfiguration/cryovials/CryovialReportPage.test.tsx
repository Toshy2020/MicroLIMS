import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { lightTheme } from "../../../theme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { labelService } from "../../../services/labelService";
import { CryovialReportPage } from "./CryovialReportPage";
import type { CryovialSummary } from "./types/cryovialSummaryTypes";

const get = vi.hoisted(() => vi.fn());
vi.mock("../../../services/apiClient", () => ({ apiClient: { get } }));
vi.mock("../../testingWorkspace/services/ArchivedRecordsService", () => ({
  ArchivedRecordsService: { getForEntity: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "Analyst", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const summary = (over: Partial<CryovialSummary> = {}): CryovialSummary => ({
  cryovialId: 7, code: "CRY-0007", organismName: "Escherichia coli", manufacturerName: "Mfr", materialName: "Lyophilized E. coli",
  materialBatchNumber: "B1", expiryDate: "2027-01-01T00:00:00Z", numberOfVialsPrepared: 10, vialsRemaining: 8, storageCondition: "-70 C",
  physicalCheckConfirmed: true, physicalCheckText: "", preparedAt: "2026-10-01T00:00:00Z", preparedByName: "Analyst", approvalStatus: "Approved",
  approvedByName: null, approvedAt: null, isDestroyed: false, identityConfirmations: [], unthawedVialsDestruction: null,
  thawHistory: [], timeline: [], signatures: [], ...over
});

const renderReport = () =>
  render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter initialEntries={["/cryovials/7/report"]}>
          <Routes><Route path="/cryovials/:id/report" element={<CryovialReportPage />} /></Routes>
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  seedPermissions([PERMISSIONS.CRYOVIALS_MANAGE, PERMISSIONS.LABELS_REPRINT]);
  get.mockReset();
  vi.mocked(labelService.getStatus).mockResolvedValue([
    {
      entityType: "Cryovial",
      entityId: 7,
      canPrint: true,
      blockedReason: null,
      printCount: 0,
      defaultCopies: 1
    }
  ]);
});
afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("CryovialReportPage reference strain", () => {
  it("shows the strain line when the batch has a reference strain", async () => {
    get.mockResolvedValue({ data: { data: summary({ referenceOrganismCode: "ORG-001", cultureCollection: "ATCC", strainNumber: "8739" }) } });
    renderReport();
    expect(await screen.findByText("ORG-001 (ATCC 8739)")).toBeTruthy();
    expect(screen.getByText("Reference strain")).toBeTruthy();
  });

  it("omits the line on old batches without a strain", async () => {
    get.mockResolvedValue({ data: { data: summary() } });
    renderReport();
    await screen.findByText("CRY-0007", { selector: ".sample-id" });
    expect(screen.queryByText("Reference strain")).toBeNull();
  });

  it("shows the print label button with the record's code", async () => {
    const user = userEvent.setup();
    get.mockResolvedValue({ data: { data: summary() } });
    renderReport();

    await screen.findByText("CRY-0007", { selector: ".sample-id" });
    const printBtn = await screen.findByRole("button", { name: "Print label" });
    await vi.waitFor(() => {
      expect(printBtn.hasAttribute("disabled")).toBe(false);
    });

    await user.click(printBtn);

    const dialog = await screen.findByRole("dialog", { name: "Print labels" });
    expect(await within(dialog).findByText("CRY-0007")).toBeTruthy();
    expect(labelService.getStatus).toHaveBeenCalledWith(
      [{ entityType: "Cryovial", entityId: 7 }],
      expect.any(AbortSignal)
    );
  });
});
