import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { SpecificationParameterDialog, type TestDefinitionSummary } from "./SpecificationParameterDialog";
import type { Item } from "../services/ItemService";
import { SpecificationService, type SpecificationDto } from "../../specifications/services/SpecificationService";
import { laboratorySectionService, type LaboratorySection } from "../../../../services/laboratorySectionService";

// Only the HTTP service layer is mocked; the real dialog, the real
// useMyLabs / useLaboratorySections hooks and the real AuthProvider run
// (CLAUDE.md Rule 4.5).
vi.mock("../../specifications/services/SpecificationService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../specifications/services/SpecificationService")>();
  return {
    ...actual,
    SpecificationService: {
      ...actual.SpecificationService,
      create: vi.fn(),
      update: vi.fn(),
      remove: vi.fn(),
      getForItem: vi.fn()
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
  { testCode: "DENS", displayName: "Density" }
];

const item: Item = {
  id: 7,
  version: 2,
  name: "Oral Solution",
  code: "FP-OS",
  category: "FinishedProduct",
  sopNumber: "SOP-1",
  isActive: true,
  assignedTests,
  specifications: []
};

const workflowTypeByCode: Record<string, string> = { PH: "Measurement", DENS: "Measurement" };

const testDefinitionByCode: Record<string, TestDefinitionSummary> = {
  PH: { id: 1, code: "PH", displayName: "pH", workflowType: "Measurement", sectionId: 1 },
  DENS: { id: 2, code: "DENS", displayName: "Density", workflowType: "Measurement", sectionId: 1 }
};

const phSpec: SpecificationDto = {
  id: 100,
  version: 1,
  itemId: 7,
  testCode: "PH",
  parameterName: "pH",
  displayOrder: 0,
  limitType: "Range",
  lowerLimit: 5,
  upperLimit: 7,
  lowerInclusive: true,
  upperInclusive: true,
  unit: null,
  referenceStandard: null,
  resultBasis: null
};

const noSpecs: SpecificationDto[] = [];

interface DialogProps {
  item: Item;
  existingSpecs: SpecificationDto[];
  editingSpec?: SpecificationDto | null;
  preselectedTestCode?: string | null;
  onClose: () => void;
  onSuccess: () => void;
}

function dialog(props: DialogProps) {
  return (
    <PinnedLightTheme>
      <AuthProvider>
        <SpecificationParameterDialog
          open
          item={props.item}
          editingSpec={props.editingSpec}
          preselectedTestCode={props.preselectedTestCode}
          workflowTypeByCode={workflowTypeByCode}
          testDefinitionByCode={testDefinitionByCode}
          existingSpecs={props.existingSpecs}
          onClose={props.onClose}
          onSuccess={props.onSuccess}
        />
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const lowerInput = () => screen.getByRole("spinbutton", { name: "Lower Limit (NLT) *" }) as HTMLInputElement;
const upperInput = () => screen.getByRole("spinbutton", { name: "Upper Limit (NMT) *" }) as HTMLInputElement;
const nameInput = () => screen.getByRole("textbox", { name: "Parameter Name *" }) as HTMLInputElement;

describe("SpecificationParameterDialog - Save and add next", () => {
  let onClose: () => void;
  let onSuccess: () => void;

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    onClose = vi.fn();
    onSuccess = vi.fn();
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([section]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([section]);
    vi.mocked(SpecificationService.create).mockResolvedValue(phSpec);
  });

  afterEach(cleanup);

  async function renderOnFirstTest() {
    const user = userEvent.setup();
    const view = render(
      dialog({ item, existingSpecs: noSpecs, preselectedTestCode: "PH", onClose, onSuccess })
    );
    // The own-lab list needs the sections to load before the next test is known.
    await screen.findByRole("button", { name: "Save and add next: Density" });
    return { user, view };
  }

  async function saveFirstAndContinue(user: ReturnType<typeof userEvent.setup>) {
    await user.type(lowerInput(), "5");
    await user.type(upperInput(), "7");
    await user.click(screen.getByRole("button", { name: "Save and add next: Density" }));
    await waitFor(() => expect(nameInput().value).toBe("Density"));
  }

  it("saves the parameter, stays open and moves to the next test without limits", async () => {
    const { user } = await renderOnFirstTest();
    expect(nameInput().value).toBe("pH");

    await saveFirstAndContinue(user);

    expect(SpecificationService.create).toHaveBeenCalledTimes(1);
    expect(SpecificationService.create).toHaveBeenCalledWith(
      expect.objectContaining({
        itemId: 7,
        testCode: "PH",
        parameterName: "pH",
        displayOrder: 0,
        limitType: "Range",
        lowerLimit: 5,
        upperLimit: 7,
        lowerInclusive: true,
        upperInclusive: true,
        grade: null
      })
    );
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(onClose).not.toHaveBeenCalled();
    expect(lowerInput().value).toBe("");
    expect(upperInput().value).toBe("");
  });

  it("keeps what the user typed for the next test when the item and specifications reload", async () => {
    const { user, view } = await renderOnFirstTest();
    await saveFirstAndContinue(user);

    await user.type(lowerInput(), "0.9");
    expect(lowerInput().value).toBe("0.9");

    // The parent reloads the item and its specifications after the save: new
    // object identities, same content, and PH now has a specification.
    const reloadedItem: Item = { ...item, assignedTests: assignedTests.map((t) => ({ ...t })) };
    view.rerender(
      dialog({ item: reloadedItem, existingSpecs: [phSpec], preselectedTestCode: "PH", onClose, onSuccess })
    );

    expect(nameInput().value).toBe("Density");
    expect(lowerInput().value).toBe("0.9");
  });

  it("offers no next button on the last test and closes after saving it", async () => {
    const { user, view } = await renderOnFirstTest();
    await saveFirstAndContinue(user);

    const reloadedItem: Item = { ...item, assignedTests: assignedTests.map((t) => ({ ...t })) };
    view.rerender(
      dialog({ item: reloadedItem, existingSpecs: [phSpec], preselectedTestCode: "PH", onClose, onSuccess })
    );

    expect(screen.queryByRole("button", { name: /^Save and add next/ })).toBeNull();

    await user.type(lowerInput(), "0.9");
    await user.type(upperInput(), "1.1");
    vi.mocked(SpecificationService.create).mockClear();
    await user.click(screen.getByRole("button", { name: "Add Parameter" }));

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
    expect(SpecificationService.create).toHaveBeenCalledTimes(1);
    expect(SpecificationService.create).toHaveBeenCalledWith(
      expect.objectContaining({
        itemId: 7,
        testCode: "DENS",
        parameterName: "Density",
        displayOrder: 0,
        limitType: "Range",
        lowerLimit: 0.9,
        upperLimit: 1.1
      })
    );
    // One save for PH (next step) plus one for DENS (this step).
    expect(onSuccess).toHaveBeenCalledTimes(2);
  });

  it("offers no next button when editing", async () => {
    render(
      dialog({ item, existingSpecs: [phSpec], editingSpec: phSpec, onClose, onSuccess })
    );

    expect(await screen.findByRole("button", { name: "Save Changes" })).toBeTruthy();
    // Let the section lookups settle so a late-appearing button would be caught.
    await waitFor(() => expect(laboratorySectionService.getMySections).toHaveBeenCalled());
    expect(screen.queryByRole("button", { name: /^Save and add next/ })).toBeNull();
    expect(nameInput().value).toBe("pH");
  });

  it("shows the server's refusal and stays on the first test with what was typed", async () => {
    vi.mocked(SpecificationService.create).mockRejectedValue({
      response: { data: { message: "Lower limit must be below upper limit." } }
    });
    const { user } = await renderOnFirstTest();

    await user.type(lowerInput(), "9");
    await user.type(upperInput(), "7");
    await user.click(screen.getByRole("button", { name: "Save and add next: Density" }));

    expect(await screen.findByText("Lower limit must be below upper limit.")).toBeTruthy();
    expect(nameInput().value).toBe("pH");
    expect(lowerInput().value).toBe("9");
    expect(upperInput().value).toBe("7");
    expect(onClose).not.toHaveBeenCalled();
    expect(onSuccess).not.toHaveBeenCalled();
  });
});
