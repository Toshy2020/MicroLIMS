import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { resetDraftDb, rawStoredDrafts } from "../../../services/drafts/draftTestUtils";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { entryDefaultsService } from "../../../services/entryDefaultsService";
import { LAST_CHOICE_CAPTION } from "../../../components/entry/LastChoiceField";
import { ReceiveService } from "../services/ReceiveService";
import type { SampleRecord } from "../types/receivingTypes";
import { NewSampleDialog } from "./NewSampleDialog";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
    receiveBatch: vi.fn(),
    receiveItemBased: vi.fn(),
    receiveWater: vi.fn(),
    receiveEM: vi.fn(),
    receiveAfterCleaning: vi.fn(),
    receiptLabs: vi.fn(),
    itemGrades: vi.fn(),
    uploadSampleDocument: vi.fn()
  }
}));

vi.mock("../../../services/entryDefaultsService", () => ({
  entryDefaultsService: { get: vi.fn() }
}));

vi.mock("../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getCausesOfTesting: vi.fn(),
    getSamplers: vi.fn(),
    getProductionStages: vi.fn(),
    getWaterSamplingPoints: vi.fn(),
    getWaterDepartments: vi.fn(),
    getDepartments: vi.fn(),
    getMachines: vi.fn(),
    getItems: vi.fn()
  }
}));

function sampleRecord(over: Partial<SampleRecord>): SampleRecord {
  return {
    id: 101,
    sampleId: 101,
    referenceNumber: "FP-2611-001",
    category: "FinishedProduct",
    displayName: "Paracetamol 500mg",
    departmentId: null,
    machineId: null,
    waterDepartmentId: null,
    productionStage: "Bulk",
    causeOfTesting: "Routine",
    batchNumber: "B-001",
    controlNumber: "C-001",
    status: "Received",
    preparationStatus: "NotRequired",
    receivedAt: "2026-11-14T08:00:00Z",
    sampledBy: "Analyst Alice",
    assignedTests: [],
    ...over
  };
}

function renderDialog(
  props: Partial<React.ComponentProps<typeof NewSampleDialog>> = {}
) {
  const onClose = props.onClose ?? vi.fn();
  const onSuccess = props.onSuccess ?? vi.fn();
  return {
    onClose,
    onSuccess,
    ...render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <NewSampleDialog
              open={true}
              onClose={onClose}
              onSuccess={onSuccess}
              allowedCategories={["product", "rm", "pm", "water", "em", "ac"]}
              labMode={{ kind: "fixed", sectionId: 2, code: "FP" }}
              {...props}
            />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    )
  };
}

describe("NewSampleDialog — characterisation per category", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem("microlims_token", "h.e30=.s");
    localStorage.setItem("microlims_user_id", "7");

    vi.mocked(entryDefaultsService.get).mockResolvedValue({ defaults: [] });
    vi.mocked(masterDataOptions.getCausesOfTesting).mockResolvedValue([
      { id: 1, name: "Routine" }
    ] as never);
    vi.mocked(masterDataOptions.getSamplers).mockResolvedValue([
      { id: 1, name: "Analyst Alice" }
    ] as never);
    vi.mocked(masterDataOptions.getProductionStages).mockResolvedValue([
      { id: 1, name: "Bulk", isActive: true, role: "Bulk" }
    ] as never);
    vi.mocked(masterDataOptions.getWaterSamplingPoints).mockResolvedValue([] as never);
    vi.mocked(masterDataOptions.getWaterDepartments).mockResolvedValue([
      { id: 30, name: "Purified Water Loop" }
    ] as never);
    vi.mocked(masterDataOptions.getDepartments).mockResolvedValue([
      { id: 40, name: "Cleanroom Grade A" }
    ] as never);
    vi.mocked(masterDataOptions.getMachines).mockResolvedValue([
      { id: 50, name: "Blender 01" }
    ] as never);
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([
      { id: 10, name: "Paracetamol 500mg", category: "FinishedProduct" }
    ] as never);

    vi.mocked(ReceiveService.receiptLabs).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology Laboratory", testCount: 1 },
      { sectionId: 2, sectionCode: "FP", sectionName: "Physicochemical Laboratory", testCount: 2 }
    ]);
    vi.mocked(ReceiveService.itemGrades).mockResolvedValue([]);
    vi.mocked(ReceiveService.receiveBatch).mockResolvedValue({
      samples: [{ row: 0, sampleId: 101, referenceNumber: "FP-2611-001" }]
    });
    vi.mocked(ReceiveService.receiveItemBased).mockResolvedValue(sampleRecord({}));
    vi.mocked(ReceiveService.receiveWater).mockResolvedValue(
      sampleRecord({ id: 201, sampleId: 201, referenceNumber: "WT-2611-001", category: "Water" })
    );
    vi.mocked(ReceiveService.receiveEM).mockResolvedValue(
      sampleRecord({ id: 301, sampleId: 301, referenceNumber: "EM-2611-001", category: "EnvironmentalMonitoring" })
    );
    vi.mocked(ReceiveService.receiveAfterCleaning).mockResolvedValue(
      sampleRecord({ id: 401, sampleId: 401, referenceNumber: "AC-2611-001", category: "AfterCleaning" })
    );
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("Product row payload matches single-request body", async () => {
    const user = userEvent.setup();
    renderDialog({
      allowedCategories: ["product"],
      labMode: { kind: "choose" }
    });

    await user.click(await screen.findByText("Product"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 item"));
    await user.click(await screen.findByRole("option", { name: "Paracetamol 500mg" }));

    await user.click(screen.getByLabelText("Sample 1 production stage"));
    await user.click(await screen.findByRole("option", { name: "Bulk" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByLabelText("Sample 1 quantity"), "50g");
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 batch number"), "BATCH-P1");
    await user.type(screen.getByLabelText("Sample 1 control number"), "CTRL-P1");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "Product",
        items: [
          {
            itemId: 10,
            causeOfTestingId: 1,
            sampleQuantity: "50g",
            sampledBy: "Analyst Alice",
            batchNumber: "BATCH-P1",
            controlNumber: "CTRL-P1",
            mfgDate: null,
            expDate: null,
            productionStage: "Bulk",
            targetSectionIds: [1, 2],
            grade: null
          }
        ]
      });
    });
  });

  it("Product row with a half-typed Mfg Date is not saved (Save clicked while focus is in the date field)", async () => {
    const user = userEvent.setup();
    renderDialog({
      allowedCategories: ["product"],
      labMode: { kind: "choose" }
    });

    await user.click(await screen.findByText("Product"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 item"));
    await user.click(await screen.findByRole("option", { name: "Paracetamol 500mg" }));

    await user.click(screen.getByLabelText("Sample 1 production stage"));
    await user.click(await screen.findByRole("option", { name: "Bulk" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByLabelText("Sample 1 quantity"), "50g");
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 batch number"), "BATCH-P1");
    await user.type(screen.getByLabelText("Sample 1 control number"), "CTRL-P1");

    const mfg = screen.getByRole("group", { name: /Sample 1 manufacturing date/i });
    await user.click(mfg.querySelector('[role="spinbutton"][aria-label="Month"]') as HTMLElement);
    await user.keyboard("03");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    expect(await screen.findAllByText(/Enter a complete date/i)).not.toHaveLength(0);
    expect(ReceiveService.receiveBatch).not.toHaveBeenCalled();
  });

  it("Raw Material row payload matches single-request body", async () => {
    const user = userEvent.setup();
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([
      { id: 11, name: "Lactose Monohydrate", category: "RawMaterial" }
    ] as never);
    vi.mocked(ReceiveService.itemGrades).mockResolvedValue(["EP", "USP"]);

    renderDialog({
      allowedCategories: ["rm"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" }
    });

    await user.click(await screen.findByText("Raw Material"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 item"));
    await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));

    await user.click(await screen.findByLabelText("Sample 1 grade"));
    await user.click(await screen.findByRole("option", { name: "EP" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByLabelText("Sample 1 quantity"), "100g");
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 batch number"), "BATCH-RM1");
    await user.type(screen.getByLabelText("Sample 1 control number"), "CTRL-RM1");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "Product",
        items: [
          {
            itemId: 11,
            causeOfTestingId: 1,
            sampleQuantity: "100g",
            sampledBy: "Analyst Alice",
            batchNumber: "BATCH-RM1",
            controlNumber: "CTRL-RM1",
            mfgDate: null,
            expDate: null,
            productionStage: null,
            targetSectionIds: [1],
            grade: "EP"
          }
        ]
      });
    });
  });

  it("Packaging Material row payload matches single-request body", async () => {
    const user = userEvent.setup();
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([
      { id: 12, name: "HDPE Bottle 100ml", category: "PackagingMaterial" }
    ] as never);

    renderDialog({
      allowedCategories: ["pm"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" }
    });

    await user.click(await screen.findByText("Packaging Material"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 item"));
    await user.click(await screen.findByRole("option", { name: "HDPE Bottle 100ml" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByLabelText("Sample 1 quantity"), "10 units");
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 batch number"), "BATCH-PM1");
    await user.type(screen.getByLabelText("Sample 1 control number"), "CTRL-PM1");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "Product",
        items: [
          {
            itemId: 12,
            causeOfTestingId: 1,
            sampleQuantity: "10 units",
            sampledBy: "Analyst Alice",
            batchNumber: "BATCH-PM1",
            controlNumber: "CTRL-PM1",
            mfgDate: null,
            expDate: null,
            productionStage: null,
            targetSectionIds: [1],
            grade: null
          }
        ]
      });
    });
  });

  it("Water row payload matches single-request body", async () => {
    const user = userEvent.setup();
    renderDialog({
      allowedCategories: ["water"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" }
    });

    await user.click(await screen.findByText("Water"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 department"));
    await user.click(await screen.findByRole("option", { name: "Purified Water Loop" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByLabelText("Sample 1 quantity"), "100ml");
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 control number"), "CTRL-W1");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "Water",
        water: [
          {
            waterDepartmentId: 30,
            causeOfTestingId: 1,
            sampleQuantity: "100ml",
            sampledBy: "Analyst Alice",
            controlNumber: "CTRL-W1",
            sampledAt: null,
            labSectionId: 1
          }
        ]
      });
    });
  });

  it("Environmental Monitoring row payload matches single-request body", async () => {
    const user = userEvent.setup();
    renderDialog({
      allowedCategories: ["em"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" }
    });

    await user.click(await screen.findByText("Environmental Monitoring"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 department"));
    await user.click(await screen.findByRole("option", { name: "Cleanroom Grade A" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 control number"), "CTRL-EM1");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "EM",
        em: [
          {
            departmentId: 40,
            causeOfTestingId: 1,
            sampledBy: "Analyst Alice",
            controlNumber: "CTRL-EM1",
            sampledAt: null
          }
        ]
      });
    });
  });

  it("After Cleaning row payload matches single-request body", async () => {
    const user = userEvent.setup();
    renderDialog({
      allowedCategories: ["ac"],
      labMode: { kind: "fixed", sectionId: 2, code: "FP" }
    });

    await user.click(await screen.findByText("After Cleaning"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    const prevProd = await screen.findByLabelText("Sample 1 previous product");
    await user.type(prevProd, "Paracetamol");
    await user.click(await screen.findByRole("option", { name: "Paracetamol 500mg" }));

    await user.click(screen.getByLabelText("Sample 1 technique UV"));
    await user.type(screen.getByLabelText("Sample 1 previous batch number"), "BATCH-AC1");

    await user.click(screen.getByLabelText("Sample 1 machine"));
    await user.click(await screen.findByRole("option", { name: "Blender 01" }));

    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));

    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 control number"), "CTRL-AC1");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "AfterCleaning",
        afterCleaning: [
          {
            machineId: 50,
            causeOfTestingId: 1,
            sampledBy: "Analyst Alice",
            controlNumber: "CTRL-AC1",
            previousProductName: "Paracetamol 500mg",
            previousProductBatchNumber: "BATCH-AC1",
            sampledAt: null,
            labSectionId: 2,
            previousProductItemId: 10,
            techniques: ["UV"]
          }
        ]
      });
    });
  });
});


function makeFieldErrorResponse(
  fieldErrors: Array<{ field: string; message: string; row?: number | null }>
) {
  return new AxiosError(
    "Some rows need correction.",
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      status: 400,
      statusText: "Bad Request",
      headers: new AxiosHeaders(),
      config: { headers: new AxiosHeaders() },
      data: {
        success: false,
        message: "Some rows need correction.",
        errors: fieldErrors.map((e) => e.message),
        fieldErrors
      }
    }
  );
}

describe("NewSampleDialog — batch receive, cell errors, CoA retry, draft", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    localStorage.clear();
    localStorage.setItem("microlims_token", "h.e30=.s");
    localStorage.setItem("microlims_user_id", "7");

    vi.mocked(entryDefaultsService.get).mockResolvedValue({ defaults: [] });
    vi.mocked(masterDataOptions.getCausesOfTesting).mockResolvedValue([
      { id: 1, name: "Routine" }
    ] as never);
    vi.mocked(masterDataOptions.getSamplers).mockResolvedValue([
      { id: 1, name: "Analyst Alice" }
    ] as never);
    vi.mocked(masterDataOptions.getProductionStages).mockResolvedValue([
      { id: 1, name: "Bulk", isActive: true, role: "Bulk" }
    ] as never);
    vi.mocked(masterDataOptions.getWaterSamplingPoints).mockResolvedValue([] as never);
    vi.mocked(masterDataOptions.getWaterDepartments).mockResolvedValue([] as never);
    vi.mocked(masterDataOptions.getDepartments).mockResolvedValue([] as never);
    vi.mocked(masterDataOptions.getMachines).mockResolvedValue([
      { id: 50, name: "Blender 01" }
    ] as never);
    vi.mocked(masterDataOptions.getItems).mockResolvedValue([
      { id: 10, name: "Paracetamol 500mg", category: "FinishedProduct" },
      { id: 11, name: "Lactose Monohydrate", category: "RawMaterial" }
    ] as never);
    vi.mocked(ReceiveService.receiptLabs).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology Laboratory", testCount: 1 }
    ]);
    vi.mocked(ReceiveService.itemGrades).mockResolvedValue([]);
    vi.mocked(ReceiveService.uploadSampleDocument).mockResolvedValue({} as never);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("Save sends one receiveBatch call with all rows and lists sample numbers in row order", async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    vi.mocked(ReceiveService.receiveBatch).mockResolvedValue({
      samples: [
        { row: 1, sampleId: 202, referenceNumber: "PM-2611-002" },
        { row: 0, sampleId: 201, referenceNumber: "PM-2611-001" }
      ]
    });

    renderDialog({
      allowedCategories: ["pm"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" },
      onSuccess,
      onClose
    });

    await user.click(await screen.findByText("Packaging Material"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 item"));
    await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));
    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 batch number"), "B-1");
    await user.type(screen.getByLabelText("Sample 1 control number"), "C-1");

    await user.click(screen.getByRole("button", { name: /Add Another Sample/i }));
    await user.click(screen.getByLabelText("Sample 2 item"));
    await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));
    await user.type(screen.getByLabelText("Sample 2 batch number"), "B-2");
    await user.type(screen.getByLabelText("Sample 2 control number"), "C-2");

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(ReceiveService.receiveBatch).toHaveBeenCalledTimes(1);
      expect(ReceiveService.receiveBatch).toHaveBeenCalledWith({
        category: "Product",
        items: [
          expect.objectContaining({ itemId: 11, batchNumber: "B-1", controlNumber: "C-1" }),
          expect.objectContaining({ itemId: 11, batchNumber: "B-2", controlNumber: "C-2" })
        ]
      });
      expect(onSuccess).toHaveBeenCalledWith([
        { id: 201, referenceNumber: "PM-2611-001" },
        { id: 202, referenceNumber: "PM-2611-002" }
      ]);
      expect(onClose).toHaveBeenCalled();
    });
  });

  it("server fieldErrors appear under matching cells linked by aria-describedby, focus moves to first, live region states '3 problems in 2 rows', and rows stay on failure", async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    vi.mocked(ReceiveService.receiveBatch).mockRejectedValue(
      makeFieldErrorResponse([
        { row: 0, field: "batchNumber", message: "Batch number already exists" },
        { row: 0, field: "controlNumber", message: "Control number is invalid" },
        { row: 1, field: "batchNumber", message: "Batch number is required by server" }
      ])
    );

    renderDialog({
      allowedCategories: ["pm"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" },
      onSuccess,
      onClose
    });

    await user.click(await screen.findByText("Packaging Material"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 item"));
    await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));
    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");
    await user.type(screen.getByLabelText("Sample 1 batch number"), "DUP-1");
    await user.type(screen.getByLabelText("Sample 1 control number"), "CTRL-1");

    await user.click(screen.getByRole("button", { name: /Add Another Sample/i }));
    await user.click(screen.getByLabelText("Sample 2 item"));
    await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(screen.getByText("Batch number already exists")).toBeTruthy();
      expect(screen.getByText("Control number is invalid")).toBeTruthy();
      expect(screen.getByText("Batch number is required by server")).toBeTruthy();
    });

    const summary = screen.getByText(/3 problems in 2 rows/i);
    expect(summary.closest("[aria-live]")?.getAttribute("aria-live")).toBe("polite");

    const row1Batch = screen.getByLabelText("Sample 1 batch number");
    const errId = row1Batch.getAttribute("aria-describedby");
    expect(errId).toBeTruthy();
    expect(document.getElementById(errId!)?.textContent).toBe("Batch number already exists");
    expect(document.activeElement).toBe(row1Batch);

    expect(onSuccess).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    expect((screen.getByLabelText("Sample 1 batch number") as HTMLInputElement).value).toBe("DUP-1");
  });

  it("uploads CoA files after batch success; failed upload shows 'Certificate not attached — retry' and retry does not re-register", async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    const onClose = vi.fn();

    vi.mocked(ReceiveService.receiveBatch).mockResolvedValue({
      samples: [{ row: 0, sampleId: 301, referenceNumber: "RM-2611-001" }]
    });
    vi.mocked(ReceiveService.uploadSampleDocument)
      .mockRejectedValueOnce(new Error("Network timeout"))
      .mockResolvedValueOnce({} as never);

    renderDialog({
      allowedCategories: ["rm"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" },
      onSuccess,
      onClose
    });

    await user.click(await screen.findByText("Raw Material"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 item"));
    await user.click(await screen.findByRole("option", { name: "Lactose Monohydrate" }));
    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");

    const file = new File(["pdf-bytes"], "coa.pdf", { type: "application/pdf" });
    const fileInput = screen.getByLabelText("Sample 1 supplier CoA");
    await user.upload(fileInput, file);

    await user.click(screen.getByRole("button", { name: /Save All Samples/i }));

    await waitFor(() => {
      expect(screen.getByText(/Certificate not attached — retry/i)).toBeTruthy();
    });
    expect(ReceiveService.receiveBatch).toHaveBeenCalledTimes(1);
    expect(ReceiveService.uploadSampleDocument).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole("button", { name: /Retry/i }));

    await waitFor(() => {
      expect(ReceiveService.uploadSampleDocument).toHaveBeenCalledTimes(2);
      expect(ReceiveService.receiveBatch).toHaveBeenCalledTimes(1);
      expect(onSuccess).toHaveBeenCalledWith([{ id: 301, referenceNumber: "RM-2611-001" }]);
      expect(onClose).toHaveBeenCalled();
    });
  });

  it("pre-fills sampled by and cause on row 1 from the server's last choice, labelled and editable", async () => {
    const user = userEvent.setup();
    vi.mocked(entryDefaultsService.get).mockResolvedValue({
      defaults: [
        { field: "sampledBy", id: null, text: "Analyst Alice", label: "Analyst Alice", lastUsedAtUtc: "2026-11-13T08:00:00Z" },
        { field: "causeOfTestingId", id: 1, text: null, label: "Routine", lastUsedAtUtc: "2026-11-13T08:00:00Z" }
      ]
    });
    renderDialog({ allowedCategories: ["ac"], labMode: { kind: "fixed", sectionId: 2, code: "FP" } });

    await user.click(await screen.findByText("After Cleaning"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await waitFor(() => {
      expect((screen.getByPlaceholderText("Sampled By") as HTMLInputElement).value).toBe("Analyst Alice");
    });
    expect(screen.getByLabelText("Sample 1 cause of testing").textContent).toContain("Routine");
    expect(screen.getAllByText(LAST_CHOICE_CAPTION)).toHaveLength(2);
    expect(entryDefaultsService.get).toHaveBeenCalledWith(
      expect.objectContaining({ context: "receiving" }),
      expect.anything()
    );

    await user.clear(screen.getByPlaceholderText("Sampled By"));
    await user.type(screen.getByPlaceholderText("Sampled By"), "Bob");
    expect(screen.getAllByText(LAST_CHOICE_CAPTION)).toHaveLength(1);
  });

  it("never overwrites a value the user typed before the server's last choice arrives", async () => {
    const user = userEvent.setup();
    let resolveDefaults: (value: Awaited<ReturnType<typeof entryDefaultsService.get>>) => void = () => {};
    vi.mocked(entryDefaultsService.get).mockReturnValue(
      new Promise((resolve) => { resolveDefaults = resolve; })
    );
    renderDialog({ allowedCategories: ["ac"], labMode: { kind: "fixed", sectionId: 2, code: "FP" } });

    await user.click(await screen.findByText("After Cleaning"));
    await user.click(screen.getByRole("button", { name: /Next/i }));
    await waitFor(() => expect(entryDefaultsService.get).toHaveBeenCalled());
    await user.type(screen.getByPlaceholderText("Sampled By"), "Bob");

    await act(async () => {
      resolveDefaults({
        defaults: [{ field: "sampledBy", id: null, text: "Analyst Alice", label: "Analyst Alice", lastUsedAtUtc: "2026-11-13T08:00:00Z" }]
      });
    });

    expect((screen.getByPlaceholderText("Sampled By") as HTMLInputElement).value).toBe("Bob");
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
  });

  it("leaves the receiving fields empty when the server offers nothing or the request fails", async () => {
    const user = userEvent.setup();
    vi.mocked(entryDefaultsService.get).mockRejectedValue(new Error("down"));
    renderDialog({ allowedCategories: ["ac"], labMode: { kind: "fixed", sectionId: 2, code: "FP" } });

    await user.click(await screen.findByText("After Cleaning"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await waitFor(() => expect(entryDefaultsService.get).toHaveBeenCalled());
    expect((screen.getByPlaceholderText("Sampled By") as HTMLInputElement).value).toBe("");
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
  });

  it("Add row still copies sampledBy, cause and techniques", async () => {
    const user = userEvent.setup();
    renderDialog({
      allowedCategories: ["ac"],
      labMode: { kind: "fixed", sectionId: 2, code: "FP" }
    });

    await user.click(await screen.findByText("After Cleaning"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.click(await screen.findByLabelText("Sample 1 technique UV"));
    await user.click(screen.getByLabelText("Sample 1 cause of testing"));
    await user.click(await screen.findByRole("option", { name: "Routine" }));
    await user.type(screen.getByPlaceholderText("Sampled By"), "Analyst Alice");

    await user.click(screen.getByRole("button", { name: /Add Another Sample/i }));

    expect((screen.getByLabelText("Sample 2 technique UV") as HTMLInputElement).checked).toBe(true);
    expect(screen.getByLabelText("Sample 2 cause of testing").textContent).toContain("Routine");
    expect((screen.getAllByPlaceholderText("Sampled By")[1] as HTMLInputElement).value).toBe("Analyst Alice");
  });

  it("shows 'Draft saved on this computer at HH:mm — not recorded', offers draft on reopen, and does not store or restore files", async () => {
    const user = userEvent.setup();
    const { unmount } = renderDialog({
      allowedCategories: ["rm"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" }
    });

    await user.click(await screen.findByText("Raw Material"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    await user.type(await screen.findByLabelText("Sample 1 batch number"), "DRAFT-B1");
    const file = new File(["pdf"], "secret-coa.pdf", { type: "application/pdf" });
    await user.upload(screen.getByLabelText("Sample 1 supplier CoA"), file);

    await waitFor(
      () => {
        expect(
          screen.getByText(
            (_c, el) =>
              el?.tagName === "P" && /Draft saved on this computer at \d{2}:\d{2} — not recorded/.test(el.textContent ?? "")
          )
        ).toBeTruthy();
      },
      { timeout: 2000 }
    );

    const stored = await rawStoredDrafts();
    expect(JSON.stringify(stored)).not.toContain("coaFile");
    expect(JSON.stringify(stored)).not.toContain("secret-coa.pdf");

    unmount();

    renderDialog({
      allowedCategories: ["rm"],
      labMode: { kind: "fixed", sectionId: 1, code: "MICRO" }
    });

    await user.click(await screen.findByText("Raw Material"));
    await user.click(screen.getByRole("button", { name: /Next/i }));

    const restoreBtn = await screen.findByRole("button", { name: /Restore entry/i });
    await user.click(restoreBtn);

    expect((screen.getByLabelText("Sample 1 batch number") as HTMLInputElement).value).toBe("DRAFT-B1");
    expect(screen.getByText("Attach CoA")).toBeTruthy();
  });
});
