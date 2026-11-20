import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { apiClient } from "../../../services/apiClient";
import { actionAvailabilityService } from "../../../services/actionAvailabilityService";
import { labelService } from "../../../services/labelService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { AuditSearchService } from "../../auditSearch/services/AuditSearchService";
import { MediaEvaluationService } from "../mediaEvaluation/services/MediaEvaluationService";
import type { MediaLot } from "../mediaEvaluation/types/mediaEvaluationTypes";
import { MediaPreparationService } from "./services/MediaPreparationService";
import { MediaPage } from "./MediaPage";

// Pins the Media Lots register on the Media page: columns, search, both dropdowns, the
// KPI shortcut, row actions, the split-pane compact list and the exact service calls.
// The same assertions hold before and after the move onto the shared table state.

// Network boundary: the server's answer about releasing a lot.
vi.mock("../../../services/actionAvailabilityService", () => ({
  actionAvailabilityService: { get: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  ifMatch: vi.fn((version: number) => ({ headers: { "If-Match": String(version) } }))
}));
vi.mock("../../../services/labelService", () => ({
  labelService: { getStatus: vi.fn(), print: vi.fn(), getFormats: vi.fn(), updateFormat: vi.fn(), resolveScan: vi.fn() }
}));
vi.mock("../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/masterDataOptions")>();
  return { ...actual, masterDataOptions: { getMaterials: vi.fn(), getEquipment: vi.fn(), getReleasedMedia: vi.fn() } };
});
vi.mock("../mediaEvaluation/services/MediaEvaluationService", () => ({
  MediaEvaluationService: { getAll: vi.fn() }
}));
vi.mock("../../auditSearch/services/AuditSearchService", () => ({
  AuditSearchService: { getForEntity: vi.fn() }
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));
vi.mock("./services/MediaPreparationService", () => ({
  MediaPreparationService: {
    getAll: vi.fn(),
    getAwaitingApproval: vi.fn(),
    getUsableSolutions: vi.fn(),
    prepare: vi.fn(),
    decideRelease: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const MATERIALS = [
  { id: 301, materialName: "Tryptic Soy Agar Powder", batchNumber: "OX-2601" },
  { id: 302, materialName: "Sabouraud Dextrose Agar Powder", batchNumber: "OX-2602" }
];

const lot = (over: Partial<MediaLot> & { id: number }): MediaLot => ({
  materialId: 301,
  material: MATERIALS[0] as unknown as MediaLot["material"],
  lotNumber: `ML-${over.id}`,
  manufacturerLot: "OX-2601",
  manufacturerName: "Oxoid",
  totalWeight: 40,
  totalVolume: "1000 mL",
  autoclaveEquipmentId: 41,
  autoclaveProgram: "STD-121",
  loadType: "Liquids",
  temperature: 121,
  cycleTime: 15,
  cycleNumber: 3,
  ph: 7.3,
  expiryDate: "2027-06-30T00:00:00Z",
  preparedAt: "2026-11-13T08:00:00Z",
  preparedByUserId: 1,
  status: "Prepared",
  approvalStatus: "PendingReview",
  approvedByUserId: null,
  approvedAt: null,
  isReleasedForUse: false,
  ...over
});

const LOTS: MediaLot[] = [
  lot({ id: 1 }),
  lot({ id: 2, materialId: 302, material: MATERIALS[1] as unknown as MediaLot["material"] }),
  lot({ id: 3, status: "Active", approvalStatus: "Approved", isReleasedForUse: true }),
  lot({ id: 4, status: "OutOfStock", approvalStatus: "Approved" }),
  lot({ id: 5, approvalStatus: "Rejected", materialId: 302, material: MATERIALS[1] as unknown as MediaLot["material"] })
];

function LocationProbe() {
  const location = useLocation();
  return <output aria-label="Address">{location.search}</output>;
}

function renderPage(initialUrl = "/") {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialUrl]}>
          <MediaPage />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const registerRows = () => screen.getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const hasLot = (code: string) => registerRows().some((r) => r.includes(code));
const ready = () => waitFor(() => expect(screen.getByText("ML-2")).toBeTruthy());
const SEARCH = "Search by lot number, dehydrated material, batch…";

async function pickSelect(user: ReturnType<typeof userEvent.setup>, comboName: string, option: string) {
  await user.click(screen.getByRole("combobox", { name: comboName }));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("MediaPage lots register", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.MEDIA_PREPARE, PERMISSIONS.MEDIA_RELEASE]);
    vi.mocked(MediaPreparationService.getAll).mockResolvedValue(LOTS);
    vi.mocked(MediaPreparationService.getAwaitingApproval).mockResolvedValue([{ id: 2 }]);
    vi.mocked(MediaPreparationService.getUsableSolutions).mockResolvedValue([]);
    vi.mocked(MediaPreparationService.decideRelease).mockResolvedValue({});
    vi.mocked(MediaEvaluationService.getAll).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMaterials).mockResolvedValue(MATERIALS);
    vi.mocked(masterDataOptions.getReleasedMedia).mockResolvedValue([]);
    vi.mocked(AuditSearchService.getForEntity).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: null } });
    vi.mocked(tableViewService.list).mockResolvedValue([]);
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("loads lots, the approval queue, evaluations and dehydrated materials once, and shows the full-width columns", async () => {
    renderPage();
    await ready();
    expect(MediaPreparationService.getAll).toHaveBeenCalledTimes(1);
    expect(MediaPreparationService.getAll).toHaveBeenCalledWith();
    expect(MediaPreparationService.getAwaitingApproval).toHaveBeenCalledTimes(1);
    expect(MediaEvaluationService.getAll).toHaveBeenCalledTimes(1);
    expect(masterDataOptions.getMaterials).toHaveBeenCalledWith("DehydratedMedia");
    expect(screen.getAllByRole("columnheader").map((h) => h.textContent?.trim())).toEqual([
      "Prepared On", "Dehydrated Material", "Lot Number", "Expiry Date", "Status", "Actions"
    ]);
    expect(registerRows()).toHaveLength(5);
    const second = registerRows()[1];
    expect(second).toContain("Sabouraud Dextrose Agar Powder");
    expect(second).toContain("Batch: OX-2602");
    expect(second).toContain("ML-2");
    expect(second).toContain("ID #2");
    expect(second).toContain("13/11/26");
    expect(second).toContain("30/06/27");
    expect(second).toContain("Awaiting Approval");
    expect(registerRows()[0]).toContain("Pending Evaluation");
    expect(registerRows()[2]).toContain("Released");
    expect(registerRows()[3]).toContain("Out of Stock");
    expect(registerRows()[4]).toContain("Rejected");
    expect(screen.getByText("Media Lots (5)")).toBeTruthy();
  });

  it("search narrows by lot number, material name and batch, and Reset Filters restores the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    const search = screen.getByRole("textbox", { name: SEARCH });
    await user.type(search, "sabouraud");
    await waitFor(() => expect(registerRows()).toHaveLength(2), { timeout: 3000 });
    expect(hasLot("ML-2")).toBe(true);
    expect(hasLot("ML-5")).toBe(true);
    await user.clear(search);
    await user.type(search, "ml-3");
    await waitFor(() => expect(registerRows()).toHaveLength(1), { timeout: 3000 });
    expect(hasLot("ML-3")).toBe(true);
    await user.clear(search);
    await user.type(search, "ox-2601");
    await waitFor(() => expect(registerRows()).toHaveLength(3), { timeout: 3000 });
    await user.click(screen.getByRole("button", { name: "Reset Filters" }));
    await waitFor(() => expect(registerRows()).toHaveLength(5), { timeout: 3000 });
    expect(screen.queryByRole("button", { name: "Reset Filters" })).toBeNull();
    // Types three searches; give it room when the suite runs on a loaded machine.
  }, 15000);

  it("the material and status dropdowns filter, and the status dropdown and the KPI tiles stay in step", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await pickSelect(user, "Material", "Sabouraud Dextrose Agar Powder");
    await waitFor(() => expect(registerRows()).toHaveLength(2), { timeout: 5000 });
    await pickSelect(user, "Status", "Rejected");
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasLot("ML-5")).toBe(true);
    }, { timeout: 5000 });
    expect(screen.getByRole("button", { name: /^Rejected \(filtered\)/ })).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Reset Filters" }));
    await waitFor(() => expect(registerRows()).toHaveLength(5), { timeout: 5000 });

    await user.click(screen.getByRole("button", { name: /^Awaiting Approval/ }));
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasLot("ML-2")).toBe(true);
    }, { timeout: 5000 });
    expect(screen.getByRole("combobox", { name: "Status" }).textContent).toBe("Awaiting Approval");
    await user.click(screen.getByRole("button", { name: /^Awaiting Approval \(filtered\)/ }));
    await waitFor(() => expect(registerRows()).toHaveLength(5), { timeout: 5000 });
    expect(screen.getByRole("combobox", { name: "Status" }).textContent).toBe("All Statuses");

    await user.click(screen.getByRole("button", { name: /^Released/ }));
    await waitFor(() => expect(registerRows()).toHaveLength(1), { timeout: 5000 });
    await user.click(screen.getByRole("button", { name: /^Total Lots/ }));
    await waitFor(() => expect(registerRows()).toHaveLength(5), { timeout: 5000 });
  });

  it("a release the server refuses shows its sentence on the row item and does nothing when chosen", async () => {
    const user = userEvent.setup();
    const sentence = "You prepared this lot, so you cannot release it.";
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([{ action: "media.release", allowed: false, reason: sentence }]);
    renderPage();
    await ready();

    await user.click(screen.getByRole("button", { name: "Row actions for 2" }));
    await waitFor(() => expect(actionAvailabilityService.get).toHaveBeenCalledWith("media", 2, undefined, expect.any(AbortSignal)));
    const item = await screen.findByRole("menuitem", { name: /Release lot/ });
    expect(await screen.findByText(sentence)).toBeTruthy();
    expect(item.getAttribute("aria-disabled")).toBe("true");
    expect(document.getElementById(item.getAttribute("aria-describedby") ?? "")?.textContent).toBe(sentence);
    await user.click(item);
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(MediaPreparationService.decideRelease).not.toHaveBeenCalled();
  });

  it("an empty result says no lot matches the filter", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.type(screen.getByRole("textbox", { name: SEARCH }), "no-such-lot");
    expect(await screen.findByText("No media lots match this filter")).toBeTruthy();
  });

  it("row actions: release and reject only for a lot awaiting approval, record opens a new tab, audit trail", async () => {
    const user = userEvent.setup();
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    renderPage();
    await ready();

    await user.click(screen.getByRole("button", { name: "Row actions for 1" }));
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual(["View lot record (printable)", "Audit trail"]);
    await user.click(screen.getByRole("menuitem", { name: "View lot record (printable)" }));
    expect(open).toHaveBeenCalledWith("/media/1/report", "_blank", "noopener");

    await user.click(screen.getByRole("button", { name: "Row actions for 2" }));
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      "Release lot", "Reject lot", "View lot record (printable)", "Audit trail"
    ]);
    await user.click(screen.getByRole("menuitem", { name: "Audit trail" }));
    await waitFor(() => expect(AuditSearchService.getForEntity).toHaveBeenCalledWith("Media", 2));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await user.click(screen.getByRole("button", { name: "Row actions for 2" }));
    await user.click(screen.getByRole("menuitem", { name: "Release lot" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("I am releasing media lot ML-2 for use in routine testing.")).toBeTruthy();
    await user.type(within(dialog).getByLabelText(/Password/), "secret-pw");
    await user.click(within(dialog).getByRole("button", { name: /Sign|Confirm/ }));
    await waitFor(() => expect(MediaPreparationService.decideRelease).toHaveBeenCalledWith(2, "secret-pw", true));
    expect(await screen.findByText("Media lot ML-2 released for use.")).toBeTruthy();
    await waitFor(() => expect(MediaPreparationService.getAll).toHaveBeenCalledTimes(2));
  });

  it("without the release permission Reject is absent and Release shows the server's role sentence", async () => {
    seedPermissions([PERMISSIONS.MEDIA_PREPARE]);
    const sentence = "Your role does not include this action.";
    vi.mocked(actionAvailabilityService.get).mockResolvedValue([{ action: "media.release", allowed: false, reason: sentence }]);
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.click(screen.getByRole("button", { name: "Row actions for 2" }));
    expect(await screen.findByText(sentence)).toBeTruthy();
    expect(screen.getAllByRole("menuitem").map((m) => m.textContent)).toEqual([
      `Release lot${sentence}`, "View lot record (printable)", "Audit trail"
    ]);
    expect(screen.getByRole("menuitem", { name: /Release lot/ }).getAttribute("aria-disabled")).toBe("true");
  });

  it("clicking a row opens the split pane: the register narrows to Prepared On, Material / Lot and Status, with no row actions", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.click(screen.getByText("ML-3"));
    await waitFor(() => expect(screen.getAllByRole("columnheader").map((h) => h.textContent?.trim())).toEqual([
      "Prepared On", "Material / Lot", "Status"
    ]));
    expect(screen.queryByRole("button", { name: /Row actions for/ })).toBeNull();
    expect(registerRows()).toHaveLength(5);
    expect(registerRows()[2]).toContain("Lot: ML-3");
  });

  it("?tab=approved shows the Approved Media list instead", async () => {
    renderPage("/?tab=approved");
    await waitFor(() => expect(masterDataOptions.getReleasedMedia).toHaveBeenCalledTimes(1));
    // The page still loads its lots on mount while the other tab is showing.
    expect(MediaPreparationService.getAll).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("tab", { name: "Approved Media" }).getAttribute("aria-selected")).toBe("true");
  });

  it("a failed load shows the server message", async () => {
    vi.mocked(MediaPreparationService.getAll).mockRejectedValue({ response: { data: { message: "Lots unavailable." } } });
    renderPage();
    expect(await screen.findByText("Lots unavailable.")).toBeTruthy();
  });
  it("?mediaId= opens that lot's workspace once the lots have loaded", async () => {
    renderPage("/?mediaId=3");
    // The workspace header, not the register row (which also shows "ID #3").
    expect(await screen.findByRole("button", { name: /Close Workspace/ }, { timeout: 5000 })).toBeTruthy();
    expect(screen.getByText(/Prepared: .* ID #3$/)).toBeTruthy();
  });

  it("?mediaId= for a lot not in the list opens no workspace", async () => {
    renderPage("/?mediaId=99");
    await ready();
    expect(screen.queryByRole("button", { name: /Close Workspace/ })).toBeNull();
  });
});
