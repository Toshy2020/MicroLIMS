import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import axios, { AxiosHeaders } from "axios";
import { toast } from "sonner";
import { lightTheme } from "../../../theme";
import { labelService, type LabelFormat } from "../../../services/labelService";
import { LabelFormatsPage } from "./LabelFormatsPage";

vi.mock("../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

const sampleFormats: LabelFormat[] = [
  { id: 1, version: 4, entityType: "Sample", widthMm: 50, heightMm: 25, layout: "Standard", defaultCopies: 1 },
  { id: 2, version: 2, entityType: "MediaLot", widthMm: 50, heightMm: 25, layout: "Standard", defaultCopies: 2 },
  { id: 3, version: 1, entityType: "SolutionPreparation", widthMm: 60, heightMm: 30, layout: "Standard", defaultCopies: 1 },
  { id: 4, version: 3, entityType: "Cryovial", widthMm: 25, heightMm: 12, layout: "Small", defaultCopies: 4 },
  { id: 5, version: 1, entityType: "MaterialStockLot", widthMm: 50, heightMm: 25, layout: "Standard", defaultCopies: 1 },
  { id: 6, version: 5, entityType: "EquipmentAsset", widthMm: 40, heightMm: 20, layout: "Small", defaultCopies: 1 }
];

function makeAxiosError(status: number, message: string, errors: unknown = null) {
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
      data: { success: false, message, data: null, errors }
    }
  );
}

function renderPage() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <MemoryRouter>
        <LabelFormatsPage />
      </MemoryRouter>
    </ThemeProvider>
  );
}

beforeEach(() => {
  vi.mocked(labelService.getFormats).mockResolvedValue(sampleFormats);
  vi.mocked(labelService.updateFormat).mockResolvedValue(undefined);
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

describe("LabelFormatsPage", () => {
  it("lists six formats and saves an edited width with its version", async () => {
    const user = userEvent.setup();
    const toastSuccess = vi.spyOn(toast, "success");
    const updatedFormats: LabelFormat[] = [
      { ...sampleFormats[0], version: 5, widthMm: 60 },
      ...sampleFormats.slice(1)
    ];
    vi.mocked(labelService.getFormats)
      .mockResolvedValueOnce(sampleFormats)
      .mockResolvedValueOnce(updatedFormats);

    renderPage();

    expect(await screen.findByText("Sample")).toBeDefined();
    expect(screen.getByText("Media lot")).toBeDefined();
    expect(screen.getByText("Solution preparation")).toBeDefined();
    expect(screen.getByText("Cryovial")).toBeDefined();
    expect(screen.getByText("Material stock lot")).toBeDefined();
    expect(screen.getByText("Equipment")).toBeDefined();
    expect(screen.getByText("60 mm")).toBeDefined();

    await user.click(screen.getByRole("button", { name: "Edit Sample label format" }));

    const widthInput = await screen.findByLabelText(/Width/i);
    await user.clear(widthInput);
    await user.type(widthInput, "60");

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => {
      expect(labelService.updateFormat).toHaveBeenCalledWith(1, 4, {
        widthMm: 60,
        heightMm: 25,
        layout: "Standard",
        defaultCopies: 1
      });
    });

    await waitFor(() => {
      expect(labelService.getFormats).toHaveBeenCalledTimes(2);
    });
    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
    });
    expect(toastSuccess).toHaveBeenCalled();
  });

  it("shows the stale-version message on 409", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.updateFormat).mockRejectedValueOnce(
      makeAxiosError(409, "This record was changed by someone else.")
    );

    renderPage();

    await screen.findByText("Sample");
    await user.click(screen.getByRole("button", { name: "Edit Sample label format" }));

    const widthInput = await screen.findByLabelText(/Width/i);
    await user.clear(widthInput);
    await user.type(widthInput, "55");
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Someone else changed this format. Reload and try again.")).toBeDefined();
    expect(screen.getByRole("dialog")).toBeDefined();
  });

  it("shows field errors for out-of-range sizes returned by the server", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.updateFormat).mockRejectedValueOnce(
      makeAxiosError(400, "Validation failed.", {
        widthMm: "Width must be between 10 and 150 mm.",
        heightMm: "Height must be between 10 and 150 mm."
      })
    );

    renderPage();

    await screen.findByText("Sample");
    await user.click(screen.getByRole("button", { name: "Edit Sample label format" }));

    const widthInput = await screen.findByLabelText(/Width/i);
    await user.clear(widthInput);
    await user.type(widthInput, "999");
    await user.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Width must be between 10 and 150 mm.")).toBeDefined();
    expect(screen.getByText("Height must be between 10 and 150 mm.")).toBeDefined();
    expect(widthInput.getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByLabelText(/Height/i).getAttribute("aria-invalid")).toBe("true");
  });

  it("shows the empty state when no formats exist", async () => {
    vi.mocked(labelService.getFormats).mockResolvedValueOnce([]);

    renderPage();

    expect(await screen.findByText("No label formats configured")).toBeDefined();
  });

  it("shows the load error and retries", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.getFormats)
      .mockRejectedValueOnce(makeAxiosError(500, "Label service is unavailable."))
      .mockResolvedValueOnce(sampleFormats);

    renderPage();

    expect(await screen.findByText("Label service is unavailable.")).toBeDefined();
    await user.click(screen.getByRole("button", { name: "Retry" }));

    expect(await screen.findByText("Sample")).toBeDefined();
    expect(labelService.getFormats).toHaveBeenCalledTimes(2);
  });
});
