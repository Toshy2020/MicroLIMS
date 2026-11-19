import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { UserService } from "../../users/services/UserService";
import type { MediaLot } from "../../laboratoryConfiguration/mediaEvaluation/types/mediaEvaluationTypes";
import { ApprovedMediaListPage } from "./ApprovedMediaListPage";

// Pins the Approved Media List: columns, every filter, the Record link and the exact
// service calls. The same assertions hold before and after the move onto RegisterTable.

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

describe("ApprovedMediaListPage register", () => {
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

  it("loads the released media and the user names once, and shows every column and cell format", async () => {
    renderPage();
    await ready();
    expect(masterDataOptions.getReleasedMedia).toHaveBeenCalledTimes(1);
    expect(masterDataOptions.getReleasedMedia).toHaveBeenCalledWith();
    expect(UserService.getAll).toHaveBeenCalledTimes(1);
    expect(UserService.getAll).toHaveBeenCalledWith("all");
    expect(registerRoot().getAllByRole("columnheader").map((h) => h.textContent?.trim())).toEqual([
      "Lot Number", "Media Type", "pH Result", "Prepared By", "Total Volume", "Total Weight", "Manufacturer Lot",
      "Manufacturer", "Prepared On", "Expiry", "Status", "Actions"
    ]);
    const [first, second, third] = registerRows();
    expect(first).toContain("ML-001");
    expect(first).toContain("Batch: B-100");
    expect(first).toContain("Tryptic Soy Agar");
    expect(first).toContain("General Agar");
    expect(first).toContain("7.30");
    expect(first).toContain("Alice Analyst");
    expect(first).toContain("500 mL");
    expect(first).toContain("12.5 g");
    expect(first).toContain("MFG-LOT-1");
    expect(first).toContain("Released");
    expect(first).not.toContain("Expiring soon");
    expect(second).toContain("Bob Biologist");
    expect(second).toContain("6.80");
    expect(second).toContain("Expiring soon");
    expect(second).toContain("Out of Stock");
    expect(third).toContain("Named Preparer");
    expect(registerRoot().getByText("3 released media lots found")).toBeTruthy();
  });

  it("every row has a Record link that opens the lot report in a new tab", async () => {
    renderPage();
    await ready();
    const links = registerRoot().getAllByRole("link", { name: "View media lot report record" });
    expect(links.map((l) => l.getAttribute("href"))).toEqual(["/media/1/report", "/media/2/report", "/media/3/report"]);
    for (const l of links) {
      expect(l.getAttribute("target")).toBe("_blank");
      expect(l.getAttribute("rel")).toBe("noopener");
    }
  });

  it("the KPI cards count the loaded lots", async () => {
    renderPage();
    await ready();
    const card = (label: string) => (registerRoot().getByText(label).closest("div")?.parentElement?.textContent ?? "");
    expect(card("Total Media Lots")).toContain("3");
    expect(card("Expiring Soon")).toContain("1");
  });

  it("search narrows by lot, media type, material name, batch, manufacturer lot and manufacturer, and Reset restores the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    const reset = () => screen.getByRole("button", { name: "Reset" });
    expect((reset() as HTMLButtonElement).disabled).toBe(true);
    const search = screen.getByRole("textbox", { name: SEARCH });
    await user.type(search, "peptone");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("ML-002")).toBe(true);
    expect((reset() as HTMLButtonElement).disabled).toBe(false);
    await user.clear(search);
    await user.type(search, "mfg-lot-1");
    await waitFor(() => expect(hasLot("ML-001")).toBe(true));
    expect(registerRows()).toHaveLength(1);
    await user.clear(search);
    await user.type(search, "selective broth");
    await waitFor(() => expect(hasLot("ML-002")).toBe(true));
    expect(registerRows()).toHaveLength(1);
    await user.click(reset());
    await waitFor(() => expect(registerRows()).toHaveLength(3));
    expect((reset() as HTMLButtonElement).disabled).toBe(true);
  });

  it("media type, manufacturer, status and expiry dropdowns each filter the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await pick(user, "Media Type", "Selective Broth");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("ML-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Manufacturer", "Merck");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("ML-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Status", "Out of Stock");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("ML-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Status", "Released / Active");
    await waitFor(() => expect(registerRows()).toHaveLength(2));
    expect(hasLot("ML-002")).toBe(false);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Expiry Date", "Expiring in 30 Days");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasLot("ML-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Expiry Date", "Expiring in 60 Days");
    await waitFor(() => expect(registerRows()).toHaveLength(2));
    expect(hasLot("ML-001")).toBe(false);
  });

  it("an empty result explains the filters and offers Reset Filters; an empty list says so", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.type(screen.getByRole("textbox", { name: SEARCH }), "no-such-lot");
    expect(await screen.findByText("No approved media lots match the selected filters.")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Reset Filters" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));
  });

  it("with no released lots the page says none are available", async () => {
    vi.mocked(masterDataOptions.getReleasedMedia).mockResolvedValue([]);
    renderPage();
    expect(await screen.findByText("No approved media lots currently available in inventory.")).toBeTruthy();
  });

  it("a failed load shows the error and keeps the page usable", async () => {
    vi.mocked(masterDataOptions.getReleasedMedia).mockRejectedValue(new Error("down"));
    renderPage();
    expect(await screen.findByText("Failed to retrieve approved media register. Please try again.")).toBeTruthy();
  });

  it("the Columns chooser hides and shows a column", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    const hasHeader = (label: string) =>
      registerRoot().getAllByRole("columnheader").some((h) => h.textContent?.trim() === label);
    expect(hasHeader("pH Result")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "pH Result" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("pH Result")).toBe(false));
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "pH Result" }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("pH Result")).toBe(true));
  });
});
