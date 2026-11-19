import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import axios, { AxiosHeaders } from "axios";
import { toast } from "sonner";
import { lightTheme } from "../../theme";
import { labelService, type LabelPrintStatus, type LabelTarget } from "../../services/labelService";
import { PrintLabelsDialog } from "./PrintLabelsDialog";

vi.mock("../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

const sampleTarget: LabelTarget = {
  entityType: "Sample",
  entityId: 101,
  code: "S-2026-001",
  displayName: "Purified Water"
};

const mediaTarget: LabelTarget = {
  entityType: "MediaLot",
  entityId: 202,
  code: "ML-2026-004",
  displayName: "Tryptic Soy Agar"
};

function makeStatus(overrides: Partial<LabelPrintStatus> & Pick<LabelPrintStatus, "entityType" | "entityId">): LabelPrintStatus {
  return {
    canPrint: true,
    blockedReason: null,
    printCount: 0,
    defaultCopies: 1,
    ...overrides
  };
}

function makeAxiosError(status: number, message: string) {
  const headers = new AxiosHeaders();
  return new axios.AxiosError(
    `Request failed with status code ${status}`,
    axios.AxiosError.ERR_BAD_REQUEST,
    undefined,
    undefined,
    {
      status,
      statusText: "Error",
      headers,
      config: { headers },
      data: { success: false, message, data: null, errors: null }
    }
  );
}

function renderDialog(targets: LabelTarget[], onClose = vi.fn()) {
  const result = render(
    <ThemeProvider theme={lightTheme}>
      <MemoryRouter>
        <PrintLabelsDialog open={true} targets={targets} onClose={onClose} />
      </MemoryRouter>
    </ThemeProvider>
  );
  return { onClose, ...result };
}

describe("PrintLabelsDialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(URL, "createObjectURL").mockReturnValue("blob:http://localhost/label-pdf");
    vi.spyOn(URL, "revokeObjectURL").mockImplementation(() => {});
    vi.spyOn(window, "open").mockReturnValue({
      addEventListener: vi.fn()
    } as unknown as Window);
    vi.spyOn(toast, "success").mockImplementation(() => "");
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("prefills number of labels from the default and sends the edited number", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, defaultCopies: 3 })
    ]);
    vi.mocked(labelService.print).mockResolvedValue(new Blob(["%PDF"], { type: "application/pdf" }));

    renderDialog([sampleTarget]);

    const input = await screen.findByLabelText("Number of labels for S-2026-001");
    expect((input as HTMLInputElement).value).toBe("3");

    await user.clear(input);
    await user.type(input, "5");
    await user.click(screen.getByRole("button", { name: /print/i }));

    await waitFor(() => {
      expect(labelService.print).toHaveBeenCalledWith(
        [{ entityType: "Sample", entityId: 101, copies: 5 }],
        undefined
      );
    });
  });

  it('"Set all to" changes every row', async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, defaultCopies: 1 }),
      makeStatus({ entityType: "MediaLot", entityId: 202, defaultCopies: 2 })
    ]);
    vi.mocked(labelService.print).mockResolvedValue(new Blob(["%PDF"], { type: "application/pdf" }));

    renderDialog([sampleTarget, mediaTarget]);

    const setAll = await screen.findByLabelText(/set all to/i);
    await user.type(setAll, "4");

    const sampleInput = screen.getByLabelText("Number of labels for S-2026-001") as HTMLInputElement;
    const mediaInput = screen.getByLabelText("Number of labels for ML-2026-004") as HTMLInputElement;
    expect(sampleInput.value).toBe("4");
    expect(mediaInput.value).toBe("4");

    await user.click(screen.getByRole("button", { name: /print/i }));
    await waitFor(() => {
      expect(labelService.print).toHaveBeenCalledWith(
        [
          { entityType: "Sample", entityId: 101, copies: 4 },
          { entityType: "MediaLot", entityId: 202, copies: 4 }
        ],
        undefined
      );
    });
  });

  it("rejects 0 and 51 with an inline message and does not call print", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, defaultCopies: 1 })
    ]);

    renderDialog([sampleTarget]);

    const input = await screen.findByLabelText("Number of labels for S-2026-001");

    await user.clear(input);
    await user.type(input, "0");
    await user.click(screen.getByRole("button", { name: /print/i }));
    expect(await screen.findByText(/1 and 50/i)).toBeTruthy();
    expect(labelService.print).not.toHaveBeenCalled();

    await user.clear(input);
    await user.type(input, "51");
    await user.click(screen.getByRole("button", { name: /print/i }));
    expect(await screen.findByText(/1 and 50/i)).toBeTruthy();
    expect(labelService.print).not.toHaveBeenCalled();
  });

  it("shows the reprint reason field only when a label was printed before, and requires 10 characters", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, printCount: 0, defaultCopies: 1 })
    ]);

    const { unmount } = renderDialog([sampleTarget]);
    await screen.findByLabelText("Number of labels for S-2026-001");
    expect(screen.queryByLabelText(/reason for reprint/i)).toBeNull();
    unmount();

    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, printCount: 1, defaultCopies: 1 })
    ]);
    vi.mocked(labelService.print).mockResolvedValue(new Blob(["%PDF"], { type: "application/pdf" }));

    renderDialog([sampleTarget]);

    expect(await screen.findByText("Reprint labels")).toBeTruthy();
    const reasonField = screen.getByLabelText(/reason for reprint/i);
    expect(screen.getByText(/at least 10 characters/i)).toBeTruthy();

    await user.type(reasonField, "Too short");
    await user.click(screen.getByRole("button", { name: /reprint/i }));
    expect(await screen.findByText(/at least 10 characters/i)).toBeTruthy();
    expect(labelService.print).not.toHaveBeenCalled();

    await user.clear(reasonField);
    await user.type(reasonField, "Original label smudged during handling");
    await user.click(screen.getByRole("button", { name: /reprint/i }));

    await waitFor(() => {
      expect(labelService.print).toHaveBeenCalledWith(
        [{ entityType: "Sample", entityId: 101, copies: 1 }],
        "Original label smudged during handling"
      );
    });
  });

  it("shows the server error message at the top of the dialog when print fails", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, defaultCopies: 1 })
    ]);
    vi.mocked(labelService.print).mockRejectedValue(
      makeAxiosError(422, "Label content exceeds label height (25 mm).")
    );

    renderDialog([sampleTarget]);

    await screen.findByLabelText("Number of labels for S-2026-001");
    await user.click(screen.getByRole("button", { name: /print/i }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Label content exceeds label height (25 mm).");
  });

  it("shows the blocked reason for a record that cannot be printed and excludes it from the request", async () => {
    const user = userEvent.setup();
    const uncodedEquipment: LabelTarget = {
      entityType: "EquipmentAsset",
      entityId: 303,
      code: "",
      displayName: "Incubator 3"
    };
    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, defaultCopies: 2 }),
      makeStatus({
        entityType: "EquipmentAsset",
        entityId: 303,
        canPrint: false,
        blockedReason: "No asset code has been assigned yet.",
        defaultCopies: 1
      })
    ]);
    vi.mocked(labelService.print).mockResolvedValue(new Blob(["%PDF"], { type: "application/pdf" }));

    renderDialog([sampleTarget, uncodedEquipment]);

    expect(await screen.findByText("No asset code has been assigned yet.")).toBeTruthy();
    expect(screen.getByLabelText("Number of labels for S-2026-001")).toBeTruthy();
    expect(screen.queryByLabelText("Number of labels for ")).toBeNull();
    expect(screen.queryByLabelText(/Number of labels for Incubator/i)).toBeNull();

    await user.click(screen.getByRole("button", { name: /print/i }));
    await waitFor(() => {
      expect(labelService.print).toHaveBeenCalledWith(
        [{ entityType: "Sample", entityId: 101, copies: 2 }],
        undefined
      );
    });
  });

  it("shows the load error alert when status fails", async () => {
    vi.mocked(labelService.getStatus).mockRejectedValue(
      makeAxiosError(500, "Could not load label status.")
    );

    renderDialog([sampleTarget]);

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Could not load label status.");
  });

  it("opens the PDF and closes on success", async () => {
    const user = userEvent.setup();
    let loadHandler: (() => void) | undefined;
    const fakePopup = {
      addEventListener: vi.fn((event: string, cb: () => void) => {
        if (event === "load") loadHandler = cb;
      })
    };
    vi.mocked(window.open).mockReturnValue(fakePopup as unknown as Window);
    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, defaultCopies: 1 })
    ]);
    vi.mocked(labelService.print).mockResolvedValue(new Blob(["%PDF"], { type: "application/pdf" }));

    const { onClose } = renderDialog([sampleTarget]);

    await screen.findByLabelText("Number of labels for S-2026-001");
    await user.click(screen.getByRole("button", { name: /print/i }));

    await waitFor(() => {
      expect(window.open).toHaveBeenCalledWith("blob:http://localhost/label-pdf");
      expect(toast.success).toHaveBeenCalledWith("Labels ready to print");
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    loadHandler?.();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:http://localhost/label-pdf");
  });

  it("shows an Open label PDF link when the pop-up is blocked", async () => {
    const user = userEvent.setup();
    vi.mocked(window.open).mockReturnValue(null);
    vi.mocked(labelService.getStatus).mockResolvedValue([
      makeStatus({ entityType: "Sample", entityId: 101, defaultCopies: 1 })
    ]);
    vi.mocked(labelService.print).mockResolvedValue(new Blob(["%PDF"], { type: "application/pdf" }));

    const { onClose } = renderDialog([sampleTarget]);

    await screen.findByLabelText("Number of labels for S-2026-001");
    await user.click(screen.getByRole("button", { name: /print/i }));

    const link = await screen.findByRole("link", { name: /open label pdf/i });
    expect(link.getAttribute("href")).toBe("blob:http://localhost/label-pdf");
    expect(onClose).not.toHaveBeenCalled();
  });
});
