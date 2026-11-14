import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import type { TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { WaterConfigPage } from "./WaterConfigPage";
import type { WaterDept } from "./waterConfigTypes";

const waterServiceMock = vi.hoisted(() => ({
  getWaterDepartments: vi.fn(),
  getSamplingConfigurations: vi.fn(),
  createWaterDepartment: vi.fn(),
  updateWaterDepartment: vi.fn(),
  deleteWaterDepartment: vi.fn(),
  createSamplingPoint: vi.fn(),
  createSamplingPointsBatch: vi.fn(),
  updateSamplingPoint: vi.fn(),
  deleteSamplingPoint: vi.fn(),
  getPointLabFrequency: vi.fn(),
  setPointLabFrequency: vi.fn(),
  deletePointLabFrequency: vi.fn(),
  createSamplingConfiguration: vi.fn(),
  updateSamplingConfiguration: vi.fn(),
  deleteSamplingConfiguration: vi.fn()
}));

const masterDataOptionsMock = vi.hoisted(() => ({
  getTestDefinitions: vi.fn(),
  getEditableTestCodes: vi.fn()
}));

vi.mock("./services/WaterConfigService", () => ({
  WaterConfigService: waterServiceMock
}));

vi.mock("../../../services/masterDataOptions", () => ({
  masterDataOptions: masterDataOptionsMock
}));

const DEPARTMENTS: WaterDept[] = [
  {
    id: 1,
    name: "Purified Water System",
    samplingPoints: [
      {
        id: 10,
        code: "PW-01",
        location: "Existing Tap",
        testingFrequency: "Daily",
        assignedTestCodes: ["TAMC-Water"],
        waterDepartmentId: 1,
        frequencyType: "Daily",
        frequencyInterval: 1
      }
    ]
  },
  {
    id: 2,
    name: "WFI Loop",
    samplingPoints: []
  }
];

const TEST_OPTIONS: TestDefinitionOption[] = [
  {
    id: 1,
    code: "TAMC-Water",
    displayName: "TAMC for Water",
    isActive: true,
    workflowType: "CountTest",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  },
  {
    id: 2,
    code: "PH",
    displayName: "pH",
    isActive: true,
    workflowType: "Measurement",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  },
  {
    id: 3,
    code: "COND-A",
    displayName: "Conductivity",
    isActive: true,
    workflowType: "Measurement",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  },
  {
    id: 4,
    code: "COND-B",
    displayName: "Conductivity",
    isActive: true,
    workflowType: "Measurement",
    section: { id: 2, name: "Physicochemical", code: "FP" }
  }
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
      <WaterConfigPage />
    </MemoryRouter>,
    { bench: false }
  );
  await screen.findByRole("heading", { name: "Purified Water System" });
}

const previewTable = () => screen.findByRole("table", { name: /paste preview/i });
const acceptButton = () => screen.getByRole("button", { name: "Accept" }) as HTMLButtonElement;

beforeEach(() => {
  waterServiceMock.getWaterDepartments.mockResolvedValue(DEPARTMENTS);
  waterServiceMock.getSamplingConfigurations.mockResolvedValue([]);
  waterServiceMock.createSamplingPointsBatch.mockResolvedValue([]);
  masterDataOptionsMock.getTestDefinitions.mockResolvedValue(TEST_OPTIONS);
  masterDataOptionsMock.getEditableTestCodes.mockResolvedValue(["TAMC-Water", "PH"]);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("WaterConfigPage paste", () => {
  it("paste opens preview with the screen's columns", async () => {
    const user = userEvent.setup();
    await renderPage();

    await user.paste("PW-10\tPurified Water System\tLoop A\tDaily\tTAMC-Water\n");

    const table = await previewTable();
    const headers = within(table)
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headers).toEqual(["Row", "Point code", "Water system", "Point name", "Frequency", "Assigned tests", "Problem"]);

    expect(within(table).getByText("PW-10")).toBeTruthy();
    expect(within(table).getByText("Purified Water System")).toBeTruthy();
    expect(within(table).getByText("Loop A")).toBeTruthy();
    expect(within(table).getByText("Daily")).toBeTruthy();
    expect(within(table).getByText("TAMC-Water")).toBeTruthy();
    expect(acceptButton().disabled).toBe(false);
  });

  it("unknown choice name is a preview error", async () => {
    const user = userEvent.setup();
    await renderPage();

    await user.paste("PW-10\tBogus System\tLoop A\tDaily\tBogus Test\n");

    const table = await previewTable();
    expect(within(table).getByText(/unknown water system/i)).toBeTruthy();
    expect(within(table).getByText(/unknown test/i)).toBeTruthy();
    expect(screen.getByRole("alert").textContent).toMatch(/2 problems/i);
    expect(acceptButton().disabled).toBe(true);
  });

  it("existing code flagged", async () => {
    const user = userEvent.setup();
    await renderPage();

    await user.paste("pw-01\tPurified Water System\tLoop A\tDaily\tTAMC-Water\n");

    const table = await previewTable();
    expect(within(table).getByText(/already exists/i)).toBeTruthy();
    expect(screen.getByRole("alert").textContent).toMatch(/1 problem/i);
    expect(acceptButton().disabled).toBe(true);
  });

  it("Accept then Save sends one batch call", async () => {
    const user = userEvent.setup();
    await renderPage();

    await user.paste(
      "PW-10\tPurified Water System\tLoop A\tDaily\tTAMC-Water\nPW-11\tWFI Loop\tLoop B\tWeekly on Monday\tpH\n"
    );
    await previewTable();
    await user.click(acceptButton());

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(waterServiceMock.createSamplingPointsBatch).not.toHaveBeenCalled();

    const pendingTable = screen.getByRole("table", { name: /pending sample locations/i });
    expect(within(pendingTable).getByText("PW-10")).toBeTruthy();
    expect(within(pendingTable).getByText("PW-11")).toBeTruthy();
    expect(within(pendingTable).getByText("PH")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(waterServiceMock.createSamplingPointsBatch).toHaveBeenCalledTimes(1));
    expect(waterServiceMock.createSamplingPointsBatch).toHaveBeenCalledWith([
      {
        code: "PW-10",
        location: "Loop A",
        testingFrequency: "Daily",
        assignedTestCodes: ["TAMC-Water"],
        waterDepartmentId: 1,
        frequency: {
          type: "Daily",
          interval: 1,
          samplingWeekday: null,
          anchorMonth: null,
          weekOfMonth: null
        }
      },
      {
        code: "PW-11",
        location: "Loop B",
        testingFrequency: "Weekly on Monday",
        assignedTestCodes: ["PH"],
        waterDepartmentId: 2,
        frequency: {
          type: "Weekly",
          interval: 1,
          samplingWeekday: "Monday",
          anchorMonth: null,
          weekOfMonth: null
        }
      }
    ]);
  });

  it("server row errors appear in the preview list", async () => {
    const user = userEvent.setup();
    waterServiceMock.createSamplingPointsBatch.mockRejectedValueOnce(
      makeBatchError([
        { field: "code", message: "Code 'PW-10' already exists.", row: 0 },
        { field: "assignedTestCodes", message: "Belongs to Physicochemical section.", row: 1 }
      ])
    );

    await renderPage();

    await user.paste(
      "PW-10\tPurified Water System\tLoop A\tDaily\tTAMC-Water\nPW-11\tWFI Loop\tLoop B\tDaily\tpH\n"
    );
    await previewTable();
    await user.click(acceptButton());

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(screen.getByRole("alert")).toBeTruthy());
    expect(screen.getByRole("alert").textContent).toMatch(/2 problems in 2 rows/i);

    const pendingTable = screen.getByRole("table", { name: /pending sample locations/i });
    const rows = within(pendingTable).getAllByRole("row");
    expect(within(rows[1]).getByText("Code 'PW-10' already exists.")).toBeTruthy();
    expect(within(rows[2]).getByText("Belongs to Physicochemical section.")).toBeTruthy();
  });

  it("ambiguous choice name is a preview error", async () => {
    const user = userEvent.setup();
    await renderPage();

    await user.paste("PW-10\tPurified Water System\tLoop A\tDaily\tConductivity\n");

    const table = await previewTable();
    expect(within(table).getByText(/ambiguous test/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("can paste via the Paste locations button and Cancel creates nothing", async () => {
    const user = userEvent.setup();
    await renderPage();

    await user.click(screen.getByRole("button", { name: "Paste locations" }));
    const pasteBox = screen.getByLabelText("Paste spreadsheet rows");
    await user.click(pasteBox);
    await user.paste("PW-10\tPurified Water System\tLoop A\tDaily\tTAMC-Water\n");

    await previewTable();
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(screen.queryByRole("table", { name: /pending sample locations/i })).toBeNull();
    expect(waterServiceMock.createSamplingPointsBatch).not.toHaveBeenCalled();
  });
});
