import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import { MaterialWizard } from "./MaterialWizard";
import { MaterialService } from "../services/MaterialService";
import { DIALOG_MATERIAL_FIXTURES } from "./__fixtures__/materialPayloads";
import { fillMaterial, micro, mockMaterialLookups, physchem } from "./materialWizard.testUtils";

// Characterisation: the body the Add / Edit Material wizard sends to MaterialService.create and
// .update, driven with user-event. Written and run against the old dialog BEFORE it was moved onto
// materialFormModel, then retargeted at the wizard with the same fixtures to prove the move changed nothing.

// Service-boundary mocks only (CLAUDE.md Rule 4.5).
vi.mock("../services/MaterialService", () => ({
  MaterialService: { getTypeOptions: vi.fn(), getDefaultUnit: vi.fn(), create: vi.fn(), update: vi.fn() }
}));
vi.mock("../../equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: { getAll: vi.fn() }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService", () => ({
  MaterialMasterService: { getAll: vi.fn() }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/MaterialCategoryService", () => ({
  MaterialCategoryService: { getAll: vi.fn() }
}));
vi.mock("../../../laboratoryConfiguration/masterDataSimple/services/ReferenceOrganismService", () => ({
  ReferenceOrganismService: { getAll: vi.fn() }
}));
vi.mock("../../../../services/laboratorySectionService", () => ({ getMySections: vi.fn() }));
vi.mock("../../../../services/masterDataOptions", () => ({ masterDataOptions: { getMediaProducts: vi.fn() } }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const stepper = (user: ReturnType<typeof userEvent.setup>) => ({
  next: async () => {
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: /^Next$/ }));
  }
});

async function reviewAndSave(user: ReturnType<typeof userEvent.setup>) {
  const dialog = screen.getByRole("dialog");
  await user.click(within(dialog).getByRole("button", { name: /^Next$/ }));
  const save = await within(dialog).findByRole("button", { name: /^Save$/ });
  await waitFor(() => expect((save as HTMLButtonElement).disabled).toBe(false));
  await user.click(save);
}

describe("material dialog payload characterisation", () => {
  beforeEach(async () => {
    await resetDraftDb();
    signInForTest(7);
    mockMaterialLookups();
  });

  const createCases = Object.entries(DIALOG_MATERIAL_FIXTURES).filter(([, f]) => !f.ctx.isEditing);
  const editCases = Object.entries(DIALOG_MATERIAL_FIXTURES).filter(([, f]) => f.ctx.isEditing);

  it.each(createCases)("create %s sends the captured body", async (_name, fixture) => {
    if (fixture.ctx.sectionCount > 1) mockMaterialLookups([micro, physchem]);
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <AuthProvider>
        <MemoryRouter>
        <MaterialWizard open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={null} />
        </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );
    await screen.findByRole("dialog");
    await fillMaterial(user, fixture, stepper(user));
    await reviewAndSave(user);

    await waitFor(() => expect(MaterialService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialService.create).mock.calls[0]).toStrictEqual([fixture.payload]);
    expect(MaterialService.update).not.toHaveBeenCalled();
  }, 30000);

  it.each(editCases)("edit %s sends the captured body with the record version", async (_name, fixture) => {
    const user = userEvent.setup();
    render(
      <PinnedLightTheme>
        <AuthProvider>
        <MemoryRouter>
        <MaterialWizard open onClose={vi.fn()} onSuccess={vi.fn()} editingItem={fixture.editing ?? null} />
        </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );
    await screen.findByRole("dialog");
    await waitFor(() => expect(MaterialService.getTypeOptions).toHaveBeenCalledWith(1), { timeout: 4000 });
    await fillMaterial(user, fixture, stepper(user));
    await reviewAndSave(user);

    await waitFor(() => expect(MaterialService.update).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MaterialService.update).mock.calls[0]).toStrictEqual([5, fixture.payload, 4]);
    expect(MaterialService.create).not.toHaveBeenCalled();
  }, 30000);
});
