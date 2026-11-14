import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { EMConfigPage } from "./EMConfigPage";
import type { EmDepartment, RoomTestConfig } from "./emConfigTypes";

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
      { id: 10, name: "Filling Room 1", departmentId: 1, gradeClassification: "B", kind: "Room" },
      { id: 11, name: "Airlock", departmentId: 1, gradeClassification: "C", kind: "Room" }
    ]
  },
  {
    id: 2,
    name: "Packaging",
    class: "Grade D",
    testingFrequency: "Weekly",
    rooms: [
      { id: 21, name: "Airlock", departmentId: 2, gradeClassification: "D", kind: "Room" }
    ]
  }
];

const EXISTING_CONFIGS: Record<number, RoomTestConfig[]> = {
  10: [
    {
      id: 100,
      roomId: 10,
      testType: "PassiveAirSample",
      testCode: "TAMC-EM",
      alertLimit: "5",
      actionLimit: "10",
      specLimit: "20",
      unit: "",
      isActive: true
    }
  ],
  11: [
    {
      id: 101,
      roomId: 11,
      testType: "PassiveAirSample",
      testCode: "TAMC-EM",
      alertLimit: "5",
      actionLimit: "10",
      specLimit: "20",
      unit: "",
      isActive: false
    }
  ]
};

const TEST_OPTIONS: TestDefinitionOption[] = [
  { id: 1, code: "TAMC-EM", displayName: "Total Aerobic Count", isActive: true, workflowType: "CountTest" },
  { id: 2, code: "TYMC-EM", displayName: "Total Yeast & Mold", isActive: true, workflowType: "CountTest" },
  { id: 3, code: "MICRO-A", displayName: "Air Bioburden", isActive: true, workflowType: "CountTest" },
  { id: 4, code: "MICRO-B", displayName: "Air Bioburden", isActive: true, workflowType: "CountTest" }
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

async function pasteConfigs(user: ReturnType<typeof userEvent.setup>, tsv: string) {
  await user.click(screen.getByRole("button", { name: "Paste test configurations" }));
  const pasteBox = screen.getByLabelText("Paste test configuration rows");
  await user.click(pasteBox);
  await user.paste(tsv);
}

const previewTable = () => screen.findByRole("table", { name: /paste preview/i });
const acceptButton = () => screen.getByRole("button", { name: "Accept" }) as HTMLButtonElement;

beforeEach(() => {
  emServiceMock.getDepartments.mockResolvedValue(DEPARTMENTS);
  emServiceMock.getRoomTestConfigurations.mockImplementation((roomId: number) =>
    Promise.resolve(EXISTING_CONFIGS[roomId] ?? [])
  );
  emServiceMock.createRoomTestConfigurationsBatch.mockResolvedValue([]);
  masterDataOptionsMock.getTestDefinitions.mockResolvedValue(TEST_OPTIONS);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("EMConfigPage test configurations paste", () => {
  it("paste opens preview with the screen's columns", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteConfigs(user, "Filling Room 1\tActive air sample\tTAMC-EM\t1\t3\t5\tCFU/m³\n");

    const table = await previewTable();
    const headers = within(table)
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headers).toEqual([
      "Row",
      "Room",
      "Test type",
      "Test",
      "Alert limit",
      "Action limit",
      "Spec limit",
      "Unit",
      "Problem"
    ]);

    expect(within(table).getByText("Filling Room 1")).toBeTruthy();
    expect(within(table).getByText("Active air sample")).toBeTruthy();
    expect(within(table).getByText("TAMC-EM")).toBeTruthy();
    expect(within(table).getByText("CFU/m³")).toBeTruthy();
    expect(acceptButton().disabled).toBe(false);
  });

  it("unknown and ambiguous choice names are preview errors", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteConfigs(
      user,
      "Bogus Room\tSwab\tNoSuchTest\t1\t2\t5\t\nAirlock\tPassive air sample\tAir Bioburden\t1\t2\t5\t\n"
    );

    const table = await previewTable();
    expect(within(table).getByText(/unknown room/i)).toBeTruthy();
    expect(within(table).getByText(/unknown test type/i)).toBeTruthy();
    expect(within(table).getByText(/unknown test/i)).toBeTruthy();
    expect(within(table).getByText(/ambiguous room.*Department \/ Room/i)).toBeTruthy();
    expect(within(table).getByText(/ambiguous test/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("existing active configuration is flagged while inactive is allowed", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteConfigs(
      user,
      "Filling Room 1\tPassive air sample\tTAMC-EM\t5\t10\t20\t\nSterile Filling / Airlock\tPassive air sample\tTAMC-EM\t5\t10\t20\t\n"
    );

    const table = await previewTable();
    const rows = within(table).getAllByRole("row");
    expect(within(rows[1]).getByText(/active configuration.*already exists/i)).toBeTruthy();
    expect(within(rows[2]).getByText("None")).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("Accept then Save sends one batch call with the exact payload and unchanged limits", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteConfigs(
      user,
      "Filling Room 1\tActive air sample\tTotal Yeast & Mold\t< 1\t 10 \t<=20\tCFU/m³\nPackaging / Airlock\tPassiveAirSample\tTAMC-EM\t5\t10\t20\t\n"
    );
    await previewTable();
    await user.click(acceptButton());

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(emServiceMock.createRoomTestConfigurationsBatch).not.toHaveBeenCalled();

    const pendingTable = screen.getByRole("table", { name: /pending test configurations/i });
    expect(within(pendingTable).getByText("TYMC-EM")).toBeTruthy();
    expect(within(pendingTable).getByText("Packaging / Airlock")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(emServiceMock.createRoomTestConfigurationsBatch).toHaveBeenCalledTimes(1));
    expect(emServiceMock.createRoomTestConfigurationsBatch).toHaveBeenCalledWith([
      {
        roomId: 10,
        testType: "ActiveAirSample",
        testCode: "TYMC-EM",
        alertLimit: "< 1",
        actionLimit: " 10 ",
        specLimit: "<=20",
        unit: "CFU/m³"
      },
      {
        roomId: 21,
        testType: "PassiveAirSample",
        testCode: "TAMC-EM",
        alertLimit: "5",
        actionLimit: "10",
        specLimit: "20",
        unit: null
      }
    ]);
  });

  it("server row errors appear in the pending list", async () => {
    const user = userEvent.setup();
    emServiceMock.createRoomTestConfigurationsBatch.mockRejectedValueOnce(
      makeBatchError([
        { field: "testCode", message: "Test configuration 'TYMC-EM' already exists.", row: 0 },
        { field: "unit", message: "Unit is required for ActiveAirSample test configurations.", row: 1 }
      ])
    );

    await renderPage();
    await pasteConfigs(
      user,
      "Filling Room 1\tPassive air sample\tTYMC-EM\t5\t10\t20\t\nPackaging / Airlock\tActive air sample\tTAMC-EM\t1\t3\t5\t\n"
    );
    await previewTable();
    await user.click(acceptButton());

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(screen.getByRole("alert")).toBeTruthy());
    expect(screen.getByRole("alert").textContent).toMatch(/2 problems in 2 rows/i);

    const pendingTable = screen.getByRole("table", { name: /pending test configurations/i });
    const rows = within(pendingTable).getAllByRole("row");
    expect(within(rows[1]).getByText("Test configuration 'TYMC-EM' already exists.")).toBeTruthy();
    expect(within(rows[2]).getByText("Unit is required for ActiveAirSample test configurations.")).toBeTruthy();
  });

  it("Cancel and Discard create nothing", async () => {
    const user = userEvent.setup();
    await renderPage();

    await pasteConfigs(user, "Filling Room 1\tActive air sample\tTAMC-EM\t1\t3\t5\tCFU/m³\n");
    await previewTable();
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(screen.queryByRole("table", { name: /pending test configurations/i })).toBeNull();
    expect(emServiceMock.createRoomTestConfigurationsBatch).not.toHaveBeenCalled();

    const pasteBox = screen.getByLabelText("Paste test configuration rows");
    await user.click(pasteBox);
    await user.paste("Filling Room 1\tActive air sample\tTAMC-EM\t1\t3\t5\tCFU/m³\n");
    await previewTable();
    await user.click(acceptButton());
    expect(screen.getByRole("table", { name: /pending test configurations/i })).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(screen.queryByRole("table", { name: /pending test configurations/i })).toBeNull();
    expect(emServiceMock.createRoomTestConfigurationsBatch).not.toHaveBeenCalled();
  });
});
