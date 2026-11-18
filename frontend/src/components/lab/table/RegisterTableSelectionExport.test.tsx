import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AxiosError } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { Toaster } from "sonner";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { RegisterTable, type RegisterColumn } from "../RegisterTable";
import type { BulkAction } from "./BulkActionBar";
import { exportLogService } from "./services/exportLogService";
import { tableViewService } from "./services/tableViewService";
import { useRowSelection } from "./useRowSelection";
import { useTableState } from "./useTableState";

vi.mock("./services/exportLogService", () => ({
  exportLogService: {
    log: vi.fn(),
  },
}));

vi.mock("./services/tableViewService", () => ({
  tableViewService: {
    list: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
    setDefault: vi.fn(),
  },
}));

const mockedLog = vi.mocked(exportLogService.log);
const mockedViewsList = vi.mocked(tableViewService.list);

interface EquipmentRow {
  code: string;
  instrumentType: string;
  status: string;
}

const COLUMNS: RegisterColumn<EquipmentRow>[] = [
  { key: "code", label: "Code", sortable: true },
  { key: "instrumentType", label: "Type", sortable: true },
  { key: "status", label: "Status" },
];

const ROWS: EquipmentRow[] = [
  { code: "EQ-001", instrumentType: "HPLC", status: "Active" },
  { code: "EQ-002", instrumentType: "Balance", status: "Due" },
  { code: "EQ-003", instrumentType: "Incubator", status: "Active" },
];

function Harness({
  bulkActions,
  totalMatching,
  rows = ROWS,
  withTableKey = true,
}: {
  bulkActions?: BulkAction<string | number>[];
  totalMatching?: number;
  rows?: EquipmentRow[];
  withTableKey?: boolean;
}) {
  const ts = useTableState("inventory.equipment", {
    columns: ["code", "instrumentType", "status"],
  });
  const selection = useRowSelection<string | number>(
    `${JSON.stringify(ts.state.filters)}|${ts.state.search}`
  );
  return (
    <RegisterTable
      tableKey={withTableKey ? "inventory.equipment" : undefined}
      tableState={withTableKey ? ts : undefined}
      columns={COLUMNS}
      rows={rows}
      getRowId={(r) => r.code}
      empty={{ title: "No equipment" }}
      selection={selection}
      bulkActions={bulkActions}
      totalMatching={totalMatching}
    />
  );
}

function renderWithAuth(ui: React.ReactElement, permissions: string[]) {
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <AuthProvider>
          <Toaster />
          {ui}
        </AuthProvider>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

let downloadedBlobs: Blob[] = [];
let createObjectUrlSpy: ReturnType<typeof vi.spyOn>;
let revokeObjectUrlSpy: ReturnType<typeof vi.spyOn>;
let anchorClickSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  localStorage.clear();
  vi.clearAllMocks();
  mockedViewsList.mockResolvedValue([]);
  downloadedBlobs = [];
  createObjectUrlSpy = vi
    .spyOn(URL, "createObjectURL")
    .mockImplementation((obj: Blob | MediaSource) => {
      if (obj instanceof Blob) downloadedBlobs.push(obj);
      return "blob:mock-url";
    });
  revokeObjectUrlSpy = vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
  anchorClickSpy = vi
    .spyOn(HTMLAnchorElement.prototype, "click")
    .mockImplementation(() => {});
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  createObjectUrlSpy.mockRestore();
  revokeObjectUrlSpy.mockRestore();
  anchorClickSpy.mockRestore();
});

describe("RegisterTable selection and audited CSV export", () => {
  it("header checkbox is indeterminate when some rows on the page are selected", async () => {
    const user = userEvent.setup();
    renderWithAuth(<Harness />, [PERMISSIONS.DATA_EXPORT]);

    const selectRow1 = screen.getByRole("checkbox", { name: "Select row EQ-001" });
    await user.click(selectRow1);

    const headerCheckbox = screen.getByRole("checkbox", {
      name: "Select all rows on page",
    }) as HTMLInputElement;
    expect(headerCheckbox.indeterminate).toBe(true);
    expect(headerCheckbox.checked).toBe(false);

    await user.click(screen.getByRole("checkbox", { name: "Select row EQ-002" }));
    await user.click(screen.getByRole("checkbox", { name: "Select row EQ-003" }));
    expect(headerCheckbox.indeterminate).toBe(false);
    expect(headerCheckbox.checked).toBe(true);
  });

  it("checkbox column appears only when the user may use at least one action", () => {
    const { unmount } = renderWithAuth(<Harness />, []);
    expect(screen.queryByRole("checkbox", { name: "Select all rows on page" })).toBeNull();
    unmount();

    renderWithAuth(
      <Harness
        bulkActions={[{ key: "assign", label: "Assign", permission: PERMISSIONS.SAMPLES_ASSIGN_ANALYST, run: vi.fn() }]}
      />,
      []
    );
    expect(screen.queryByRole("checkbox", { name: "Select all rows on page" })).toBeNull();
  });

  it("Export action hidden without Data.Export", async () => {
    const user = userEvent.setup();
    renderWithAuth(
      <Harness
        bulkActions={[{ key: "other", label: "Other action", run: vi.fn() }]}
      />,
      []
    );

    await user.click(screen.getByRole("checkbox", { name: "Select row EQ-001" }));
    expect(screen.getByRole("button", { name: "Other action" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Export/i })).toBeNull();
  });

  it("export calls the log service first; when it rejects, no file is created and the error is shown", async () => {
    const user = userEvent.setup();
    mockedLog.mockRejectedValue(new AxiosError("Export audit failed"));

    renderWithAuth(<Harness />, [PERMISSIONS.DATA_EXPORT]);

    await user.click(screen.getByRole("checkbox", { name: "Select row EQ-001" }));
    await user.click(screen.getByRole("button", { name: "Export CSV" }));

    await waitFor(() => {
      expect(mockedLog).toHaveBeenCalledTimes(1);
    });
    expect(downloadedBlobs).toHaveLength(0);
    expect(anchorClickSpy).not.toHaveBeenCalled();
    expect(await screen.findByText(/Export audit failed/i)).toBeTruthy();
  });

  it("file header lines contain the export id and server time returned", async () => {
    const user = userEvent.setup();
    mockedLog.mockResolvedValue({
      exportId: 987,
      serverTimeUtc: "2031-05-06T07:08:09Z",
      timeZoneId: "Africa/Cairo",
      exportedBy: "Ada Analyst",
    });

    renderWithAuth(<Harness />, [PERMISSIONS.DATA_EXPORT]);

    await user.click(screen.getByRole("checkbox", { name: "Select row EQ-001" }));
    await user.click(screen.getByRole("button", { name: "Export CSV" }));

    await waitFor(() => {
      expect(downloadedBlobs).toHaveLength(1);
    });
    expect(anchorClickSpy).toHaveBeenCalledTimes(1);

    const bytes = new Uint8Array(await downloadedBlobs[0].arrayBuffer());
    expect(bytes[0]).toBe(0xef);
    expect(bytes[1]).toBe(0xbb);
    expect(bytes[2]).toBe(0xbf);
    const text = new TextDecoder("utf-8", { ignoreBOM: true }).decode(bytes);
    expect(text.startsWith("\uFEFF")).toBe(true);
    const lines = text.slice(1).split("\r\n");
    expect(lines[0]).toContain("Exported by,Ada Analyst");
    expect(lines[1]).toContain("Exported at,2031-05-06T07:08:09Z (UTC)");
    expect(lines[2]).toContain("List,Equipment");
    expect(lines[3]).toContain("Filters,");
    expect(lines[4]).toContain("Rows,1");
    expect(lines[5]).toContain("Export ID,987");
    expect(lines[6]).toBe("");
    expect(lines[7]).toBe("Code,Type,Status");
    expect(lines[8]).toBe("EQ-001,HPLC,Active");
  });

  it("more than 5,000 selected -> refused with guidance, service not called", async () => {
    const user = userEvent.setup();
    const bigRows: EquipmentRow[] = Array.from({ length: 26 }, (_, i) => ({
      code: `EQ-${i + 1}`,
      instrumentType: "HPLC",
      status: "Active",
    }));

    renderWithAuth(<Harness rows={bigRows} totalMatching={5001} />, [
      PERMISSIONS.DATA_EXPORT,
    ]);

    await user.click(screen.getByRole("checkbox", { name: "Select all rows on page" }));
    const selectAllBtn = screen.getByRole("button", {
      name: /Select all 5,001 that match/i,
    });
    await user.click(selectAllBtn);

    await user.click(screen.getByRole("button", { name: "Export CSV" }));

    expect(mockedLog).not.toHaveBeenCalled();
    expect(downloadedBlobs).toHaveLength(0);
    expect(
      await screen.findByText(/5,000|narrow your filters|Reports/i)
    ).toBeTruthy();
  });
});
