import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { tableViewService, type TableViewDto } from "../../../components/lab/table/services/tableViewService";
import { CryovialService } from "../../laboratoryConfiguration/cryovials/services/CryovialService";
import type { CryovialItem } from "../../laboratoryConfiguration/cryovials/types/cryovialTypes";
import { ApprovedCryovialListPage } from "./ApprovedCryovialListPage";

// The Approved Cryovial List on the shared table state (smarter-tables Task 10): filters and page in the address, saved views.

vi.mock("../../laboratoryConfiguration/cryovials/services/CryovialService", () => ({
  CryovialService: { getAll: vi.fn(), thawVial: vi.fn() }
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));

const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString();

const vial = (over: Partial<CryovialItem>): CryovialItem => ({
  id: 1,
  code: "CV-001",
  materialId: 10,
  material: { id: 10, materialName: "E. coli lyophilised", batchNumber: "B-100", manufacturerName: "ATCC Direct" },
  organism: { id: 20, scientificName: "Escherichia coli", atccNumber: "8739", strainNumber: "S-1" },
  organismNameSnapshot: "Escherichia coli",
  manufacturerName: "ATCC Direct",
  expiryDate: day(200),
  numberOfVialsPrepared: 10,
  vialsRemaining: 6,
  storageCondition: "-80 C",
  physicalCheckConfirmed: true,
  physicalCheckText: "ok",
  preparedAt: "2026-10-01T08:00:00Z",
  preparedByUserId: 5,
  approvalStatus: "Approved",
  isDestroyed: false,
  ...over
});

// getAll returns every cryovial; the page keeps the approved, non-destroyed, unexpired ones.
const ALL: CryovialItem[] = [
  vial({ id: 1 }),
  vial({
    id: 2, code: "CV-002", organism: { id: 21, scientificName: "Bacillus subtilis", atccNumber: "6633" },
    organismNameSnapshot: "Bacillus subtilis", manufacturerName: "", material: { id: 11, materialName: "B. subtilis", batchNumber: "B-200", manufacturerName: "Microbiologics" },
    storageCondition: "-20 C", vialsRemaining: 0, expiryDate: day(10)
  }),
  vial({
    id: 3, code: "CV-003", organism: undefined, organismNameSnapshot: "Candida albicans", material: undefined,
    manufacturerName: "Culti", storageCondition: "", expiryDate: day(45)
  }),
  vial({ id: 4, code: "CV-HIDDEN-PENDING", approvalStatus: "PendingReview" }),
  vial({ id: 5, code: "CV-HIDDEN-DESTROYED", isDestroyed: true }),
  vial({ id: 6, code: "CV-HIDDEN-EXPIRED", expiryDate: day(-3) })
];

function savedView(stateJson: string): TableViewDto {
  return {
    id: 7, tableKey: "inventory.approvedCryovials", name: "Expiring", scope: "Personal", sectionId: null, sectionName: null,
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
          <ApprovedCryovialListPage />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

// The register only: the page also renders a print-only copy of the list.
const registerRoot = () => within(document.querySelector(".no-print") as HTMLElement);
const registerRows = () => registerRoot().getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const hasCode = (code: string) => registerRows().some((r) => r.includes(code));
const ready = () => waitFor(() => expect(registerRoot().getByText("CV-002")).toBeTruthy());
const SEARCH = "Search by cryovial code, organism, ATCC no., manufacturer";

async function pick(user: ReturnType<typeof userEvent.setup>, comboName: string, option: string) {
  await user.click(screen.getByRole("combobox", { name: comboName }));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("ApprovedCryovialListPage on the shared table state", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(CryovialService.getAll).mockResolvedValue(ALL);
    vi.mocked(CryovialService.thawVial).mockResolvedValue({});
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
    await user.type(screen.getByRole("textbox", { name: SEARCH }), "cv-");
    await pick(user, "Status", "Available / In Stock");
    await pick(user, "Expiry Date", "Expiring in 60 Days");
    await waitFor(() => expect(address().get("f.expiryRange")).toBe("expiring_60"));
    expect(address().get("q")).toBe("cv-");
    expect(address().get("f.status")).toBe("in_stock");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-003")).toBe(true);
    const url = `/${screen.getByLabelText("Address").textContent}`;
    first.unmount();

    renderPage(url);
    await waitFor(() => expect(registerRoot().getByText("CV-003")).toBeTruthy());
    expect(registerRows()).toHaveLength(1);
    expect((screen.getByRole("textbox", { name: SEARCH }) as HTMLInputElement).value).toBe("cv-");
    expect((screen.getByRole("button", { name: "Reset" }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("the page number is in the address and a filter change returns to page 1", async () => {
    const many = Array.from({ length: 30 }, (_, i) => vial({ id: 100 + i, code: `CV-${String(100 + i)}` }));
    vi.mocked(CryovialService.getAll).mockResolvedValue(many);
    const user = userEvent.setup();
    renderPage();
    await waitFor(() => expect(registerRows()).toHaveLength(25));
    await user.click(screen.getAllByRole("button", { name: "Go to next page" })[0]);
    await waitFor(() => expect(address().get("page")).toBe("2"));
    expect(registerRows()).toHaveLength(5);
    await pick(user, "Status", "Available / In Stock");
    await waitFor(() => expect(address().has("page")).toBe(false));
    expect(registerRows()).toHaveLength(25);
  });

  it("Reset clears the search and filters from the address", async () => {
    const user = userEvent.setup();
    renderPage("/?q=zzz&f.status=depleted");
    await screen.findByText("No approved cryovials match the selected filters.");
    await user.click(screen.getByRole("button", { name: "Reset Filters" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));
    expect(address().has("q")).toBe(false);
    expect(address().has("f.status")).toBe(false);
  });

  it("saved view applies: its filters narrow the list and its columns are the only ones shown", async () => {
    vi.mocked(tableViewService.list).mockResolvedValue([
      savedView(JSON.stringify({
        v: 1, filters: { status: "depleted" }, search: "", sort: null, pageSize: 25,
        columns: ["code", "vials"], density: "compact"
      }))
    ]);
    renderPage("/?view=7");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-002")).toBe(true);
    expect(registerRoot().getAllByRole("columnheader").map((h) => h.textContent?.trim())).toEqual([
      "Cryovial Code", "Vials Remaining", "Actions"
    ]);
  });
});
