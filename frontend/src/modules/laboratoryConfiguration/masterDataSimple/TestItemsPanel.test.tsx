import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { TestItemsPanel } from "./TestItemsPanel";
import { TestItemsService, type TestItemRow, type TestItemsPage } from "./services/TestItemsService";

// Only the HTTP service layer is mocked; the real panel runs (CLAUDE.md Rule 4.5).
vi.mock("./services/TestItemsService", () => ({ TestItemsService: { getItems: vi.fn() } }));
const getItems = vi.mocked(TestItemsService.getItems);
const lastParams = () => getItems.mock.calls[getItems.mock.calls.length - 1]?.[1];

const row = (n: number, over: Partial<TestItemRow> = {}): TestItemRow => ({
  itemId: n,
  itemCode: `ITM-${String(n).padStart(3, "0")}`,
  itemName: `Product ${n}`,
  sopNumber: `SOP-${n}`,
  sopStatus: "Linked",
  documentMasterId: 900 + n,
  effectiveRevisionNumber: "02",
  uploadedDocumentCount: 0,
  autoAssignedOnReceipt: false,
  hasSpecification: true,
  ...over
});

const pageOf = (items: TestItemRow[], totalCount: number, page = 1): TestItemsPage => ({ items, page, pageSize: 10, totalCount });

function renderPanel() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <TestItemsPanel testId={5} />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

beforeEach(() => { getItems.mockReset(); });
afterEach(() => cleanup());

describe("TestItemsPanel", () => {
  it("panel shows total, pages and searches", async () => {
    const user = userEvent.setup();
    getItems.mockImplementation(async (_id, params) => {
      if (params.search === "needle") return pageOf([row(7, { itemName: "Needle product" })], 1);
      const start = (params.page - 1) * 10;
      return pageOf(Array.from({ length: Math.min(10, 300 - start) }, (_, i) => row(start + i + 1)), 300, params.page);
    });
    renderPanel();

    expect(await screen.findByRole("heading", { name: "Items using this test (300)" })).toBeTruthy();
    expect(screen.getAllByRole("listitem")).toHaveLength(10);
    expect(getItems.mock.calls[0][1]).toEqual({ search: "", page: 1, pageSize: 10 });

    await user.click(screen.getByRole("button", { name: /next page/i }));
    expect(await screen.findByText("ITM-011")).toBeTruthy();
    expect(lastParams()).toEqual({ search: "", page: 2, pageSize: 10 });

    await user.type(screen.getByRole("textbox", { name: "Search items" }), "needle");
    expect(await screen.findByRole("heading", { name: "Items using this test (1)" })).toBeTruthy();
    expect(lastParams()).toEqual({ search: "needle", page: 1, pageSize: 10 });
    expect(screen.getAllByRole("listitem")).toHaveLength(1);
    expect(screen.getByText("ITM-007")).toBeTruthy();
  });

  it("SOP status is text, not colour alone", async () => {
    getItems.mockResolvedValue(
      pageOf(
        [
          row(1),
          row(2, { sopStatus: "NotFound", sopNumber: "SOP-GONE", documentMasterId: null, effectiveRevisionNumber: null }),
          row(3, { sopStatus: "NoNumber", sopNumber: null, documentMasterId: null, effectiveRevisionNumber: null })
        ],
        3
      )
    );
    renderPanel();

    const items = await screen.findAllByRole("listitem");
    expect(within(items[0]).getByText("SOP: Linked to Document Control")).toBeTruthy();
    expect(within(items[0]).getByText("Revision 02")).toBeTruthy();
    expect(within(items[1]).getByText("SOP: Not in Document Control")).toBeTruthy();
    expect(within(items[2]).getByText("SOP: No SOP number")).toBeTruthy();
  });

  it('"Item documents" link opens the item on its Documents tab; "Open SOP" only when linked', async () => {
    getItems.mockResolvedValue(
      pageOf(
        [
          row(1),
          row(2, { sopStatus: "NotFound", documentMasterId: null }),
          row(3, { sopStatus: "NoNumber", sopNumber: null, documentMasterId: null })
        ],
        3
      )
    );
    renderPanel();

    const items = await screen.findAllByRole("listitem");
    expect(within(items[0]).getByRole("link", { name: "Item documents for ITM-001" }).getAttribute("href")).toBe(
      "/laboratory-configuration/items?item=1&tab=documents"
    );
    expect(within(items[0]).getByRole("link", { name: "Open SOP for ITM-001" }).getAttribute("href")).toBe("/document-control/documents/901");
    for (const i of [1, 2]) {
      expect(within(items[i]).getByRole("link", { name: /Item documents/ })).toBeTruthy();
      expect(within(items[i]).queryByRole("link", { name: /Open SOP/ })).toBeNull();
    }
  });

  it("says so when no item uses the test, and shows a retry when loading fails", async () => {
    const user = userEvent.setup();
    getItems.mockRejectedValueOnce(new Error("network"));
    renderPanel();

    expect(await screen.findByText("The items that use this test could not be loaded.")).toBeTruthy();
    getItems.mockResolvedValueOnce(pageOf([], 0));
    await user.click(screen.getByRole("button", { name: "Retry" }));

    await waitFor(() => expect(screen.getByText("No item uses this test yet.")).toBeTruthy());
    expect(screen.getByRole("heading", { name: "Items using this test (0)" })).toBeTruthy();
  });
});
