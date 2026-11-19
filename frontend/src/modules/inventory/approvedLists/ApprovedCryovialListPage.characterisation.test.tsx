import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import { tableViewService } from "../../../components/lab/table/services/tableViewService";
import { CryovialService } from "../../laboratoryConfiguration/cryovials/services/CryovialService";
import type { CryovialItem } from "../../laboratoryConfiguration/cryovials/types/cryovialTypes";
import { ApprovedCryovialListPage } from "./ApprovedCryovialListPage";

// Pins the Approved Cryovial List: columns, every filter, the Thaw Vial and Report actions
// and the exact service calls. The same assertions hold before and after the move onto RegisterTable.

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

describe("ApprovedCryovialListPage register", () => {
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

  it("loads the cryovials once, keeps only approved non-destroyed unexpired ones, and shows every column", async () => {
    renderPage();
    await ready();
    expect(CryovialService.getAll).toHaveBeenCalledTimes(1);
    expect(CryovialService.getAll).toHaveBeenCalledWith();
    expect(registerRoot().getAllByRole("columnheader").map((h) => h.textContent?.trim())).toEqual([
      "Cryovial Code", "Organism", "ATCC No.", "Manufacturer", "Storage", "Vials Remaining", "Expiry", "Status", "Actions"
    ]);
    expect(registerRows()).toHaveLength(3);
    expect(hasCode("CV-HIDDEN")).toBe(false);
    const [first, second, third] = registerRows();
    expect(first).toContain("CV-001");
    expect(first).toContain("Batch: B-100");
    expect(first).toContain("Escherichia coli");
    expect(first).toContain("E. coli lyophilised");
    expect(first).toContain("8739");
    expect(first).toContain("ATCC Direct");
    expect(first).toContain("-80 C");
    expect(first).toContain("6 of 10");
    expect(first).toContain("Approved");
    expect(first).not.toContain("Expiring soon");
    expect(second).toContain("Microbiologics");
    expect(second).toContain("0 of 10");
    expect(second).toContain("Expiring soon");
    expect(second).toContain("Depleted");
    expect(third).toContain("Candida albicans");
    expect(registerRoot().getByText("3 approved cryovials found")).toBeTruthy();
  });

  it("each row has a Report link to a new tab; Thaw Vial is offered only while vials remain", async () => {
    renderPage();
    await ready();
    const links = registerRoot().getAllByRole("link", { name: "View cryovial report record" });
    expect(links.map((l) => l.getAttribute("href"))).toEqual(["/cryovials/1/report", "/cryovials/2/report", "/cryovials/3/report"]);
    for (const l of links) {
      expect(l.getAttribute("target")).toBe("_blank");
      expect(l.getAttribute("rel")).toBe("noopener");
    }
    const thaw = registerRoot().getAllByRole("button", { name: "Thaw a single vial from this approved batch" });
    expect(thaw).toHaveLength(2);
    const rows = registerRoot().getAllByRole("row").slice(1);
    expect(within(rows[1]).queryByRole("button", { name: /Thaw a single vial/ })).toBeNull();
  });

  it("Thaw Vial asks for a reason of at least 5 characters, thaws with that reason, reloads and confirms", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    const rows = registerRoot().getAllByRole("row").slice(1);
    await user.click(within(rows[0]).getByRole("button", { name: /Thaw a single vial/ }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Thaw Vial: Provide Reason")).toBeTruthy();
    const confirm = within(dialog).getByRole("button", { name: "Confirm Thaw" }) as HTMLButtonElement;
    expect(confirm.disabled).toBe(true);
    await user.type(within(dialog).getByRole("textbox", { name: "Enter reason for thawing this cryovial" }), "abc");
    expect(confirm.disabled).toBe(true);
    await user.type(within(dialog).getByRole("textbox", { name: "Enter reason for thawing this cryovial" }), "de");
    expect(confirm.disabled).toBe(false);
    await user.click(confirm);
    await waitFor(() => expect(CryovialService.thawVial).toHaveBeenCalledWith(1, "abcde"));
    expect(await screen.findByText("Vial from batch CV-001 successfully thawed and recorded.")).toBeTruthy();
    await waitFor(() => expect(CryovialService.getAll).toHaveBeenCalledTimes(2));
  });

  it("search narrows by code, organism, ATCC no., strain, manufacturer, storage, material and batch, and Reset restores the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    const reset = () => screen.getByRole("button", { name: "Reset" }) as HTMLButtonElement;
    expect(reset().disabled).toBe(true);
    const search = screen.getByRole("textbox", { name: SEARCH });
    await user.type(search, "subtilis");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-002")).toBe(true);
    expect(reset().disabled).toBe(false);
    await user.clear(search);
    await user.type(search, "b-100");
    await waitFor(() => expect(hasCode("CV-001")).toBe(true));
    expect(registerRows()).toHaveLength(1);
    await user.clear(search);
    await user.type(search, "s-1");
    await waitFor(() => expect(hasCode("CV-001")).toBe(true));
    expect(registerRows()).toHaveLength(1);
    await user.click(reset());
    await waitFor(() => expect(registerRows()).toHaveLength(3));
    expect(reset().disabled).toBe(true);
  });

  it("organism, storage, manufacturer, status and expiry dropdowns each filter the list", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();

    await pick(user, "Organism", "Candida albicans");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-003")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Storage", "-20 C");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Manufacturer", "Microbiologics");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Status", "Depleted (0 Vials)");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Status", "Available / In Stock");
    await waitFor(() => expect(registerRows()).toHaveLength(2));
    expect(hasCode("CV-002")).toBe(false);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Expiry Date", "Expiring in 30 Days");
    await waitFor(() => expect(registerRows()).toHaveLength(1));
    expect(hasCode("CV-002")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Reset" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));

    await pick(user, "Expiry Date", "Expiring in 60 Days");
    await waitFor(() => expect(registerRows()).toHaveLength(2));
    expect(hasCode("CV-001")).toBe(false);
  });

  it("an empty result explains the filters and offers Reset Filters; an empty list says so", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    await user.type(screen.getByRole("textbox", { name: SEARCH }), "no-such-vial");
    expect(await screen.findByText("No approved cryovials match the selected filters.")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Reset Filters" }));
    await waitFor(() => expect(registerRows()).toHaveLength(3));
  });

  it("with no approved cryovials the page says none are available", async () => {
    vi.mocked(CryovialService.getAll).mockResolvedValue([]);
    renderPage();
    expect(await screen.findByText("No approved cryovials currently available in inventory.")).toBeTruthy();
  });

  it("a failed load shows the error", async () => {
    vi.mocked(CryovialService.getAll).mockRejectedValue(new Error("down"));
    renderPage();
    expect(await screen.findByText("Failed to retrieve approved cryovials register. Please try again.")).toBeTruthy();
  });

  it("the Columns chooser hides and shows a column", async () => {
    const user = userEvent.setup();
    renderPage();
    await ready();
    const hasHeader = (label: string) =>
      registerRoot().getAllByRole("columnheader").some((h) => h.textContent?.trim() === label);
    expect(hasHeader("ATCC No.")).toBe(true);
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "ATCC No." }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("ATCC No.")).toBe(false));
    await user.click(screen.getByRole("button", { name: "Columns" }));
    await user.click(await screen.findByRole("checkbox", { name: "ATCC No." }));
    await user.keyboard("{Escape}");
    await waitFor(() => expect(hasHeader("ATCC No.")).toBe(true));
  });
});
