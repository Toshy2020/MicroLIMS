import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { TestWorkflowDialog } from "./TestWorkflowDialog";
import { ItemStmService, type ItemStmDto } from "../laboratoryConfiguration/items/services/ItemStmService";

// Only the HTTP service layer and the lookup cache are mocked; the real dialog runs.
const workflow = vi.hoisted(() => ({ getCurrentStep: vi.fn() }));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: workflow }));
vi.mock("../laboratoryConfiguration/items/services/ItemStmService", () => ({ ItemStmService: { getForItem: vi.fn() } }));
vi.mock("../../services/lookupCache", () => ({
  lookupCache: { getReleasedMedia: vi.fn().mockResolvedValue([]), getIncubators: vi.fn().mockResolvedValue([]) }
}));

const getForItem = vi.mocked(ItemStmService.getForItem);

const stm = (over: Partial<ItemStmDto>): ItemStmDto => ({
  status: "Effective",
  sopNumber: "SOP-QC-014",
  documentMasterId: 77,
  companyDocumentCode: "SOP-QC-014",
  title: "Assay",
  documentTypeCode: "SOP",
  revisionNumber: "03",
  effectiveDate: "2026-10-01T00:00:00Z",
  controlledPdfFileId: 5,
  controlledPdfFileName: "a.pdf",
  canDownload: true,
  ...over
});

function renderDialog(itemId?: number | null) {
  return render(
    <PinnedLightTheme>
      <TestWorkflowDialog testOrderId={9} testCode="T1" category="FinishedProduct" displayName="Assay" itemId={itemId} />
    </PinnedLightTheme>
  );
}

let openSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  workflow.getCurrentStep.mockReset();
  workflow.getCurrentStep.mockReturnValue(new Promise(() => undefined));
  getForItem.mockReset();
  openSpy = vi.spyOn(window, "open").mockImplementation(() => null);
});
afterEach(() => {
  cleanup();
  openSpy.mockRestore();
});

describe('TestWorkflowDialog "Open SOP"', () => {
  it("Linked: opens the document in a new tab without opener access", async () => {
    const user = userEvent.setup();
    getForItem.mockResolvedValue(stm({}));
    renderDialog(12);

    await user.click(screen.getByRole("button", { name: "Open SOP" }));

    expect(getForItem).toHaveBeenCalledWith(12);
    await vi.waitFor(() => expect(openSpy).toHaveBeenCalledWith("/document-control/documents/77", "_blank", "noopener,noreferrer"));
  });

  it.each([
    ["no SOP number", stm({ status: "NotLinked", sopNumber: null, documentMasterId: null }), "No SOP number on this item"],
    ["number not in Document Control", stm({ status: "NotFound", sopNumber: "SOP-X-1", documentMasterId: null }), "SOP SOP-X-1 is not in Document Control"],
    ["no effective revision", stm({ status: "NoEffectiveRevision", sopNumber: "SOP-X-2", revisionNumber: null }), "SOP SOP-X-2 has no effective revision"]
  ])("%s: says so and opens nothing", async (_name, response, message) => {
    const user = userEvent.setup();
    getForItem.mockResolvedValue(response);
    renderDialog(12);

    await user.click(screen.getByRole("button", { name: "Open SOP" }));

    expect(await screen.findByText(message)).toBeTruthy();
    expect(openSpy).not.toHaveBeenCalled();
  });

  it("no read access (403): says so and opens nothing", async () => {
    const user = userEvent.setup();
    getForItem.mockRejectedValue({ response: { status: 403 } });
    renderDialog(12);

    await user.click(screen.getByRole("button", { name: "Open SOP" }));

    expect(await screen.findByText("You do not have read access to this SOP")).toBeTruthy();
    expect(openSpy).not.toHaveBeenCalled();
  });

  it("no itemId -> no button, and no request for the SOP", () => {
    renderDialog(null);
    renderDialog(undefined);

    expect(screen.queryByRole("button", { name: "Open SOP" })).toBeNull();
    expect(getForItem).not.toHaveBeenCalled();
  });
});
