import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { i18n } from "../../../../i18n";
import { resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../../../services/laboratorySectionService";
import {
  masterDataUsageService,
  type UsageSummary
} from "../../../../services/masterDataUsageService";
import {
  HplcMethodService,
  type HplcMethodResponse
} from "../../masterDataSimple/services/HplcMethodService";
import type { GradedSpecification } from "../../specifications/services/gradeGroups";
import {
  SpecificationService,
  type SpecificationDto
} from "../../specifications/services/SpecificationService";
import type { TestDefinitionSummary } from "./specHelpers";
import type { Item } from "../services/ItemService";
import { SPEC_PAYLOAD_FIXTURES } from "./__fixtures__/specPayloads";
import { SpecificationWizard, type SpecificationWizardProps } from "./SpecificationWizard";

// Service-boundary mocks only (CLAUDE.md Rule 4.5).
vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<
    typeof import("../../specifications/services/SpecificationService")
  >();
  return {
    ...actual,
    SpecificationService: {
      ...actual.SpecificationService,
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn(),
      getForItem: vi.fn(),
      validate: vi.fn()
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

vi.mock("../../masterDataSimple/services/HplcMethodService", async (importOriginal) => {
  const actual = await importOriginal<
    typeof import("../../masterDataSimple/services/HplcMethodService")
  >();
  return {
    ...actual,
    HplcMethodService: {
      ...actual.HplcMethodService,
      getById: vi.fn()
    }
  };
});

vi.mock("../../../../services/masterDataUsageService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataUsageService")>();
  return {
    ...actual,
    masterDataUsageService: {
      ...actual.masterDataUsageService,
      getUsage: vi.fn()
    }
  };
});

const section: LaboratorySection = {
  sectionId: 1,
  sectionCode: "FP",
  sectionName: "Finished Product",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};

const assignedTests = [
  { testCode: "PH", displayName: "pH" },
  { testCode: "TAMC", displayName: "Total Aerobic Microbial Count" },
  { testCode: "DISS", displayName: "Dissolution" },
  { testCode: "HPLC_A", displayName: "HPLC Assay" }
];

const item: Item = {
  id: 7,
  version: 2,
  name: "Finished Tablets 400 mg",
  code: "FP-TAB-400",
  category: "FinishedProduct",
  sopNumber: "SOP-FP-01",
  isActive: true,
  assignedTests,
  specifications: []
};

const workflowTypeByCode: Record<string, string> = {
  PH: "Measurement",
  TAMC: "CountTest",
  DISS: "Dissolution",
  HPLC_A: "HplcMethodAssay"
};

const testDefinitionByCode: Record<string, TestDefinitionSummary> = {
  PH: { id: 1, code: "PH", displayName: "pH", workflowType: "Measurement", sectionId: 1 },
  TAMC: {
    id: 5,
    code: "TAMC",
    displayName: "Total Aerobic Microbial Count",
    workflowType: "CountTest",
    sectionId: 1
  },
  DISS: { id: 9, code: "DISS", displayName: "Dissolution", workflowType: "Dissolution", sectionId: 1 },
  HPLC_A: {
    id: 12,
    code: "HPLC_A",
    displayName: "HPLC Assay",
    workflowType: "HplcMethodAssay",
    equationType: "HplcMethodAssay",
    hplcMethodId: 20,
    sectionId: 1
  }
};

const hplcAssayMethod: HplcMethodResponse = {
  id: 20,
  sectionId: 1,
  name: "Ibuprofen Assay",
  abbreviation: "IBU-A",
  effectiveDate: "2026-10-27T00:00:00Z",
  isActive: true,
  columnDesignation: "L1",
  columnLengthMm: 150,
  columnInternalDiameterMm: 4.6,
  particleSizeUm: 5,
  columnTemperatureC: 30,
  elutionMode: "Isocratic",
  flowRateMlPerMin: 1.0,
  detectorType: "UV",
  injectionVolumeUl: 20,
  runTimeMin: 15,
  diluentSolutionId: 2,
  mobilePhases: [],
  gradientSteps: [],
  technique: "Hplc",
  resultMode: "Assay",
  analytes: [
    {
      id: 601,
      displayOrder: 1,
      name: "Ibuprofen",
      wavelengthNm: 254,
      standardEntryId: 5,
      standardEntryCode: "STD-IBU",
      theoreticalWeightStdMg: 50,
      theoreticalWeightTestMg: 50,
      standardInjections: 5
    }
  ]
};

const noUsage: UsageSummary = {
  type: "Specification",
  id: 42,
  openWorkCount: 0,
  historicalCount: 0,
  lines: []
};

const openWorkUsage: UsageSummary = {
  type: "Specification",
  id: 42,
  openWorkCount: 3,
  historicalCount: 5,
  lines: [
    {
      key: "open-test-orders",
      label: "Open test orders",
      count: 3,
      isOpenWork: true,
      listRoute: "/receiving-testing"
    }
  ]
};

const editingPhSpec: SpecificationDto = {
  id: 42,
  version: 3,
  itemId: 7,
  testCode: "PH",
  parameterName: "pH",
  displayOrder: 2,
  limitType: "Range",
  referenceStandard: "USP <791>",
  unit: "pH units",
  lowerLimit: 5,
  upperLimit: 7,
  lowerInclusive: true,
  upperInclusive: true,
  productionStageRole: "Bulk"
};

function renderWizard(overrides: Partial<SpecificationWizardProps> = {}) {
  const onClose = vi.fn();
  const onSuccess = vi.fn();
  const user = userEvent.setup();
  const props: SpecificationWizardProps = {
    open: true,
    item,
    editingSpec: null,
    preselectedTestCode: null,
    workflowTypeByCode,
    testDefinitionByCode,
    existingSpecs: [],
    onClose,
    onSuccess,
    ...overrides
  };
  const rendered = render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <SpecificationWizard {...props} />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
  return { ...rendered, user, onClose, onSuccess };
}

async function selectOption(
  user: ReturnType<typeof userEvent.setup>,
  label: RegExp | string,
  optionText: RegExp | string
) {
  const trigger = await screen.findByLabelText(label);
  await user.click(trigger);
  const option = await screen.findByRole("option", { name: optionText });
  await user.click(option);
}

describe("SpecificationWizard", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    localStorage.clear();
    await resetDraftDb();
    signInForTest(7);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([section]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([section]);
    vi.mocked(SpecificationService.validate).mockResolvedValue([]);
    vi.mocked(SpecificationService.create).mockResolvedValue({ id: 900, testCode: "PH" });
    vi.mocked(SpecificationService.update).mockResolvedValue({ id: 42, testCode: "PH" });
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue(noUsage);
    vi.mocked(HplcMethodService.getById).mockResolvedValue(hplcAssayMethod);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("omits Method step for tests without HPLC/ICP method and creates Range fixture payload", async () => {
    const { user, onSuccess, onClose } = renderWizard({ preselectedTestCode: "PH" });

    const stepper = await screen.findByRole("list", { name: "Wizard steps" });
    expect(within(stepper).queryByText("Method & Basis")).toBeNull();
    expect(within(stepper).getByText("Test & Parameter")).toBeTruthy();
    expect(within(stepper).getByText("Limits")).toBeTruthy();
    expect(within(stepper).getByText("Unit & Reference")).toBeTruthy();
    expect(within(stepper).getByText("Review")).toBeTruthy();

    await selectOption(user, /Production Stage/i, "Finished Product");
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "5");
    await user.type(screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "7");
    await user.click(screen.getByRole("checkbox", { name: /Upper inclusive/i }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByRole("textbox", { name: "Unit" }), "pH units");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <791>");
    await user.click(screen.getByRole("button", { name: "Next" }));

    const limitsSection = await screen.findByRole("region", { name: "Limits" });
    expect(within(limitsSection).getByText("NLT 5 — NMT 7")).toBeTruthy();

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(
      SPEC_PAYLOAD_FIXTURES.range
    );
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("creates CountTiered fixture payload and shows table wording on Review", async () => {
    const { user } = renderWizard({ preselectedTestCode: "TAMC" });

    await user.click(await screen.findByRole("button", { name: "Next" }));

    await user.type(await screen.findByRole("textbox", { name: "Alert Limit" }), "100");
    await user.type(screen.getByRole("textbox", { name: "Action Limit" }), "500");
    await user.type(screen.getByRole("textbox", { name: "Specification Limit *" }), "1000");
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByRole("textbox", { name: "Unit" }), "CFU/g");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <61>");
    await user.type(screen.getByRole("spinbutton", { name: "Dilution Factor" }), "10");
    await user.click(screen.getByRole("button", { name: "Next" }));

    const limitsSection = await screen.findByRole("region", { name: "Limits" });
    expect(within(limitsSection).getByText("Alert 100 · Action 500 · Spec 1000")).toBeTruthy();

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(
      SPEC_PAYLOAD_FIXTURES.countTiered
    );
  });

  it("shows Method step for HPLC Assay and creates HPLC Assay fixture payload", async () => {
    const { user } = renderWizard({ preselectedTestCode: "HPLC_A" });

    const stepper = await screen.findByRole("list", { name: "Wizard steps" });
    expect(within(stepper).getByText("Method & Basis")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(HplcMethodService.getById).toHaveBeenCalledWith(20));

    // Missing analyte blocks Next with validateStep's message
    await user.click(screen.getByRole("button", { name: "Next" }));
    expect(
      await screen.findByText("Please select an analyte from the HPLC method.")
    ).toBeTruthy();

    await selectOption(user, /Analyte \*/i, /Ibuprofen/i);
    await selectOption(user, /Basis \*/i, "Amount per unit");
    await user.type(await screen.findByRole("spinbutton", { name: "Label Claim *" }), "400");
    await user.type(screen.getByRole("textbox", { name: "Label Claim Unit *" }), "mg");
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "380");
    await user.type(screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "420");
    await user.click(screen.getByRole("button", { name: "Next" }));

    // Unit step already pre-filled with "mg"
    await user.click(await screen.findByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(
      SPEC_PAYLOAD_FIXTURES.hplcAssay
    );
  });

  it("sends no stale fields when switching limit type then back (Review Focus 1)", async () => {
    const { user } = renderWizard({ preselectedTestCode: "PH" });

    await selectOption(user, /Production Stage/i, "Finished Product");
    await user.click(screen.getByRole("button", { name: "Next" }));

    // Fill Range first
    await user.type(await screen.findByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "5");
    await user.type(screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "7");
    await user.click(screen.getByRole("checkbox", { name: /Upper inclusive/i }));

    // Switch to Count-Tiered and enter values for its fields
    await selectOption(user, /Limit Type \*/i, "Count-Tiered (Alert / Action / Spec)");
    await user.type(await screen.findByRole("textbox", { name: "Alert Limit" }), "100");
    await user.type(screen.getByRole("textbox", { name: "Action Limit" }), "500");
    await user.type(screen.getByRole("textbox", { name: "Specification Limit *" }), "1000");

    // Switch back to Range
    await selectOption(user, /Limit Type \*/i, "Range (NLT — NMT)");
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByRole("textbox", { name: "Unit" }), "pH units");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <791>");
    await user.click(screen.getByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(
      SPEC_PAYLOAD_FIXTURES.range
    );
  });

  it("blocks Next with validateStep messages when required fields are missing", async () => {
    // 1) Missing Assigned Test on step 1
    const noTestsItem: Item = { ...item, assignedTests: [] };
    const first = renderWizard({ item: noTestsItem });
    await first.user.click(await screen.findByRole("button", { name: "Next" }));
    expect(await screen.findByText("Assigned Test is required.")).toBeTruthy();
    expect(screen.queryByRole("region", { name: "Limits" })).toBeNull();
    first.unmount();

    // 2) Missing DissolutionQ fields on Limits step
    const second = renderWizard({ preselectedTestCode: "DISS" });
    await second.user.click(await screen.findByRole("button", { name: "Next" }));
    await second.user.click(await screen.findByRole("button", { name: "Next" }));

    expect(
      await screen.findByText(
        "Lower limit Q (%) must be between 0 and 100 (exclusive of 0, inclusive of 100)."
      )
    ).toBeTruthy();
    expect(
      screen.getByText("Label claim must be greater than zero for Dissolution Q specifications.")
    ).toBeTruthy();
    expect(screen.queryByRole("textbox", { name: "Reference Standard" })).toBeNull();
  });

  it("edits an existing specification and sends the fixture update payload without reason when no open work", async () => {
    const { user } = renderWizard({ editingSpec: editingPhSpec });

    await user.click(await screen.findByRole("button", { name: "Next" }));

    const upperInput = await screen.findByRole("spinbutton", { name: "Upper Limit (NMT) *" });
    await user.clear(upperInput);
    await user.type(upperInput, "7.5");
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.click(await screen.findByRole("button", { name: "Next" }));

    const changesSection = await screen.findByRole("region", { name: "Changes" });
    expect(within(changesSection).getByText("NLT 5 — NMT 7")).toBeTruthy();
    expect(within(changesSection).getByText("NLT 5 — NMT 7.5")).toBeTruthy();

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await waitFor(() => expect(SpecificationService.update).toHaveBeenCalledTimes(1));
    expect(SpecificationService.update).toHaveBeenCalledWith(42, SPEC_PAYLOAD_FIXTURES.rangeEdit, 3);
  });

  it("asks for a change reason when editing a specification with open work and passes it to SpecificationService.update", async () => {
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue(openWorkUsage);

    const { user } = renderWizard({ editingSpec: editingPhSpec });

    await user.click(await screen.findByRole("button", { name: "Next" }));
    const upperInput = await screen.findByRole("spinbutton", { name: "Upper Limit (NMT) *" });
    await user.clear(upperInput);
    await user.type(upperInput, "7.5");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    await screen.findByRole("link", { name: "Open test orders" });
    expect(
      screen.getByText(/This change affects 3 open work items\. A reason will be required when you save\./)
    ).toBeTruthy();

    const saveButton = screen.getByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    const reasonInput = await screen.findByRole("textbox", { name: /Reason for change/i });
    await user.type(reasonInput, "Updated USP pH acceptance range");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(SpecificationService.update).toHaveBeenCalledTimes(1));
    expect(SpecificationService.update).toHaveBeenCalledWith(
      42,
      SPEC_PAYLOAD_FIXTURES.rangeEdit,
      3,
      "Updated USP pH acceptance range"
    );
  });

  it("asks for a change reason when removing a grade sibling with no open work and passes it to remove()", async () => {
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue(noUsage);
    vi.mocked(SpecificationService.remove).mockResolvedValue({ outcome: "deleted", id: 51 });

    const rmItem: Item = {
      ...item,
      id: 18,
      category: "RawMaterial",
      assignedTests: [{ testCode: "PH", displayName: "pH" }]
    };
    const bpSpec: SpecificationDto = {
      id: 50,
      version: 2,
      itemId: 18,
      testCode: "PH",
      parameterName: "pH",
      displayOrder: 0,
      limitType: "Range",
      lowerLimit: 5,
      upperLimit: 7,
      lowerInclusive: true,
      upperInclusive: true,
      grade: "BP"
    };
    const uspSpec: SpecificationDto = { ...bpSpec, id: 51, version: 1, grade: "USP" };
    const gradedEditing: GradedSpecification = {
      ...bpSpec,
      gradeSiblings: [bpSpec, uspSpec]
    };

    const { user } = renderWizard({
      item: rmItem,
      editingSpec: gradedEditing,
      existingSpecs: [bpSpec, uspSpec]
    });

    const gradeInput = await screen.findByRole("combobox", { name: "Grade" });
    await user.click(gradeInput);
    await user.keyboard("{Backspace}");

    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    const reasonInput = await screen.findByRole("textbox", { name: /Reason for change/i });
    await user.type(reasonInput, "Drop USP grade per monograph update");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(SpecificationService.remove).toHaveBeenCalledTimes(1));
    expect(SpecificationService.remove).toHaveBeenCalledWith(
      51,
      "Drop USP grade per monograph update"
    );
  });

  it("Save and add next saves the specification and shows the next test preselected in create mode", async () => {
    const twoTestItem: Item = {
      ...item,
      assignedTests: [
        { testCode: "PH", displayName: "pH" },
        { testCode: "TAMC", displayName: "Total Aerobic Microbial Count" }
      ]
    };
    const { user, onSuccess, onClose } = renderWizard({
      item: twoTestItem,
      preselectedTestCode: "PH",
      existingSpecs: []
    });

    await selectOption(user, /Production Stage/i, "Finished Product");
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByRole("spinbutton", { name: "Lower Limit (NLT) *" }), "5");
    await user.type(screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }), "7");
    await user.click(screen.getByRole("checkbox", { name: /Upper inclusive/i }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.type(await screen.findByRole("textbox", { name: "Unit" }), "pH units");
    await user.type(screen.getByRole("textbox", { name: "Reference Standard" }), "USP <791>");
    await user.click(screen.getByRole("button", { name: "Next" }));

    const nextButton = await screen.findByRole("button", {
      name: "Save and add next: Total Aerobic Microbial Count"
    });
    await waitFor(() => expect((nextButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(nextButton);

    await waitFor(() => expect(SpecificationService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(SpecificationService.create).mock.calls[0][0]).toStrictEqual(
      SPEC_PAYLOAD_FIXTURES.range
    );
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(onClose).not.toHaveBeenCalled();

    const paramInput = (await screen.findByRole("textbox", {
      name: "Parameter Name *"
    })) as HTMLInputElement;
    await waitFor(() => expect(paramInput.value).toBe("Total Aerobic Microbial Count"));
    expect((screen.getByDisplayValue("TAMC") as HTMLInputElement).disabled).toBe(true);
  });

  it("renders the wizard title and parameter label in Arabic when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig", "common"]);
    try {
      renderWizard();
      expect(await screen.findByText("إضافة معيار مواصفات")).toBeTruthy();
      expect(screen.getByRole("textbox", { name: "اسم المعيار *" })).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });

  it("after 409 conflict, reloads version 8 and sends version 8 on retry", async () => {
    const conflictErr = new AxiosError("Conflict", "ERR_BAD_REQUEST", undefined, undefined, {
      status: 409,
      statusText: "Conflict",
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { message: "Modified by another user" },
    });
    vi.mocked(SpecificationService.update)
      .mockRejectedValueOnce(conflictErr)
      .mockResolvedValueOnce({ id: 42, testCode: "PH", version: 9 });
    vi.mocked(SpecificationService.getForItem).mockResolvedValue([
      { ...editingPhSpec, version: 8, referenceStandard: "USP <791> rev 2" },
    ]);

    const { user, onSuccess } = renderWizard({ editingSpec: editingPhSpec, existingSpecs: [editingPhSpec] });

    const paramInput = await screen.findByRole("textbox", { name: "Parameter Name *" });
    await user.clear(paramInput);
    await user.type(paramInput, "pH Revised");

    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    const saveButton = await screen.findByRole("button", { name: "Save" });
    await waitFor(() => expect((saveButton as HTMLButtonElement).disabled).toBe(false));
    await user.click(saveButton);

    await screen.findByRole("alert");
    await user.click(screen.getByRole("button", { name: "Review and save again" }));
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(SpecificationService.update).toHaveBeenCalledTimes(2));
    expect(vi.mocked(SpecificationService.update).mock.calls[0][2]).toBe(3);
    expect(vi.mocked(SpecificationService.update).mock.calls[1][2]).toBe(8);
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });
});
