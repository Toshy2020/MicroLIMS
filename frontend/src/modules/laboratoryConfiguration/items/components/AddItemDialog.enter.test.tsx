import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { AddItemDialog } from "./AddItemDialog";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { documentControlService } from "../../../documentControl/services/documentControlService";

// Only the HTTP service layer is mocked; the real dialog, form and test picker
// run (CLAUDE.md Rule 4.5).
vi.mock("../../../../services/masterDataOptions", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../../services/masterDataOptions")>();
  return {
    ...actual,
    masterDataOptions: { ...actual.masterDataOptions, getTestDefinitions: vi.fn(), createTestDefinition: vi.fn() }
  };
});

vi.mock("../../../documentControl/services/documentControlService", () => ({
  documentControlService: { getLibrary: vi.fn() }
}));

function renderDialog() {
  const onSave = vi.fn().mockResolvedValue(undefined);
  const onClose = vi.fn();
  render(
    <PinnedLightTheme>
      <AddItemDialog open itemToEdit={null} onClose={onClose} onSave={onSave} />
    </PinnedLightTheme>
  );
  return { onSave, onClose };
}

async function pickAssayTest(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole("combobox", { name: "Assigned Tests" }));
  await user.click(await screen.findByRole("option", { name: "ASSAY: Assay by titration" }));
}

describe("AddItemDialog - saving with Enter", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([
      { id: 1, code: "ASSAY", displayName: "Assay by titration", isActive: true, workflowType: "Titration" }
    ]);
    vi.mocked(documentControlService.getLibrary).mockResolvedValue({
      items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0
    });
  });

  afterEach(cleanup);

  it("Enter in a text field saves the item and closes the dialog", async () => {
    const user = userEvent.setup();
    const { onSave, onClose } = renderDialog();

    await user.type(screen.getByRole("textbox", { name: /Item Code/ }), "FP-001");
    await pickAssayTest(user);
    await user.type(screen.getByRole("textbox", { name: /Item Name/ }), "Paracetamol Tablets{Enter}");

    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    expect(onSave).toHaveBeenCalledWith({
      name: "Paracetamol Tablets",
      code: "FP-001",
      category: "FinishedProduct",
      sopNumber: "",
      testCodes: ["ASSAY"]
    });
    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
  });

  it("Enter with a required field missing shows the reason and saves nothing", async () => {
    const user = userEvent.setup();
    const { onSave, onClose } = renderDialog();

    await user.type(screen.getByRole("textbox", { name: /Item Name/ }), "Paracetamol Tablets{Enter}");

    expect(await screen.findByText("Item Name, Item Code, and at least one assigned test are required.")).toBeTruthy();
    expect(onSave).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it("Enter in the SOP search field or the test picker does not save the item", async () => {
    const user = userEvent.setup();
    const { onSave } = renderDialog();

    await user.type(screen.getByRole("textbox", { name: /Item Name/ }), "Paracetamol Tablets");
    await user.type(screen.getByRole("textbox", { name: /Item Code/ }), "FP-001");
    await pickAssayTest(user);

    await user.type(screen.getByRole("combobox", { name: "SOP Number" }), "SOP-9{Enter}");
    await user.type(screen.getByRole("combobox", { name: "Assigned Tests" }), "{Escape}{Enter}");

    expect(onSave).not.toHaveBeenCalled();

    // The form is complete, so the same key in a plain field does save - with the SOP typed above.
    await user.type(screen.getByRole("textbox", { name: /Item Code/ }), "{Enter}");
    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    expect(onSave.mock.calls[0][0]).toMatchObject({ code: "FP-001", sopNumber: "SOP-9", testCodes: ["ASSAY"] });
  });
});
