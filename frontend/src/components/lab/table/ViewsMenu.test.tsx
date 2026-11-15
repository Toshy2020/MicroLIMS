import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import {
  laboratorySectionService,
  type LaboratorySection,
} from "../../../services/laboratorySectionService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { TableToolbar } from "./TableToolbar";
import {
  tableViewService,
  type TableViewDto,
} from "./services/tableViewService";
import { useTableState } from "./useTableState";
import { resetUiStateCache } from "../../../hooks/useUiState";
import { resetFakeUiState, storedFavourites } from "../../../modules/navigation/favouritesTestUtils";

vi.mock("../../../services/uiStateService", async () => {
  const { fakeUiStateService: service } = await import("../../../modules/navigation/favouritesTestUtils");
  return { uiStateService: service };
});

vi.mock("./services/tableViewService", () => ({
  tableViewService: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    setDefault: vi.fn(),
  },
}));

vi.mock("../../../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getSections: vi.fn(),
    getMySections: vi.fn(),
    getUserMemberships: vi.fn(),
    replaceUserMemberships: vi.fn(),
  },
}));

const mockedList = vi.mocked(tableViewService.list);
const mockedCreate = vi.mocked(tableViewService.create);
const mockedUpdate = vi.mocked(tableViewService.update);
const mockedRemove = vi.mocked(tableViewService.remove);
const mockedSetDefault = vi.mocked(tableViewService.setDefault);
const mockedGetMySections = vi.mocked(laboratorySectionService.getMySections);

function makeView(overrides: Partial<TableViewDto> = {}): TableViewDto {
  return {
    id: 1,
    tableKey: "inventory.equipment",
    name: "Active equipment",
    scope: "Personal",
    sectionId: null,
    sectionName: null,
    stateJson: JSON.stringify({
      v: 1,
      filters: { status: "Active" },
      search: "",
      sort: null,
      pageSize: 25,
      columns: ["code", "status"],
      density: "comfortable",
    }),
    isMine: true,
    canEdit: true,
    isDefault: false,
    ownerName: "Ada Analyst",
    ...overrides,
  };
}

function makeAxiosFieldError(
  field: string,
  message: string,
  topMessage?: string
): AxiosError {
  const headers = new AxiosHeaders();
  const error = new AxiosError(
    topMessage ?? message,
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      status: 400,
      statusText: "Bad Request",
      headers,
      config: { headers },
      data: {
        success: false,
        message: topMessage ?? message,
        fieldErrors: [{ field, message }],
      },
    }
  );
  return error;
}

function ViewsHarness() {
  const ts = useTableState("inventory.equipment");
  const loc = useLocation();
  return (
    <div>
      <TableToolbar tableKey="inventory.equipment" tableState={ts} />
      <button type="button" onClick={() => ts.setFilter("status", "Retired")}>
        Filter Retired
      </button>
      <div data-testid="status">{String(ts.state.filters.status ?? "")}</div>
      <div data-testid="columns">{ts.state.columns.join(",")}</div>
      <div data-testid="location-search">{loc.search}</div>
      <div data-testid="view-id">{ts.viewId ?? ""}</div>
    </div>
  );
}

function renderHarness(
  initialEntry = "/",
  permissions: string[] = []
) {
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
  const router = createMemoryRouter(
    [{ path: "*", element: <ViewsHarness /> }],
    { initialEntries: [initialEntry] }
  );
  const result = render(
    <PinnedLightTheme>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </PinnedLightTheme>
  );
  return { router, ...result };
}

beforeEach(() => {
  localStorage.clear();
  resetUiStateCache();
  resetFakeUiState();
  vi.clearAllMocks();
  mockedList.mockResolvedValue([]);
  mockedGetMySections.mockResolvedValue([]);
});

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("Saved views favourites", () => {
  it("the star in the views menu favourites a view by table and id", async () => {
    const user = userEvent.setup();
    mockedList.mockResolvedValue([makeView({ id: 7, name: "Active Only" })]);
    renderHarness("/");
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: /^views$/i }));
    const star = await screen.findByRole("button", { name: "Add view Active Only to favourites" });
    await waitFor(() => expect((star as HTMLButtonElement).disabled).toBe(false));
    await user.click(star);

    await waitFor(() =>
      expect(storedFavourites()).toEqual([{ t: "v", table: "inventory.equipment", view: 7 }])
    );
    expect(
      await screen.findByRole("button", { name: "Remove view Active Only from favourites" })
    ).toBeTruthy();
  });
});

describe("Saved views UI (Task 4)", () => {
  it('views menu lists "My views" and "Shared views" with the section name', async () => {
    const user = userEvent.setup();
    mockedList.mockResolvedValue([
      makeView({ id: 1, name: "My Active", scope: "Personal" }),
      makeView({
        id: 2,
        name: "Micro Pending",
        scope: "Section",
        sectionId: 5,
        sectionName: "Microbiology",
        isMine: false,
        canEdit: false,
      }),
    ]);

    renderHarness("/");
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: /^views$/i }));
    const popover = screen.getByRole("region", { name: /saved views/i });
    expect(within(popover).getByText("My views")).toBeTruthy();
    expect(within(popover).getByText("My Active")).toBeTruthy();
    expect(within(popover).getByText("Shared views")).toBeTruthy();
    expect(within(popover).getByText("Micro Pending")).toBeTruthy();
    expect(within(popover).getByText("Microbiology")).toBeTruthy();
  });

  it("choosing a view applies it and puts view=<id> in the URL", async () => {
    const user = userEvent.setup();
    mockedList.mockResolvedValue([
      makeView({
        id: 7,
        name: "Active Only",
        stateJson: JSON.stringify({
          v: 1,
          filters: { status: "Active" },
          search: "HPLC",
          sort: { key: "code", direction: "asc" },
          pageSize: 50,
          columns: ["code", "status"],
          density: "comfortable",
        }),
      }),
    ]);

    renderHarness("/");
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: /^views$/i }));
    await user.click(screen.getByRole("button", { name: "Active Only" }));

    expect(screen.getByTestId("status").textContent).toBe("Active");
    expect(screen.getByTestId("columns").textContent).toBe("code,status");
    expect(screen.getByTestId("view-id").textContent).toBe("7");
    expect(screen.getByTestId("location-search").textContent).toContain("view=7");
  });

  it('changing a filter after applying marks the view as modified and offers "Save changes" (owner) or "Save a copy" (shared)', async () => {
    const user = userEvent.setup();
    const myView = makeView({ id: 1, name: "My View", canEdit: true, isMine: true });
    const sharedView = makeView({
      id: 2,
      name: "Section View",
      scope: "Section",
      sectionId: 5,
      sectionName: "Microbiology",
      isMine: false,
      canEdit: false,
    });
    mockedList.mockResolvedValue([myView, sharedView]);

    renderHarness("/");
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    // Apply owner view, then modify filter
    await user.click(screen.getByRole("button", { name: /^views$/i }));
    await user.click(screen.getByRole("button", { name: "My View" }));
    await user.click(screen.getByRole("button", { name: "Filter Retired" }));

    const trigger = screen.getByRole("button", { name: /views.*modified/i });
    await user.click(trigger);
    expect(screen.getByRole("button", { name: "Save changes" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Save a copy" })).toBeNull();

    // Apply read-only shared view, then modify filter
    await user.click(screen.getByRole("button", { name: /^Section View/ }));
    await user.click(screen.getByRole("button", { name: "Filter Retired" }));

    await user.click(screen.getByRole("button", { name: /views.*modified/i }));
    expect(screen.getByRole("button", { name: "Save a copy" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Save changes" })).toBeNull();
  });

  it("Save view validates name (required, <= 60) and shows the server's duplicate-name message on the field", async () => {
    const user = userEvent.setup();
    mockedList.mockResolvedValue([]);
    mockedCreate.mockRejectedValueOnce(
      makeAxiosFieldError(
        "name",
        "You already have a view named 'Dup' for this table.",
        "Validation failed."
      )
    );

    renderHarness("/");
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: /^views$/i }));
    await user.click(screen.getByRole("button", { name: /save as new view/i }));

    const dialog = screen.getByRole("dialog", { name: /save view/i });
    const nameInput = within(dialog).getByLabelText(/view name/i);

    // Required validation
    await user.click(within(dialog).getByRole("button", { name: /^save$/i }));
    expect(within(dialog).getByText(/view name is required/i)).toBeTruthy();
    expect(mockedCreate).not.toHaveBeenCalled();

    // Max length 60 validation
    await user.type(nameInput, "a".repeat(61));
    await user.click(within(dialog).getByRole("button", { name: /^save$/i }));
    expect(within(dialog).getByText(/60 characters/i)).toBeTruthy();
    expect(mockedCreate).not.toHaveBeenCalled();

    // Server duplicate-name field error
    await user.clear(nameInput);
    await user.type(nameInput, "Dup");
    await user.click(within(dialog).getByRole("button", { name: /^save$/i }));

    await waitFor(() => {
      expect(
        within(dialog).getByText("You already have a view named 'Dup' for this table.")
      ).toBeTruthy();
      expect(nameInput.getAttribute("aria-invalid")).toBe("true");
    });
  });

  it('"Share with section" appears only with TableViews.Share and lists only my sections', async () => {
    const user = userEvent.setup();
    const mySections: LaboratorySection[] = [
      {
        sectionId: 11,
        sectionCode: "MICRO",
        sectionName: "Microbiology",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control",
      },
      {
        sectionId: 12,
        sectionCode: "FP",
        sectionName: "Finished Products",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control",
      },
    ];
    mockedGetMySections.mockResolvedValue(mySections);

    // Without permission: no "Share with section"
    const { unmount } = renderHarness("/", []);
    await waitFor(() => expect(mockedList).toHaveBeenCalled());
    await user.click(screen.getByRole("button", { name: /^views$/i }));
    await user.click(screen.getByRole("button", { name: /save as new view/i }));
    expect(screen.queryByLabelText(/share with section/i)).toBeNull();
    unmount();

    // With TableViews.Share: shows checkbox and lists only my sections
    renderHarness("/", [PERMISSIONS.TABLE_VIEWS_SHARE]);
    await waitFor(() => expect(mockedList).toHaveBeenCalled());
    await user.click(screen.getByRole("button", { name: /^views$/i }));
    await user.click(screen.getByRole("button", { name: /save as new view/i }));

    const shareCheckbox = screen.getByLabelText(/share with section/i);
    await user.click(shareCheckbox);

    await waitFor(() => expect(mockedGetMySections).toHaveBeenCalled());
    const sectionSelect = screen.getByRole("combobox", { name: /^section$/i });
    await user.click(sectionSelect);
    const listbox = screen.getByRole("listbox");
    expect(within(listbox).getByRole("option", { name: "Microbiology" })).toBeTruthy();
    expect(within(listbox).getByRole("option", { name: "Finished Products" })).toBeTruthy();
  });

  it("Set as default / Clear default", async () => {
    const user = userEvent.setup();
    const v1 = makeView({ id: 3, name: "Default Candidate", isDefault: false });
    mockedList
      .mockResolvedValueOnce([v1])
      .mockResolvedValueOnce([{ ...v1, isDefault: true }])
      .mockResolvedValueOnce([{ ...v1, isDefault: false }]);
    mockedSetDefault.mockResolvedValue(undefined);

    renderHarness("/");
    await waitFor(() => expect(mockedList).toHaveBeenCalledTimes(1));

    await user.click(screen.getByRole("button", { name: /^views$/i }));
    await user.click(screen.getByRole("button", { name: /set default candidate as default/i }));

    await waitFor(() => {
      expect(mockedSetDefault).toHaveBeenCalledWith("inventory.equipment", 3);
    });
    await waitFor(() => {
      expect(screen.getByRole("button", { name: /clear default for default candidate/i })).toBeTruthy();
    });

    await user.click(screen.getByRole("button", { name: /clear default for default candidate/i }));
    await waitFor(() => {
      expect(mockedSetDefault).toHaveBeenCalledWith("inventory.equipment", null);
    });
  });

  it("default view applies on open with a clean URL and not when the URL has filters", async () => {
    const defaultView = makeView({
      id: 4,
      name: "My Default",
      isDefault: true,
      stateJson: JSON.stringify({
        v: 1,
        filters: { status: "Active" },
        search: "",
        sort: null,
        pageSize: 25,
        columns: ["code", "status"],
        density: "comfortable",
      }),
    });
    mockedList.mockResolvedValue([defaultView]);

    // Clean URL -> applies default view
    const { unmount } = renderHarness("/");
    await waitFor(() => {
      expect(screen.getByTestId("status").textContent).toBe("Active");
    });
    expect(screen.getByTestId("view-id").textContent).toBe("4");
    unmount();

    // URL with filters -> does NOT apply default view
    renderHarness("/?f.status=Retired");
    await waitFor(() => expect(mockedList).toHaveBeenCalledTimes(2));
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(screen.getByTestId("status").textContent).toBe("Retired");
    expect(screen.getByTestId("view-id").textContent).toBe("");
  });

  it('view naming a removed column applies the rest and shows "Not applied: column \'x\'" and saving again drops unknown parts', async () => {
    const user = userEvent.setup();
    const staleView = makeView({
      id: 9,
      name: "Stale View",
      canEdit: true,
      stateJson: JSON.stringify({
        v: 1,
        filters: { status: "Active" },
        search: "",
        sort: null,
        pageSize: 25,
        columns: ["code", "x", "status"],
        density: "comfortable",
      }),
    });
    mockedList.mockResolvedValue([staleView]);
    mockedUpdate.mockResolvedValue({
      ...staleView,
      stateJson: JSON.stringify({
        v: 1,
        filters: { status: "Active" },
        search: "",
        sort: null,
        pageSize: 25,
        columns: ["code", "status"],
        density: "comfortable",
      }),
    });

    renderHarness("/");
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: /^views$/i }));
    await user.click(screen.getByRole("button", { name: "Stale View" }));

    expect(screen.getByTestId("status").textContent).toBe("Active");
    expect(screen.getByTestId("columns").textContent).toBe("code,status");
    expect(screen.getByRole("status").textContent).toContain("Not applied: column 'x'");

    // Saving that view again drops the unknown column 'x'
    await user.click(screen.getByRole("button", { name: /views: stale view/i }));
    await user.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(mockedUpdate).toHaveBeenCalledTimes(1));
    const [, updatePayload] = mockedUpdate.mock.calls[0];
    const savedState = JSON.parse(updatePayload.stateJson ?? "{}") as { columns: string[] };
    expect(savedState.columns).toEqual(["code", "status"]);
  });

  it("views fail to load -> table works, notice shown", async () => {
    const user = userEvent.setup();
    mockedList.mockRejectedValue(new Error("Network down"));

    renderHarness("/");

    await waitFor(() => {
      expect(screen.getByRole("status").textContent).toMatch(/views.*unavailable/i);
    });

    // Table still works normally
    await user.click(screen.getByRole("button", { name: "Filter Retired" }));
    expect(screen.getByTestId("status").textContent).toBe("Retired");
  });

  it("delete asks for confirmation with Cancel focused", async () => {
    const user = userEvent.setup();
    const v1 = makeView({ id: 5, name: "Old View", canEdit: true });
    mockedList.mockResolvedValueOnce([v1]).mockResolvedValueOnce([]);
    mockedRemove.mockResolvedValue(undefined);

    renderHarness("/");
    await waitFor(() => expect(mockedList).toHaveBeenCalled());

    await user.click(screen.getByRole("button", { name: /^views$/i }));
    await user.click(screen.getByRole("button", { name: /delete old view/i }));

    const confirmDialog = screen.getByRole("alertdialog");
    const cancelBtn = within(confirmDialog).getByRole("button", { name: "Cancel" });
    expect(document.activeElement).toBe(cancelBtn);

    await user.click(within(confirmDialog).getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(mockedRemove).toHaveBeenCalledWith(5));
  });

  it("a link with view=<id> the user cannot see falls back to page defaults with a notice", async () => {
    mockedList.mockResolvedValue([makeView({ id: 1, name: "Visible View" })]);

    renderHarness("/?view=999");

    await waitFor(() => {
      expect(screen.getByRole("status").textContent).toMatch(/view.*not found|cannot access|default/i);
      expect(screen.getByTestId("view-id").textContent).toBe("");
    });
    expect(screen.getByTestId("location-search").textContent).not.toContain("view=999");
  });
});
