import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { EMConfigPage } from "./EMConfigPage";
import type { EmDepartment } from "./emConfigTypes";

const emServiceMock = vi.hoisted(() => ({
  getDepartments: vi.fn(),
  createDepartment: vi.fn(),
  updateDepartment: vi.fn(),
  deleteDepartment: vi.fn(),
  getRooms: vi.fn(),
  createRoom: vi.fn(),
  createRoomsBatch: vi.fn(),
  updateRoom: vi.fn(),
  deleteRoom: vi.fn(),
  getRoomTestConfigurations: vi.fn(),
  createRoomTestConfiguration: vi.fn(),
  createRoomTestConfigurationsBatch: vi.fn(),
  updateRoomTestConfiguration: vi.fn(),
  deleteRoomTestConfiguration: vi.fn()
}));

const masterDataOptionsMock = vi.hoisted(() => ({
  getTestDefinitions: vi.fn()
}));

vi.mock("./services/EMConfigService", () => ({
  EMConfigService: emServiceMock
}));

vi.mock("../../../services/masterDataOptions", () => ({
  masterDataOptions: masterDataOptionsMock
}));

const DEPARTMENTS: EmDepartment[] = [
  {
    id: 1,
    name: "Sterile Filling",
    class: "Grade B",
    testingFrequency: "Daily",
    rooms: [
      { id: 10, name: "Filling Room 1", departmentId: 1, gradeClassification: "B", kind: "Room" }
    ]
  },
  {
    id: 2,
    name: "Packaging",
    class: "Grade D",
    testingFrequency: "Weekly",
    rooms: []
  }
];

const TEST_OPTIONS: TestDefinitionOption[] = [
  { id: 1, code: "TAMC-EM", displayName: "Total Aerobic Count", isActive: true, workflowType: "CountTest" }
];

function makeBatchError(fieldErrors: Array<{ field: string; message: string; row?: number | null }>): AxiosError {
  return new AxiosError(
    "Request failed with status code 400",
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      data: {
        success: false,
        message: "Some rows need correction.",
        data: null,
        errors: fieldErrors.map((e) => e.message),
        fieldErrors
      },
      status: 400,
      statusText: "Bad Request",
      headers: {},
      config: { headers: new AxiosHeaders() }
    }
  );
}

async function renderPage() {
  renderWithTheme(
    <MemoryRouter>
      <EMConfigPage />
    </MemoryRouter>,
    { bench: false }
  );
  await screen.findByRole("heading", { name: "Sterile Filling" });
}

async function pasteRooms(user: ReturnType<typeof userEvent.setup>, tsv: string) {
  await user.click(screen.getByRole("button", { name: "Paste rooms" }));
  const pasteBox = screen.getByLabelText("Paste room rows");
  await user.click(pasteBox);
  await user.paste(tsv);
}

const previewTable = () => screen.findByRole("table", { name: /paste preview/i });
const acceptButton = () => screen.getByRole("button", { name: "Accept" }) as HTMLButtonElement;

beforeEach(() => {
  emServiceMock.getDepartments.mockResolvedValue(DEPARTMENTS);
  emServiceMock.getRoomTestConfigurations.mockResolvedValue([]);
  emServiceMock.createRoomsBatch.mockResolvedValue([]);
  masterDataOptionsMock.getTestDefinitions.mockResolvedValue(TEST_OPTIONS);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("EMConfigPage rooms paste", () => {
  it("paste opens preview with the screen's columns", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteRooms(user, "Cleanroom 2\tSterile Filling\tB\tRoom\t\tDaily\n");

    const table = await previewTable();
    const headers = within(table)
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headers).toEqual([
      "Row",
      "Room name",
      "Department",
      "Grade",
      "Type",
      "Sits in room",
      "Frequency",
      "Problem"
    ]);

    expect(within(table).getByText("Cleanroom 2")).toBeTruthy();
    expect(within(table).getByText("Sterile Filling")).toBeTruthy();
    expect(within(table).getByText("B")).toBeTruthy();
    expect(within(table).getByText("Room")).toBeTruthy();
    expect(within(table).getByText("Daily")).toBeTruthy();
    expect(acceptButton().disabled).toBe(false);
  });

  it("unknown and ambiguous choice names are preview errors", async () => {
    const user = userEvent.setup();
    emServiceMock.getDepartments.mockResolvedValueOnce([
      ...DEPARTMENTS,
      { id: 3, name: "packaging", class: "Grade D", testingFrequency: "Weekly", rooms: [] }
    ]);
    await renderPage();

    await pasteRooms(
      user,
      "Room X\tBogus Dept\tGrade Z\tRoom\t\t\nRoom Y\tPackaging\tC\tLAF\tMissing Parent\t\n"
    );

    const table = await previewTable();
    expect(within(table).getByText(/unknown department/i)).toBeTruthy();
    expect(within(table).getByText(/unknown grade/i)).toBeTruthy();
    expect(within(table).getByText(/ambiguous department/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("existing room in the same department is flagged", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteRooms(user, "filling room 1\tSterile Filling\tB\tRoom\t\t\n");

    const table = await previewTable();
    expect(within(table).getByText(/already exists in Sterile Filling/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("Accept then Save sends one batch call with the exact payload", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteRooms(
      user,
      "LAF-02\tSterile Filling\tGrade A\tLAF\tFilling Room 1\tWeekly\nPack Room 2\tPackaging\tD\t\t\tMonthly, week 2\n"
    );
    await previewTable();
    await user.click(acceptButton());

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(emServiceMock.createRoomsBatch).not.toHaveBeenCalled();

    const pendingTable = screen.getByRole("table", { name: /pending rooms/i });
    expect(within(pendingTable).getByText("LAF-02")).toBeTruthy();
    expect(within(pendingTable).getByText("Pack Room 2")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(emServiceMock.createRoomsBatch).toHaveBeenCalledTimes(1));
    expect(emServiceMock.createRoomsBatch).toHaveBeenCalledWith([
      {
        name: "LAF-02",
        departmentId: 1,
        gradeClassification: "A",
        kind: "LAF",
        parentRoomId: 10,
        frequency: {
          type: "Weekly",
          interval: 1,
          samplingWeekday: null,
          anchorMonth: null,
          weekOfMonth: null
        }
      },
      {
        name: "Pack Room 2",
        departmentId: 2,
        gradeClassification: "D",
        kind: "Room",
        parentRoomId: null,
        frequency: {
          type: "Monthly",
          interval: 1,
          samplingWeekday: null,
          anchorMonth: null,
          weekOfMonth: 2
        }
      }
    ]);
  });

  it("server row errors appear in the pending list", async () => {
    const user = userEvent.setup();
    emServiceMock.createRoomsBatch.mockRejectedValueOnce(
      makeBatchError([
        { field: "name", message: "Room 'Cleanroom 2' already exists.", row: 0 },
        { field: "parentRoomId", message: "An LAF must sit in a room.", row: 1 }
      ])
    );

    await renderPage();
    await pasteRooms(
      user,
      "Cleanroom 2\tSterile Filling\tB\tRoom\t\t\nLAF-02\tSterile Filling\tA\tLAF\tFilling Room 1\t\n"
    );
    await previewTable();
    await user.click(acceptButton());

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(screen.getByRole("alert")).toBeTruthy());
    expect(screen.getByRole("alert").textContent).toMatch(/2 problems in 2 rows/i);

    const pendingTable = screen.getByRole("table", { name: /pending rooms/i });
    const rows = within(pendingTable).getAllByRole("row");
    expect(within(rows[1]).getByText("Room 'Cleanroom 2' already exists.")).toBeTruthy();
    expect(within(rows[2]).getByText("An LAF must sit in a room.")).toBeTruthy();
  });

  it("Cancel and Discard create nothing", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteRooms(user, "Cleanroom 2\tSterile Filling\tB\tRoom\t\t\n");
    await previewTable();
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(screen.queryByRole("table", { name: /pending rooms/i })).toBeNull();
    expect(emServiceMock.createRoomsBatch).not.toHaveBeenCalled();

    const pasteBox = screen.getByLabelText("Paste room rows");
    await user.click(pasteBox);
    await user.paste("Cleanroom 2\tSterile Filling\tB\tRoom\t\t\n");
    await previewTable();
    await user.click(acceptButton());
    expect(screen.getByRole("table", { name: /pending rooms/i })).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(screen.queryByRole("table", { name: /pending rooms/i })).toBeNull();
    expect(emServiceMock.createRoomsBatch).not.toHaveBeenCalled();
  });
});
