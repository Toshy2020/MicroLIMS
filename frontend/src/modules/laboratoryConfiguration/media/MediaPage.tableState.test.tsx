import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { apiClient } from "../../../services/apiClient";
import { labelService } from "../../../services/labelService";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { tableViewService, type TableViewDto } from "../../../components/lab/table/services/tableViewService";
import { AuditSearchService } from "../../auditSearch/services/AuditSearchService";
import { MediaEvaluationService } from "../mediaEvaluation/services/MediaEvaluationService";
import type { MediaLot } from "../mediaEvaluation/types/mediaEvaluationTypes";
import { MediaPreparationService } from "./services/MediaPreparationService";
import { MediaPage } from "./MediaPage";

// Media Lots register on the shared table state (smarter-tables Task 10): filters in the address, saved views.

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

function savedView(stateJson: string): TableViewDto {
  return {
    id: 7,
    tableKey: "labConfig.mediaLots",
    name: "Sabouraud Rejected",
    scope: "Personal",
    sectionId: null,
    sectionName: null,
    isMine: true,
    canEdit: true,
    isDefault: false,
    ownerName: "SectionHead",
    stateJson
  };
}

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

const address = () => new URLSearchParams(screen.getByLabelText("Address").textContent ?? "");
const registerRows = () => screen.getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const hasLot = (code: string) => registerRows().some((r) => r.includes(code));
const ready = () => waitFor(() => expect(screen.getByText("ML-2")).toBeTruthy());
const SEARCH = "Search by lot number, dehydrated material, batch…";

const hasHeader = (label: string) =>
  screen.getAllByRole("columnheader").some((h) => h.textContent?.trim() === label);

async function pickSelect(user: ReturnType<typeof userEvent.setup>, comboName: string, option: string) {
  await user.click(screen.getByRole("combobox", { name: comboName }));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("MediaPage on the shared table state", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.MEDIA_PREPARE, PERMISSIONS.MEDIA_RELEASE]);
    vi.mocked(MediaPreparationService.getAll).mockResolvedValue(LOTS);
    vi.mocked(MediaPreparationService.getAwaitingApproval).mockResolvedValue([{ id: 2 }]);
    vi.mocked(MediaPreparationService.getUsableSolutions).mockResolvedValue([]);
    vi.mocked(MediaEvaluationService.getAll).mockResolvedValue([]);
    vi.mocked(masterDataOptions.getMaterials).mockResolvedValue(MATERIALS);
    vi.mocked(masterDataOptions.getReleasedMedia).mockResolvedValue([]);
    vi.mocked(AuditSearchService.getForEntity).mockResolvedValue([]);
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: null } });
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("URL round trip: search, material dropdown and status/KPI filter are in the address and restore on reload", async () => {
    const user = userEvent.setup();
    const first = renderPage("/");
    await ready();

    await user.type(screen.getByRole("textbox", { name: SEARCH }), "ox-2602");
    await pickSelect(user, "Material", "Sabouraud Dextrose Agar Powder");
    await user.click(screen.getByRole("button", { name: /^Rejected/ }));

    await waitFor(() => expect(address().get("f.status")).toBe("Rejected"));
    expect(address().get("q")).toBe("ox-2602");
    expect(address().get("f.materialId")).toBe("302");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("ML-5")).toBe(true);
    const url = `/${screen.getByLabelText("Address").textContent}`;
    const before = registerRows();
    first.unmount();

    renderPage(url);
    await waitFor(() => expect(screen.getByText("ML-5")).toBeTruthy());
    expect(registerRows()).toEqual(before);
    expect((screen.getByRole("textbox", { name: SEARCH }) as HTMLInputElement).value).toBe("ox-2602");
    expect(screen.getByRole("combobox", { name: "Material" }).textContent).toBe("Sabouraud Dextrose Agar Powder");
    expect(screen.getByRole("combobox", { name: "Status" }).textContent).toBe("Rejected");
    expect(screen.getByRole("button", { name: /^Rejected \(filtered\)/ })).toBeTruthy();
  });

  it("Reset Filters clears every filter from the address", async () => {
    const user = userEvent.setup();
    renderPage("/?f.materialId=301&f.status=Released&q=missing");
    await waitFor(() => expect(screen.getByText("No media lots match this filter")).toBeTruthy());

    await user.click(screen.getByRole("button", { name: "Reset Filters" }));
    await waitFor(() => expect(registerRows()).toHaveLength(5));
    expect(address().has("f.materialId")).toBe(false);
    expect(address().has("f.status")).toBe(false);
    expect(address().has("q")).toBe(false);
  });

  it("saved view applies: its filters narrow the list and its columns are the only ones shown", async () => {
    vi.mocked(tableViewService.list).mockResolvedValue([
      savedView(
        JSON.stringify({
          v: 1,
          filters: { materialId: "302", status: "Rejected" },
          search: "",
          sort: null,
          pageSize: 25,
          columns: ["preparedAt", "lotNumber", "status"],
          density: "compact"
        })
      )
    ]);

    renderPage("/?view=7");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("ML-5")).toBe(true);
    expect(screen.getAllByRole("columnheader").map((h) => h.textContent?.trim()).filter(Boolean)).toEqual([
      "Prepared On", "Lot Number", "Status", "Actions"
    ]);
  });

  it("the toolbar offers saved views and a column chooser that hides and shows a column", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    expect(screen.getByRole("button", { name: /Views/ })).toBeTruthy();
    expect(hasHeader("Dehydrated Material")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "Dehydrated Material" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("Dehydrated Material")).toBe(false));
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "Dehydrated Material" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("Dehydrated Material")).toBe(true));
  });
});
