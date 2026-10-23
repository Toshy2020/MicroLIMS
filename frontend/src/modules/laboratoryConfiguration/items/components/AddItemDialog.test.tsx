import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor, act } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { AddItemDialog } from "./AddItemDialog";
import { documentControlService } from "../../../documentControl/services/documentControlService";
import type { DocumentLibraryResponse, DocumentMasterSummaryDto } from "../../../documentControl/types/documentControlTypes";

vi.mock("../../../documentControl/services/documentControlService", () => ({
  documentControlService: {
    getLibrary: vi.fn(),
  },
}));

vi.mock("../../../../components/TestCodePickerMulti", () => ({
  TestCodePickerMulti: ({ value, onChange }: { value: string[]; onChange: (codes: string[]) => void }) => (
    <div data-testid="test-code-picker">
      <button type="button" onClick={() => onChange(["TEST-01"])}>
        Select Test
      </button>
      <span>{value.join(",")}</span>
    </div>
  ),
}));

describe("AddItemDialog - SOP Number Autocomplete", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  const mockLibraryResponse = (items: Partial<DocumentMasterSummaryDto>[]): DocumentLibraryResponse => ({
    items: items as DocumentMasterSummaryDto[],
    totalCount: items.length,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  });

  const renderComponent = (props?: Partial<React.ComponentProps<typeof AddItemDialog>>) => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    const onClose = vi.fn();
    const result = render(
      <ThemeProvider theme={lightTheme}>
        <AddItemDialog
          open={true}
          itemToEdit={null}
          onClose={onClose}
          onSave={onSave}
          {...props}
        />
      </ThemeProvider>
    );
    return { ...result, onSave, onClose };
  };

  it("typing 'STM-RM' calls the library search and lists 'STM-RM-102 — Specification…'", async () => {
    vi.useFakeTimers();
    vi.mocked(documentControlService.getLibrary).mockResolvedValue(
      mockLibraryResponse([
        {
          id: 102,
          companyDocumentCode: "STM-RM-102",
          title: "Specification for Raw Material",
        },
      ])
    );

    renderComponent();

    const sopInput = screen.getByLabelText(/SOP Number/i);
    fireEvent.change(sopInput, { target: { value: "STM-RM" } });

    // 300ms debounce
    expect(documentControlService.getLibrary).not.toHaveBeenCalled();

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    expect(documentControlService.getLibrary).toHaveBeenCalledWith({
      searchTerm: "STM-RM",
      pageSize: 20,
    });

    vi.useRealTimers();

    await waitFor(() => {
      expect(screen.getByText("STM-RM-102 — Specification for Raw Material")).toBeTruthy();
    });
  });

  it("picking an option saves companyDocumentCode as sopNumber", async () => {
    vi.useFakeTimers();
    vi.mocked(documentControlService.getLibrary).mockResolvedValue(
      mockLibraryResponse([
        {
          id: 102,
          companyDocumentCode: "STM-RM-102",
          title: "Specification for Raw Material",
        },
      ])
    );

    const { onSave } = renderComponent();

    // Fill in required fields
    fireEvent.change(screen.getByLabelText(/Item Name/i), { target: { value: "Vitamin C" } });
    fireEvent.change(screen.getByLabelText(/Item Code/i), { target: { value: "RM-VITC" } });
    fireEvent.click(screen.getByText("Select Test"));

    // Type in SOP input to search
    const sopInput = screen.getByLabelText(/SOP Number/i);
    fireEvent.change(sopInput, { target: { value: "STM-RM" } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    vi.useRealTimers();

    const option = await screen.findByText("STM-RM-102 — Specification for Raw Material");
    fireEvent.click(option);

    // Click Save Item
    fireEvent.click(screen.getByRole("button", { name: "Save Item" }));

    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith({
        name: "Vitamin C",
        code: "RM-VITC",
        category: "FinishedProduct",
        sopNumber: "STM-RM-102",
        testCodes: ["TEST-01"],
      });
    });
  });

  it("typing an unmatched value and saving still submits it trimmed", async () => {
    vi.useFakeTimers();
    vi.mocked(documentControlService.getLibrary).mockResolvedValue(mockLibraryResponse([]));

    const { onSave } = renderComponent();

    // Fill in required fields
    fireEvent.change(screen.getByLabelText(/Item Name/i), { target: { value: "Zinc Oxide" } });
    fireEvent.change(screen.getByLabelText(/Item Code/i), { target: { value: "RM-ZINC" } });
    fireEvent.click(screen.getByText("Select Test"));

    // Type unmatched value with leading/trailing spaces
    const sopInput = screen.getByLabelText(/SOP Number/i);
    fireEvent.change(sopInput, { target: { value: "  CUSTOM-SOP-999  " } });

    await act(async () => {
      vi.advanceTimersByTime(350);
    });

    vi.useRealTimers();

    // Click Save Item directly
    fireEvent.click(screen.getByRole("button", { name: "Save Item" }));

    await waitFor(() => {
      expect(onSave).toHaveBeenCalledWith({
        name: "Zinc Oxide",
        code: "RM-ZINC",
        category: "FinishedProduct",
        sopNumber: "CUSTOM-SOP-999",
        testCodes: ["TEST-01"],
      });
    });
  });

  it("does not search library when typing less than 2 characters", async () => {
    vi.useFakeTimers();
    renderComponent();

    const sopInput = screen.getByLabelText(/SOP Number/i);
    fireEvent.change(sopInput, { target: { value: "S" } });

    await act(async () => {
      vi.advanceTimersByTime(500);
    });

    expect(documentControlService.getLibrary).not.toHaveBeenCalled();
    vi.useRealTimers();
  });
});
