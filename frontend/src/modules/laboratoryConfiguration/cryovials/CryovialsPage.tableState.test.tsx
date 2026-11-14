import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { labelService } from "../../../services/labelService";
import { tableViewService, type TableViewDto } from "../../../components/lab/table/services/tableViewService";
import { CryovialService } from "./services/CryovialService";
import type { CryovialItem } from "./types/cryovialTypes";
import { CryovialsPage } from "./CryovialsPage";

// Cryovials register on the shared table state (smarter-tables Task 10): filters in the address, saved views.

vi.mock("../../../services/labelService", () => ({
  labelService: { getStatus: vi.fn(), print: vi.fn(), getFormats: vi.fn(), updateFormat: vi.fn(), resolveScan: vi.fn() }
}));
vi.mock("../../../components/lab/table/services/tableViewService", () => ({
  tableViewService: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() }
}));
vi.mock("./services/CryovialService", () => ({
  CryovialService: {
    getAll: vi.fn(),
    prepare: vi.fn(),
    approve: vi.fn(),
    destroy: vi.fn(),
    moveStorage: vi.fn(),
    thawVial: vi.fn()
  }
}));

function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const day = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString();

const cryovial = (over: Partial<CryovialItem> & { id: number }): CryovialItem => ({
  code: `CV-${String(over.id).padStart(3, "0")}`,
  materialId: 10,
  material: { id: 10, materialName: "E. coli lyophilised", batchNumber: "B-100", manufacturerName: "ATCC Direct" },
  organismId: 20,
  organism: { id: 20, scientificName: "Escherichia coli", atccNumber: "8739", strainNumber: "S-1" },
  organismNameSnapshot: "Escherichia coli",
  manufacturerName: "ATCC Direct",
  expiryDate: day(200),
  numberOfVialsPrepared: 10,
  vialsRemaining: 6,
  storageCondition: "Deep Freezer · DF-01",
  physicalCheckConfirmed: true,
  physicalCheckText: "Intact",
  preparedAt: "2026-10-01T08:00:00Z",
  preparedByUserId: 5,
  approvalStatus: "Approved",
  isDestroyed: false,
  ...over
});

const ROWS: CryovialItem[] = [
  cryovial({ id: 1 }),
  cryovial({
    id: 2,
    code: "CV-002",
    organism: { id: 21, scientificName: "Bacillus subtilis", atccNumber: "6633" },
    organismNameSnapshot: "Bacillus subtilis",
    manufacturerName: "Microbiologics",
    approvalStatus: "PendingReview",
    expiryDate: day(15)
  }),
  cryovial({
    id: 3,
    code: "CV-003",
    organism: { id: 22, scientificName: "Candida albicans", atccNumber: "10231" },
    organismNameSnapshot: "Candida albicans",
    manufacturerName: "CultiLoops",
    approvalStatus: "Approved",
    expiryDate: day(10)
  }),
  cryovial({
    id: 4,
    code: "CV-004",
    approvalStatus: "Approved",
    expiryDate: day(-5)
  })
];

function savedView(stateJson: string): TableViewDto {
  return {
    id: 7,
    tableKey: "labConfig.cryovials",
    name: "Expiring Candida",
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
          <CryovialsPage />
          <LocationProbe />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const address = () => new URLSearchParams(screen.getByLabelText("Address").textContent ?? "");
const registerRows = () => screen.getAllByRole("row").slice(1).map((r) => r.textContent ?? "");
const hasCode = (code: string) => registerRows().some((r) => r.includes(code));
const ready = () => waitFor(() => expect(screen.getByText("CV-001")).toBeTruthy());
const SEARCH = "Search by code, organism, material...";

const hasHeader = (label: string) =>
  screen.getAllByRole("columnheader").some((h) => h.textContent?.trim() === label);

async function pickSelect(user: ReturnType<typeof userEvent.setup>, label: string, option: string) {
  await user.click(screen.getByLabelText(label));
  await user.click(await screen.findByRole("option", { name: option }));
}

describe("CryovialsPage on the shared table state", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    seedPermissions([PERMISSIONS.CRYOVIALS_MANAGE, PERMISSIONS.CRYOVIALS_APPROVE]);
    vi.mocked(CryovialService.getAll).mockResolvedValue(ROWS);
    vi.mocked(labelService.getStatus).mockResolvedValue([]);
    vi.mocked(tableViewService.list).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("URL round trip: search, organism, status and expiry filters are in the address and restore on reload", async () => {
    const user = userEvent.setup();
    const first = renderPage("/");
    await ready();

    await user.type(screen.getByRole("textbox", { name: SEARCH }), "cultiloops");
    await pickSelect(user, "Organism", "Candida albicans");
    await pickSelect(user, "Status", "Approved");
    await pickSelect(user, "Expiry", "Expiring in 30 Days");

    await waitFor(() => expect(address().get("f.expiryRange")).toBe("expiring_30"));
    expect(address().get("q")).toBe("cultiloops");
    expect(address().get("f.organism")).toBe("Candida albicans");
    expect(address().get("f.status")).toBe("Approved");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-003")).toBe(true);
    const url = `/${screen.getByLabelText("Address").textContent}`;
    const before = registerRows();
    first.unmount();

    renderPage(url);
    await waitFor(() => expect(screen.getByText("CV-003")).toBeTruthy());
    expect(registerRows()).toEqual(before);
    expect((screen.getByRole("textbox", { name: SEARCH }) as HTMLInputElement).value).toBe("cultiloops");
    expect(screen.getByLabelText("Organism").textContent).toBe("Candida albicans");
    expect(screen.getByLabelText("Status").textContent).toBe("Approved");
    expect(screen.getByLabelText("Expiry").textContent).toBe("Expiring in 30 Days");
  });

  it("Reset clears every filter from the address", async () => {
    const user = userEvent.setup();
    renderPage("/?f.organism=Candida+albicans&f.status=Rejected&f.expiryRange=expired&q=missing");
    await waitFor(() => expect(screen.getByText("No cryovials matching the filter criteria")).toBeTruthy());

    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(4));
    expect(address().has("f.organism")).toBe(false);
    expect(address().has("f.status")).toBe(false);
    expect(address().has("f.expiryRange")).toBe(false);
    expect(address().has("q")).toBe(false);
  });

  it("saved view applies: its filters narrow the list and its columns are the only ones shown", async () => {
    vi.mocked(tableViewService.list).mockResolvedValue([
      savedView(
        JSON.stringify({
          v: 1,
          filters: { organism: "Candida albicans", status: "Approved", expiryRange: "expiring_30" },
          search: "",
          sort: null,
          pageSize: 25,
          columns: ["code", "organism", "expiryDate"],
          density: "compact"
        })
      )
    ]);

    renderPage("/?view=7");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-003")).toBe(true);
    expect(screen.getAllByRole("columnheader").map((h) => h.textContent?.trim()).filter(Boolean)).toEqual([
      "Code", "Organism & Source", "Expiry", "Actions"
    ]);
  });

  it("the toolbar offers saved views and a column chooser that hides and shows a column", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    expect(screen.getByRole("button", { name: /Views/ })).toBeTruthy();
    expect(hasHeader("Prepared")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "Prepared" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("Prepared")).toBe(false));
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "Prepared" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("Prepared")).toBe(true));
  });
});
