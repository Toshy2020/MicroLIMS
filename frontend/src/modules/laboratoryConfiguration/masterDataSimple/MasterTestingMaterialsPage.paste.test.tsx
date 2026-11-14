import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { MasterTestingMaterialsPage } from "./MasterTestingMaterialsPage";
import {
  MaterialMasterService,
  type MaterialMasterEntry
} from "./services/MaterialMasterService";
import {
  MaterialCategoryService,
  type MaterialCategory
} from "./services/MaterialCategoryService";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../../services/laboratorySectionService";
import { TestingMaterialCatalogService } from "./services/TestingMaterialCatalogService";

vi.mock("./services/MaterialMasterService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./services/MaterialMasterService")>();
  return {
    ...actual,
    MaterialMasterService: {
      ...actual.MaterialMasterService,
      getAll: vi.fn(),
      createBatch: vi.fn()
    }
  };
});

vi.mock("./services/MaterialCategoryService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./services/MaterialCategoryService")>();
  return {
    ...actual,
    MaterialCategoryService: {
      ...actual.MaterialCategoryService,
      getAll: vi.fn()
    }
  };
});

vi.mock("../../../services/laboratorySectionService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/laboratorySectionService")>();
  const getSections = vi.fn();
  const getMySections = vi.fn();
  return {
    ...actual,
    getSections,
    getMySections,
    laboratorySectionService: {
      ...actual.laboratorySectionService,
      getSections,
      getMySections
    }
  };
});

vi.mock("./services/TestingMaterialCatalogService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./services/TestingMaterialCatalogService")>();
  return {
    ...actual,
    TestingMaterialCatalogService: {
      ...actual.TestingMaterialCatalogService,
      getAll: vi.fn()
    }
  };
});

const FP_SECTION: LaboratorySection = {
  sectionId: 1,
  sectionCode: "FP",
  sectionName: "Finished Product",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const MICRO_SECTION: LaboratorySection = {
  sectionId: 2,
  sectionCode: "MICRO",
  sectionName: "Microbiology",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const EXISTING_ENTRIES: MaterialMasterEntry[] = [
  {
    id: 1,
    version: 1,
    sectionId: 1,
    sectionName: "Finished Product",
    sectionCode: "FP",
    canEdit: true,
    code: "RG-EXIST",
    name: "Existing Reagent",
    category: "Reagent",
    grade: "AR",
    source: "Merck",
    baseUnit: "Gram",
    isActive: true
  }
];

const MATERIAL_CATEGORIES: MaterialCategory[] = [
  {
    id: 10,
    name: "Filters",
    codePrefix: "FLT",
    defaultUnit: "Pack",
    coaRequired: true,
    expiryRequired: true,
    quantityTracked: true,
    approvalBeforeUse: false,
    labelRequired: false,
    isActive: true,
    sectionIds: [],
    hasEntries: false
  },
  {
    id: 11,
    name: "Shared Supplies",
    codePrefix: "SUP1",
    defaultUnit: "Piece",
    coaRequired: false,
    expiryRequired: false,
    quantityTracked: true,
    approvalBeforeUse: false,
    labelRequired: false,
    isActive: true,
    sectionIds: [],
    hasEntries: false
  },
  {
    id: 12,
    name: "Shared Supplies",
    codePrefix: "SUP2",
    defaultUnit: "Pack",
    coaRequired: false,
    expiryRequired: false,
    quantityTracked: true,
    approvalBeforeUse: false,
    labelRequired: false,
    isActive: true,
    sectionIds: [],
    hasEntries: false
  }
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

async function mountPage(initialPath = "/laboratory-configuration/master-testing-materials") {
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={[initialPath]}>
          <MasterTestingMaterialsPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
  await screen.findByText("Existing Reagent");
}

async function pasteRows(user: ReturnType<typeof userEvent.setup>, tsv: string) {
  await user.click(screen.getByRole("button", { name: "Paste entries" }));
  const pasteBox = screen.getByLabelText("Paste material master rows");
  await user.click(pasteBox);
  await user.paste(tsv);
}

const previewTable = () => screen.findByRole("table", { name: /paste preview/i });
const acceptButton = () => screen.getByRole("button", { name: "Accept" }) as HTMLButtonElement;

describe("MasterTestingMaterialsPage paste", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([FP_SECTION, MICRO_SECTION]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([FP_SECTION]);
    vi.mocked(MaterialMasterService.getAll).mockResolvedValue(EXISTING_ENTRIES);
    vi.mocked(MaterialMasterService.createBatch).mockResolvedValue([]);
    vi.mocked(MaterialCategoryService.getAll).mockResolvedValue(MATERIAL_CATEGORIES);
    vi.mocked(TestingMaterialCatalogService.getAll).mockResolvedValue([]);
  });

  afterEach(cleanup);

  it("paste opens preview with the screen's columns", async () => {
    const user = userEvent.setup();
    await mountPage();

    await pasteRows(user, "RG-101\tReagent\tAcetic Acid\tMilliliter\tAR\tMerck\n");

    const table = await previewTable();
    const headers = within(table)
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headers).toEqual([
      "Row",
      "Code",
      "Category",
      "Name",
      "Base Unit",
      "Grade",
      "Source / Pharmacopoeia",
      "Working Concentration",
      "Solvent",
      "Transition Range (From)",
      "Transition Range (To)",
      "Colour Change",
      "Indicator Use",
      "Problem"
    ]);
    expect(within(table).getByText("RG-101")).toBeTruthy();
    expect(within(table).getByText("Acetic Acid")).toBeTruthy();
    expect(acceptButton().disabled).toBe(false);
  });

  it("unknown choice name and ambiguous choice name are preview errors", async () => {
    const user = userEvent.setup();
    await mountPage();

    await pasteRows(
      user,
      "RG-102\tBogusCategory\tTest Material\tBogusUnit\t\t\n"
    );

    const reagentsTable = await previewTable();
    expect(within(reagentsTable).getByText(/unknown category 'BogusCategory'/i)).toBeTruthy();
    expect(within(reagentsTable).getByText(/unknown base unit 'BogusUnit'/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    await user.click(screen.getByRole("tab", { name: "Other Materials" }));
    await pasteRows(
      user,
      [
        "SUP-001\tOther\tShared Supplies\tSyringe 10mL\tPiece\t\t",
        "UNK-001\tOther\tNoSuchCategory\tVial Rack\tPiece\t\t"
      ].join("\n")
    );

    const otherTable = await previewTable();
    expect(within(otherTable).getByText(/ambiguous material category 'Shared Supplies'/i)).toBeTruthy();
    expect(within(otherTable).getByText(/unknown material category 'NoSuchCategory'/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("existing code is flagged and duplicate code inside the paste is flagged on both rows", async () => {
    const user = userEvent.setup();
    await mountPage();

    await pasteRows(
      user,
      [
        " rg-exist \tReagent\tDuplicate Existing\tGram\t\t",
        "RG-DUP\tReagent\tFirst Dup\tGram\t\t",
        "RG-UNIQ\tReagent\tUnique Material\tGram\t\t",
        "rg-dup\tReagent\tSecond Dup\tGram\t\t"
      ].join("\n")
    );

    const table = await previewTable();
    const rows = within(table).getAllByRole("row");
    expect(within(rows[1]).getByText(/already exists/i)).toBeTruthy();
    expect(within(rows[2]).getByText(/duplicate code in paste/i)).toBeTruthy();
    expect(within(rows[3]).getByText("None")).toBeTruthy();
    expect(within(rows[4]).getByText(/duplicate code in paste/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("non-numeric number cell is a preview error and valid decimal is accepted", async () => {
    const user = userEvent.setup();
    await mountPage();

    await pasteRows(
      user,
      "IND-010\tIndicator\tPhenolphthalein\tMilliliter\tAR\tSigma\t1% w/v\tEthanol\tabc\t10.0\tColourless to Pink\tAcid-Base\n"
    );

    const badTable = await previewTable();
    expect(within(badTable).getByText(/Transition Range \(From\):.*number/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    const pasteBox = screen.getByLabelText("Paste material master rows");
    await user.click(pasteBox);
    await user.paste(
      "IND-010\tIndicator\tPhenolphthalein\tMilliliter\tAR\tSigma\t1% w/v\tEthanol\t8.2\t10.0\tColourless to Pink\tAcid-Base\n"
    );

    const goodTable = await previewTable();
    expect(within(goodTable).getByText("8.2")).toBeTruthy();
    expect(within(goodTable).getByText("None")).toBeTruthy();
    expect(acceptButton().disabled).toBe(false);
  });

  it("Accept then Save sends one batch call with the exact payload", async () => {
    const user = userEvent.setup();
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([FP_SECTION, MICRO_SECTION]);
    await mountPage();

    await pasteRows(
      user,
      [
        "rg-201\tReagent\tNitric Acid\tMilliliter\tAR\tMerck\tFP",
        "IND-201\tIndicator\tMethyl Orange\tMilliliter\t\tSigma\tMicrobiology\t0.1% w/v\tWater\t3.1\t4.4\tRed to Yellow\tAcid-Base"
      ].join("\n")
    );

    await previewTable();
    await user.click(acceptButton());

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(MaterialMasterService.createBatch).not.toHaveBeenCalled();

    const pendingTable = screen.getByRole("table", { name: /pending material master entries/i });
    expect(within(pendingTable).getByText("RG-201")).toBeTruthy();
    expect(within(pendingTable).getByText("IND-201")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(MaterialMasterService.createBatch).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialMasterService.createBatch).mock.calls[0][0]).toEqual([
      {
        code: "RG-201",
        name: "Nitric Acid",
        category: "Reagent",
        materialCategoryId: null,
        grade: "AR",
        source: "Merck",
        baseUnit: "Milliliter",
        sectionId: 1,
        workingConcentration: null,
        solvent: null,
        transitionRangeFrom: null,
        transitionRangeTo: null,
        colourChange: null,
        indicatorUse: null
      },
      {
        code: "IND-201",
        name: "Methyl Orange",
        category: "Indicator",
        materialCategoryId: null,
        grade: null,
        source: "Sigma",
        baseUnit: "Milliliter",
        sectionId: 2,
        workingConcentration: "0.1% w/v",
        solvent: "Water",
        transitionRangeFrom: 3.1,
        transitionRangeTo: 4.4,
        colourChange: "Red to Yellow",
        indicatorUse: "Acid-Base"
      }
    ]);
    await waitFor(() =>
      expect(screen.queryByRole("table", { name: /pending material master entries/i })).toBeNull()
    );
  });

  it("server row error appears on that pending row and nothing is cleared", async () => {
    const user = userEvent.setup();
    vi.mocked(MaterialMasterService.createBatch).mockRejectedValueOnce(
      makeBatchError([
        { field: "code", message: 'Reagent codes must start with "RG-".', row: 1 }
      ])
    );
    await mountPage();

    await pasteRows(
      user,
      [
        "RG-301\tReagent\tValid Reagent\tGram\t\t",
        "BAD-302\tReagent\tBad Prefix Reagent\tGram\t\t"
      ].join("\n")
    );
    await previewTable();
    await user.click(acceptButton());

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(screen.getByRole("alert")).toBeTruthy());
    expect(screen.getByRole("alert").textContent).toMatch(/1 problem in 1 row/i);

    const pendingTable = screen.getByRole("table", { name: /pending material master entries/i });
    const rows = within(pendingTable).getAllByRole("row");
    expect(within(rows[1]).getByText("None")).toBeTruthy();
    expect(within(rows[2]).getByText('Reagent codes must start with "RG-".')).toBeTruthy();
  });

  it("Cancel and Discard send nothing", async () => {
    const user = userEvent.setup();
    await mountPage();

    await pasteRows(user, "RG-401\tReagent\tTest Acid\tMilliliter\t\t\n");
    await previewTable();
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("table", { name: /paste preview/i })).toBeNull());
    expect(screen.queryByRole("table", { name: /pending material master entries/i })).toBeNull();
    expect(MaterialMasterService.createBatch).not.toHaveBeenCalled();

    const pasteBox = screen.getByLabelText("Paste material master rows");
    await user.click(pasteBox);
    await user.paste("RG-401\tReagent\tTest Acid\tMilliliter\t\t\n");
    await previewTable();
    await user.click(acceptButton());
    expect(screen.getByRole("table", { name: /pending material master entries/i })).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(screen.queryByRole("table", { name: /pending material master entries/i })).toBeNull();
    expect(MaterialMasterService.createBatch).not.toHaveBeenCalled();
  });
});
