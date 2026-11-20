import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { tableViewService, type TableViewDto } from "../../../components/lab/table/services/tableViewService";
import { UserService } from "../../users/services/UserService";
import type { MediaLot } from "../../laboratoryConfiguration/mediaEvaluation/types/mediaEvaluationTypes";
import { ApprovedMediaListPage } from "./ApprovedMediaListPage";

// The Approved Media List on the shared table state (smarter-tables Task 10): filters and page in the address, saved views.

vi.mock("../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/masterDataOptions")>();
  return { ...actual, masterDataOptions: { getReleasedMedia: vi.fn() } };
});
vi.mock("../../users/services/UserService", () => ({ UserService: { getAll: vi.fn() } }));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));

type Row = MediaLot & {
  mediaType?: { class?: string };
  preparedByName?: string;
  preparedByUser?: { fullName?: string };
};

const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString();

const lot = (over: Partial<Row>): Row => ({
  id: 1,
  materialId: 10,
  material: {
    materialName: "Tryptic Soy Agar", batchNumber: "B-100", manufacturerName: "Oxoid"
  } as Row["material"],
  lotNumber: "ML-001",
  manufacturerLot: "MFG-LOT-1",
  manufacturerName: "Oxoid",
  totalWeight: 12.5,
  totalVolume: "500 mL",
  autoclaveEquipmentId: null,
  autoclaveProgram: "",
  loadType: "",
  temperature: 121,
  cycleTime: 20,
  cycleNumber: 1,
  ph: 7.3,
  expiryDate: day(120),
  preparedAt: "2026-10-01T08:00:00Z",
  preparedByUserId: 5,
  status: "Active",
  approvalStatus: "Approved",
  approvedByUserId: 6,
  approvedAt: "2026-10-02T08:00:00Z",
  isReleasedForUse: true,
  mediaType: { class: "GeneralAgar" },
  ...over
});

const ROWS: Row[] = [
  lot({ id: 1 }),
  lot({
    id: 2, lotNumber: "ML-002", material: { materialName: "Peptone Water", batchNumber: "B-200", manufacturerName: "Merck" } as Row["material"],
    manufacturerName: "Merck", manufacturerLot: "MFG-LOT-2", ph: 6.8, totalWeight: null as unknown as number, totalVolume: "",
    expiryDate: day(10), preparedByUserId: 9, status: "OutOfStock", isReleasedForUse: false, mediaType: { class: "SelectiveBroth" }
  }),
  lot({
    id: 3, lotNumber: "ML-003", material: null, manufacturerName: "", manufacturerLot: "", preparedByUserId: 0, ph: null as unknown as number,
    expiryDate: day(45), mediaType: { class: "GeneralAgar" }, preparedByName: "Named Preparer"
  })
];

function savedView(stateJson: string): TableViewDto {
  return {
    id: 7, tableKey: "inventory.approvedMedia", name: "Expiring", scope: "Personal", sectionId: null, sectionName: null,
    isMine: true, canEdit: true, isDefault: false, ownerName: "Analyst", stateJson
  };
}

const address = () => new URLSearchParams(screen.getByLabelText("Address").textContent ?? "");

function LocationProbe() {
  const location = useLocation();
  return <output aria-label="Address">{location.search}</output>;
}

function renderPage(initialUrl = "/") {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialUrl]}>
          <ApprovedMediaListPage />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

// The register only: the page also renders a print-only copy of the list.
const registerRoot = () => within(document.querySelector(".no-print") as HTMLElement);
const registerRows = () => registerRoot().getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const hasLot = (code: string) => registerRows().some((r) => r.includes(code));
const ready = () => waitFor(() => expect(registerRoot().getByText("ML-002")).toBeTruthy());
const SEARCH = "Search by lot number, media type, manufacturer, batch";

async function pick(user: ReturnType<typeof userEvent.setup>, comboName: string, option: string) {
  await user.click(screen.getByRole("combobox", { name: comboName }));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("ApprovedMediaListPage on the shared table state", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(masterDataOptions.getReleasedMedia).mockResolvedValue(ROWS);
    vi.mocked(UserService.getAll).mockResolvedValue([
      { id: 5, fullName: "Alice Analyst" },
      { id: 9, fullName: "Bob Biologist" }
    ] as never);
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("URL round trip: search and dropdown filters are in the address and restore on reload", async () => {
    const user = userEvent.setup();
    const first = renderPage();
    await ready();
    await user.type(screen.getByRole("textbox", { name: SEARCH }), "ml-");
    await pick(user, "Status", "Released / Active");
    await pick(user, "Expiry Date", "Expiring in 60 Days");
    await waitFor(() => expect(address().get("f.expiryRange")).toBe("expiring_60"));
    expect(address().get("q")).toBe("ml-");
    expect(address().get("f.status")).toBe("Released");
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasLot("ML-003")).toBe(true);
    }, { timeout: 5000 });
    const url = `/${screen.getByLabelText("Address").textContent}`;
    first.unmount();

    renderPage(url);
    await waitFor(() => expect(registerRoot().getByText("ML-003")).toBeTruthy());
    expect(registerRows()).toHaveLength(1);
    expect((screen.getByRole("textbox", { name: SEARCH }) as HTMLInputElement).value).toBe("ml-");
    expect((screen.getByRole("button", { name: "Reset" }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("the page number is in the address and a filter change returns to page 1", async () => {
    const many = Array.from({ length: 30 }, (_, i) => lot({ id: 100 + i, lotNumber: `ML-${String(100 + i)}` }));
    vi.mocked(masterDataOptions.getReleasedMedia).mockResolvedValue(many);
    const user = userEvent.setup();
    renderPage();
    await waitFor(() => expect(registerRows()).toHaveLength(25), { timeout: 5000 });
    await user.click(screen.getAllByRole("button", { name: "Go to next page" })[0]);
    await waitFor(() => expect(address().get("page")).toBe("2"));
    expect(registerRows()).toHaveLength(5);
    await pick(user, "Status", "Released / Active");
    await waitFor(() => expect(address().has("page")).toBe(false));
    expect(registerRows()).toHaveLength(25);
  });

  it("Reset clears the search and filters from the address", async () => {
    const user = userEvent.setup();
    renderPage("/?q=zzz&f.status=OutOfStock");
    await screen.findByText("No approved media lots match the selected filters.");
    await user.click(screen.getByRole("button", { name: "Reset Filters" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3), { timeout: 5000 });
    expect(address().has("q")).toBe(false);
    expect(address().has("f.status")).toBe(false);
  });

  it("saved view applies: its filters narrow the list and its columns are the only ones shown", async () => {
    vi.mocked(tableViewService.list).mockResolvedValue([
      savedView(JSON.stringify({
        v: 1, filters: { expiryRange: "expiring_30" }, search: "", sort: null, pageSize: 25,
        columns: ["lotNumber", "expiry"], density: "compact"
      }))
    ]);
    renderPage("/?view=7");
    await waitFor(() => {
      expect(registerRows()).toHaveLength(1);
      expect(hasLot("ML-002")).toBe(true);
    }, { timeout: 5000 });
    expect(registerRoot().getAllByRole("columnheader").map((h) => h.textContent?.trim())).toEqual([
      "Lot Number", "Expiry", "Actions"
    ]);
  });
});
