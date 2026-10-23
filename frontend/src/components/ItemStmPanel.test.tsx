import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../theme";
import { ItemStmPanel } from "./ItemStmPanel";
import { ItemStmService, ItemStmDto } from "../modules/laboratoryConfiguration/items/services/ItemStmService";
import { documentControlService } from "../modules/documentControl/services/documentControlService";

vi.mock("../modules/laboratoryConfiguration/items/services/ItemStmService", () => ({
  ItemStmService: {
    getForItem: vi.fn(),
  },
}));

vi.mock("../modules/documentControl/services/documentControlService", () => ({
  documentControlService: {
    viewFileBlob: vi.fn(),
    downloadFile: vi.fn(),
  },
}));

describe("ItemStmPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  const renderComponent = (itemId: number = 1) =>
    render(
      <ThemeProvider theme={lightTheme}>
        <ItemStmPanel itemId={itemId} />
      </ThemeProvider>
    );

  it("shows warning when status is NotLinked", async () => {
    const dto: ItemStmDto = {
      status: "NotLinked",
      sopNumber: null,
      documentMasterId: null,
      companyDocumentCode: null,
      title: null,
      documentTypeCode: null,
      revisionNumber: null,
      effectiveDate: null,
      controlledPdfFileId: null,
      controlledPdfFileName: null,
      canDownload: false,
    };
    vi.mocked(ItemStmService.getForItem).mockResolvedValue(dto);

    renderComponent(1);

    await waitFor(() => {
      expect(screen.getByText("No effective STM")).toBeTruthy();
    });
    expect(screen.getByText("This item has no SOP number.")).toBeTruthy();
  });

  it("shows warning when status is NotFound", async () => {
    const dto: ItemStmDto = {
      status: "NotFound",
      sopNumber: "STM-999",
      documentMasterId: null,
      companyDocumentCode: null,
      title: null,
      documentTypeCode: null,
      revisionNumber: null,
      effectiveDate: null,
      controlledPdfFileId: null,
      controlledPdfFileName: null,
      canDownload: false,
    };
    vi.mocked(ItemStmService.getForItem).mockResolvedValue(dto);

    renderComponent(2);

    await waitFor(() => {
      expect(screen.getByText("No effective STM")).toBeTruthy();
    });
    expect(screen.getByText("SOP number STM-999 is not in the Document Library.")).toBeTruthy();
  });

  it("shows warning when status is NoEffectiveRevision", async () => {
    const dto: ItemStmDto = {
      status: "NoEffectiveRevision",
      sopNumber: "STM-RM-102",
      documentMasterId: 42,
      companyDocumentCode: "STM-RM-102",
      title: "Raw Material Specification",
      documentTypeCode: "STM",
      revisionNumber: null,
      effectiveDate: null,
      controlledPdfFileId: null,
      controlledPdfFileName: null,
      canDownload: false,
    };
    vi.mocked(ItemStmService.getForItem).mockResolvedValue(dto);

    renderComponent(3);

    await waitFor(() => {
      expect(screen.getByText("No effective STM")).toBeTruthy();
    });
    expect(screen.getByText("STM-RM-102 has no effective revision yet.")).toBeTruthy();
  });

  it("shows effective STM details and View/Download buttons when canDownload is true", async () => {
    const dto: ItemStmDto = {
      status: "Effective",
      sopNumber: "STM-RM-102",
      documentMasterId: 42,
      companyDocumentCode: "STM-RM-102",
      title: "Specification for Raw Material",
      documentTypeCode: "STM",
      revisionNumber: "01",
      effectiveDate: "2026-10-15T12:00:00Z",
      controlledPdfFileId: 101,
      controlledPdfFileName: "STM-RM-102_rev01.pdf",
      canDownload: true,
    };
    vi.mocked(ItemStmService.getForItem).mockResolvedValue(dto);

    const openSpy = vi.spyOn(window, "open").mockImplementation(() => null);
    vi.mocked(documentControlService.viewFileBlob).mockResolvedValue({
      blob: new Blob(),
      url: "blob:http://localhost/dummy-pdf-blob",
    });

    renderComponent(4);

    await waitFor(() => {
      expect(screen.getByText("STM STM-RM-102")).toBeTruthy();
    });
    expect(screen.getByText("Specification for Raw Material")).toBeTruthy();
    expect(screen.getByText(/Revision 01 · Effective Oct 15, 2026/)).toBeTruthy();

    const viewButton = screen.getByRole("button", { name: /View/i });
    const downloadButton = screen.getByRole("button", { name: /Download/i });
    expect(viewButton).toBeTruthy();
    expect(downloadButton).toBeTruthy();

    fireEvent.click(viewButton);

    await waitFor(() => {
      expect(documentControlService.viewFileBlob).toHaveBeenCalledWith(101);
      expect(openSpy).toHaveBeenCalledWith("blob:http://localhost/dummy-pdf-blob", "_blank");
    });

    openSpy.mockRestore();
  });

  it("hides Download button when canDownload is false", async () => {
    const dto: ItemStmDto = {
      status: "Effective",
      sopNumber: "STM-RM-102",
      documentMasterId: 42,
      companyDocumentCode: "STM-RM-102",
      title: "Specification for Raw Material",
      documentTypeCode: "STM",
      revisionNumber: "01",
      effectiveDate: "2026-10-15T12:00:00Z",
      controlledPdfFileId: 101,
      controlledPdfFileName: "STM-RM-102_rev01.pdf",
      canDownload: false,
    };
    vi.mocked(ItemStmService.getForItem).mockResolvedValue(dto);

    renderComponent(5);

    await waitFor(() => {
      expect(screen.getByText("STM STM-RM-102")).toBeTruthy();
    });
    expect(screen.getByRole("button", { name: /View/i })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Download/i })).toBeNull();
  });

  it("calls downloadFile when Download button is clicked", async () => {
    const dto: ItemStmDto = {
      status: "Effective",
      sopNumber: "STM-RM-102",
      documentMasterId: 42,
      companyDocumentCode: "STM-RM-102",
      title: "Specification for Raw Material",
      documentTypeCode: "STM",
      revisionNumber: "01",
      effectiveDate: "2026-10-15T12:00:00Z",
      controlledPdfFileId: 101,
      controlledPdfFileName: "STM-RM-102_rev01.pdf",
      canDownload: true,
    };
    vi.mocked(ItemStmService.getForItem).mockResolvedValue(dto);
    vi.mocked(documentControlService.downloadFile).mockResolvedValue(undefined);

    renderComponent(6);

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /Download/i })).toBeTruthy();
    });

    fireEvent.click(screen.getByRole("button", { name: /Download/i }));

    await waitFor(() => {
      expect(documentControlService.downloadFile).toHaveBeenCalledWith(101, "STM-RM-102_rev01.pdf");
    });
  });

  it("refetches when itemId changes", async () => {
    const dto1: ItemStmDto = {
      status: "NotLinked",
      sopNumber: null,
      documentMasterId: null,
      companyDocumentCode: null,
      title: null,
      documentTypeCode: null,
      revisionNumber: null,
      effectiveDate: null,
      controlledPdfFileId: null,
      controlledPdfFileName: null,
      canDownload: false,
    };
    const dto2: ItemStmDto = {
      status: "NotFound",
      sopNumber: "STM-888",
      documentMasterId: null,
      companyDocumentCode: null,
      title: null,
      documentTypeCode: null,
      revisionNumber: null,
      effectiveDate: null,
      controlledPdfFileId: null,
      controlledPdfFileName: null,
      canDownload: false,
    };
    vi.mocked(ItemStmService.getForItem).mockResolvedValueOnce(dto1).mockResolvedValueOnce(dto2);

    const { rerender } = render(
      <ThemeProvider theme={lightTheme}>
        <ItemStmPanel itemId={10} />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("This item has no SOP number.")).toBeTruthy();
    });

    rerender(
      <ThemeProvider theme={lightTheme}>
        <ItemStmPanel itemId={20} />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("SOP number STM-888 is not in the Document Library.")).toBeTruthy();
    });

    expect(ItemStmService.getForItem).toHaveBeenCalledWith(10);
    expect(ItemStmService.getForItem).toHaveBeenCalledWith(20);
  });
});
