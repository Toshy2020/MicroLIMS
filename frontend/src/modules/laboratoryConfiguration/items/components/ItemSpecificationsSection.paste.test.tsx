import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { ItemSpecificationsSection } from "./ItemSpecificationsSection";
import type { TestDefinitionSummary } from "./SpecificationParameterDialog";
import type { Item } from "../services/ItemService";
import {
  SpecificationService,
  type SpecificationDto
} from "../../specifications/services/SpecificationService";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../../../services/laboratorySectionService";
import { masterDataOptions } from "../../../../services/masterDataOptions";

vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../specifications/services/SpecificationService")>();
  return {
    ...actual,
    SpecificationService: {
      ...actual.SpecificationService,
      getForItem: vi.fn(),
      createSpecificationsBatch: vi.fn()
    }
  };
});

vi.mock("../../../../services/laboratorySectionService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/laboratorySectionService")>();
  return {
    ...actual,
    getSections: vi.fn(),
    getMySections: vi.fn(),
    laboratorySectionService: {
      ...actual.laboratorySectionService,
      getSections: vi.fn(),
      getMySections: vi.fn()
    }
  };
});

vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: {
      ...actual.masterDataOptions,
      getTestDefinitions: vi.fn()
    }
  };
});

const fpSection: LaboratorySection = {
  sectionId: 1,
  sectionCode: "FP",
  sectionName: "Finished Product",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const ITEM_ID = 77;

const testItem: Item = {
  id: ITEM_ID,
  version: 1,
  name: "Paracetamol Tablets",
  code: "FP-PARA",
  category: "FinishedProduct",
  sopNumber: "SOP-PARA",
  isActive: true,
  assignedTests: [
    { testCode: "PH", displayName: "pH" },
    { testCode: "LOD", displayName: "Loss on Drying" },
    { testCode: "DESC", displayName: "Appearance" },
    { testCode: "WT", displayName: "Average Weight" },
    { testCode: "MICRO", displayName: "Microbial Count" }
  ],
  specifications: []
};

const testDefinitions: TestDefinitionSummary[] = [
  { id: 1, code: "PH", displayName: "pH", workflowType: "Measurement", sectionId: 1 },
  { id: 2, code: "LOD", displayName: "Loss on Drying", workflowType: "Measurement", sectionId: 1 },
  { id: 3, code: "DESC", displayName: "Appearance", workflowType: "Observation", sectionId: 1 },
  { id: 4, code: "WT", displayName: "Average Weight", workflowType: "Measurement", sectionId: 1 },
  { id: 5, code: "MICRO", displayName: "Microbial Count", workflowType: "CountTest", sectionId: 2 }
];

function makeBatchError(
  fieldErrors: Array<{ field: string; message: string; row?: number | null }>
): AxiosError {
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

function mountSection(options: { readOnly?: boolean; onSpecsChanged?: () => void } = {}) {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <ItemSpecificationsSection
          item={testItem}
          readOnly={options.readOnly}
          onSpecsChanged={options.onSpecsChanged}
        />
      </AuthProvider>
    </PinnedLightTheme>
  );
}

async function pasteSpecs(user: ReturnType<typeof userEvent.setup>, tsv: string) {
  await user.click(await screen.findByRole("button", { name: "Paste specifications" }));
  const pasteBox = screen.getByLabelText("Paste specification rows");
  await user.click(pasteBox);
  await user.paste(tsv);
}

const previewTable = () => screen.findByRole("table", { name: /paste preview/i });
const acceptButton = () => screen.getByRole("button", { name: "Accept" }) as HTMLButtonElement;

describe("ItemSpecificationsSection paste", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([fpSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([fpSection]);
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue(testDefinitions);
    vi.mocked(SpecificationService.getForItem).mockResolvedValue([]);
    vi.mocked(SpecificationService.createSpecificationsBatch).mockResolvedValue([] as SpecificationDto[]);
  });

  afterEach(cleanup);

  it("paste opens preview with the screen's columns", async () => {
    const user = userEvent.setup();
    mountSection();

    await pasteSpecs(user, "PH\tpH\tRange\t5\t7\t\t\t\t\t\t\t\tpH units\n");

    const table = await previewTable();
    const headers = within(table)
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headers).toEqual([
      "Row",
      "Test",
      "Parameter name",
      "Limit type",
      "Lower limit",
      "Upper limit",
      "Target",
      "Tolerance",
      "Tolerance mode",
      "Alert limit",
      "Action limit",
      "Spec limit",
      "Expected result text",
      "Unit",
      "Problem"
    ]);
    expect(within(table).getByText("Range (NLT — NMT)")).toBeTruthy();
    expect(within(table).getByText("pH units")).toBeTruthy();
    expect(acceptButton().disabled).toBe(false);
  });

  it("unknown test name, unknown limit type, and unknown tolerance mode are preview errors", async () => {
    const user = userEvent.setup();
    mountSection();

    // MICRO belongs to section 2 (not caller's lab), so it is also not offered by the form.
    await pasteSpecs(
      user,
      "NoSuchTest\tParam A\tInvalidLimit\t1\t5\t\t\tBogusMode\t\t\t\t\t\nMICRO\tParam B\tRange\t1\t5\t\t\t\t\t\t\t\t\n"
    );

    const table = await previewTable();
    expect(within(table).getByText(/unknown test 'NoSuchTest'/i)).toBeTruthy();
    expect(within(table).getByText(/unknown limit type 'InvalidLimit'/i)).toBeTruthy();
    expect(within(table).getByText(/unknown tolerance mode 'BogusMode'/i)).toBeTruthy();
    expect(within(table).getByText(/unknown test 'MICRO'/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("non-numeric limit is a preview error and '-1.5' is accepted", async () => {
    const user = userEvent.setup();
    mountSection();

    await pasteSpecs(user, "PH\tpH\tRange\tabc\t7\t\t\t\t\t\t\t\t\n");
    const badTable = await previewTable();
    expect(within(badTable).getByText(/Lower limit:.*number/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    const pasteBox = screen.getByLabelText("Paste specification rows");
    await user.click(pasteBox);
    await user.paste("PH\tpH\tRange\t-1.5\t7\t\t\t\t\t\t\t\t\n");
    const goodTable = await previewTable();
    expect(within(goodTable).getByText("-1.5")).toBeTruthy();
    expect(within(goodTable).getByText("None")).toBeTruthy();
    expect(acceptButton().disabled).toBe(false);
  });

  it("Accept then Save sends one batch call with the exact payload (including TargetWithTolerance toleranceMode and omitting lowerInclusive/upperInclusive)", async () => {
    const user = userEvent.setup();
    const onSpecsChanged = vi.fn();
    mountSection({ onSpecsChanged });

    await pasteSpecs(
      user,
      [
        "Loss on Drying\tLOD (105 C)\tNMT (Not More Than)\t\t0.5\t\t\t\t\t\t\t\t%",
        "WT\tAverage weight\tTarget ± Tolerance\t\t\t500\t25\tAbsolute (± value)\t\t\t\t\tmg",
        "DESC\tAppearance\tQualitative\t\t\t\t\t\t\t\t\t  White to off-white tablets  \t",
        "PH\tBioburden check\tCountTiered\t\t\t\t\t\t < 10 \t <= 50 \t NMT 100 \t\tCFU/g"
      ].join("\n")
    );
    await previewTable();
    await user.click(acceptButton());

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(SpecificationService.createSpecificationsBatch).not.toHaveBeenCalled();

    const pendingTable = screen.getByRole("table", { name: /pending specifications/i });
    expect(within(pendingTable).getByText("LOD")).toBeTruthy();
    expect(within(pendingTable).getByText("Absolute (± value)")).toBeTruthy();
    expect(within(pendingTable).getByText("White to off-white tablets")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(SpecificationService.createSpecificationsBatch).toHaveBeenCalledTimes(1)
    );
    const sentPayload = vi.mocked(SpecificationService.createSpecificationsBatch).mock.calls[0][0];
    expect(sentPayload).toEqual([
      {
        itemId: ITEM_ID,
        testCode: "LOD",
        parameterName: "LOD (105 C)",
        limitType: "NotMoreThan",
        lowerLimit: null,
        upperLimit: 0.5,
        target: null,
        tolerance: null,
        toleranceMode: null,
        alertLimit: null,
        actionLimit: null,
        specLimit: null,
        expectedResultText: null,
        unit: "%"
      },
      {
        itemId: ITEM_ID,
        testCode: "WT",
        parameterName: "Average weight",
        limitType: "TargetWithTolerance",
        lowerLimit: null,
        upperLimit: null,
        target: 500,
        tolerance: 25,
        toleranceMode: "Absolute",
        alertLimit: null,
        actionLimit: null,
        specLimit: null,
        expectedResultText: null,
        unit: "mg"
      },
      {
        itemId: ITEM_ID,
        testCode: "DESC",
        parameterName: "Appearance",
        limitType: "Qualitative",
        lowerLimit: null,
        upperLimit: null,
        target: null,
        tolerance: null,
        toleranceMode: null,
        alertLimit: null,
        actionLimit: null,
        specLimit: null,
        expectedResultText: "White to off-white tablets",
        unit: null
      },
      {
        itemId: ITEM_ID,
        testCode: "PH",
        parameterName: "Bioburden check",
        limitType: "CountTiered",
        lowerLimit: null,
        upperLimit: null,
        target: null,
        tolerance: null,
        toleranceMode: null,
        alertLimit: "< 10",
        actionLimit: "<= 50",
        specLimit: "NMT 100",
        expectedResultText: null,
        unit: "CFU/g"
      }
    ]);
    for (const row of sentPayload) {
      expect("lowerInclusive" in row).toBe(false);
      expect("upperInclusive" in row).toBe(false);
    }
    await waitFor(() =>
      expect(screen.queryByRole("table", { name: /pending specifications/i })).toBeNull()
    );
    expect(onSpecsChanged).toHaveBeenCalledTimes(1);
  });

  it("server row error appears on that pending row and nothing is cleared", async () => {
    const user = userEvent.setup();
    vi.mocked(SpecificationService.createSpecificationsBatch).mockRejectedValueOnce(
      makeBatchError([
        { field: "lowerLimit", message: "Lower limit cannot be greater than upper limit.", row: 1 }
      ])
    );

    mountSection();
    await pasteSpecs(
      user,
      "PH\tpH\tRange\t5\t7\t\t\t\t\t\t\t\t\nLOD\tLoss on Drying\tRange\t9\t3\t\t\t\t\t\t\t\t%\n"
    );
    await previewTable();
    await user.click(acceptButton());

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(screen.getByRole("alert")).toBeTruthy());
    expect(screen.getByRole("alert").textContent).toMatch(/1 problem in 1 row/i);

    const pendingTable = screen.getByRole("table", { name: /pending specifications/i });
    const rows = within(pendingTable).getAllByRole("row");
    expect(within(rows[1]).getByText("None")).toBeTruthy();
    expect(within(rows[2]).getByText("Lower limit cannot be greater than upper limit.")).toBeTruthy();
  });

  it("Cancel and Discard send nothing", async () => {
    const user = userEvent.setup();
    mountSection();

    await pasteSpecs(user, "PH\tpH\tRange\t5\t7\t\t\t\t\t\t\t\t\n");
    await previewTable();
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(screen.queryByRole("table", { name: /pending specifications/i })).toBeNull();
    expect(SpecificationService.createSpecificationsBatch).not.toHaveBeenCalled();

    const pasteBox = screen.getByLabelText("Paste specification rows");
    await user.click(pasteBox);
    await user.paste("PH\tpH\tRange\t5\t7\t\t\t\t\t\t\t\t\n");
    await previewTable();
    await user.click(acceptButton());
    expect(screen.getByRole("table", { name: /pending specifications/i })).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(screen.queryByRole("table", { name: /pending specifications/i })).toBeNull();
    expect(SpecificationService.createSpecificationsBatch).not.toHaveBeenCalled();
  });

  it("hides paste controls for read-only viewers and documents range inclusivity and unsupported structured choices in helper text", async () => {
    const user = userEvent.setup();
    const { unmount } = mountSection({ readOnly: true });
    await screen.findByText(/Limits set for/);
    expect(screen.queryByRole("button", { name: "Paste specifications" })).toBeNull();
    unmount();

    mountSection();
    await user.click(await screen.findByRole("button", { name: "Paste specifications" }));
    expect(
      screen.getByText(/A pasted range includes both limits; use Add Specification Parameter for an exclusive limit\./i)
    ).toBeTruthy();
    const helper = screen.getByText(/Use Add Specification Parameter for stages, HPLC analytes\/impurities/i);
    const useAddPart = helper.textContent?.split("Use Add Specification Parameter for")[1] ?? "";
    expect(useAddPart).not.toMatch(/tolerance mode/i);
  });
});
