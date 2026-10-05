import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { UserSectionsDialog } from "./UserSectionsDialog";
import * as labService from "../../../services/laboratorySectionService";
import { UserRecord } from "../services/UserService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

vi.mock("../../../services/laboratorySectionService", async () => {
  const actual = await vi.importActual<typeof labService>("../../../services/laboratorySectionService");
  return {
    ...actual,
    getSections: vi.fn(),
    getUserMemberships: vi.fn(),
    replaceUserMemberships: vi.fn()
  };
});

const mockSections: labService.LaboratorySection[] = [
  {
    departmentId: 1,
    departmentName: "Quality Control",
    departmentCode: "QC",
    sectionId: 101,
    sectionName: "Physicochemical",
    sectionCode: "FP"
  },
  {
    departmentId: 1,
    departmentName: "Quality Control",
    departmentCode: "QC",
    sectionId: 102,
    sectionName: "Microbiology",
    sectionCode: "MB"
  }
];

const mockUser: UserRecord = {
  id: 42,
  fullName: "Jane Analyst",
  username: "janalyst",
  email: "jane@example.com",
  roleId: 2,
  role: { id: 2, name: "Analyst", type: "LabUser" },
  isActive: true,
  isLocked: false,
  lockedUntil: null,
  mustChangePassword: false,
  createdAt: "2026-01-01T00:00:00Z",
  lastLoginAt: null,
  passwordChangedAt: null
};

function renderDialog(props: Partial<Parameters<typeof UserSectionsDialog>[0]> = {}) {
  return render(
    <PinnedLightTheme>
      <UserSectionsDialog
        open={true}
        onClose={vi.fn()}
        user={mockUser}
        onSuccess={vi.fn()}
        {...props}
      />
    </PinnedLightTheme>
  );
}

describe("UserSectionsDialog - Lane F5 S3 physchemArea", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(labService.getSections).mockResolvedValue(mockSections);
    vi.mocked(labService.getUserMemberships).mockResolvedValue([]);
    vi.mocked(labService.replaceUserMemberships).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
  });

  it("renders area select only for the FP section membership and not for other sections", async () => {
    vi.mocked(labService.getUserMemberships).mockResolvedValue([
      { departmentId: 1, sectionId: 101, physchemArea: null },
      { departmentId: 1, sectionId: 102, physchemArea: null }
    ]);

    renderDialog();

    // Wait for the sections to load
    await waitFor(() => {
      expect(screen.getByText(/Physicochemical \(FP\)/i)).toBeTruthy();
      expect(screen.getByText(/Microbiology \(MB\)/i)).toBeTruthy();
    });

    // The FP section should have the Area select
    const areaSelects = screen.getAllByTestId("physchem-area-select");
    expect(areaSelects).toHaveLength(1);

    // Combobox labeled Area should exist
    const combobox = screen.getByRole("combobox", { name: /area/i });
    expect(combobox).toBeTruthy();
    expect(combobox.textContent).toContain("Both");
  });

  it("dynamically shows area select when FP is checked and hides it when unchecked", async () => {
    vi.mocked(labService.getUserMemberships).mockResolvedValue([]);

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText(/Physicochemical \(FP\)/i)).toBeTruthy();
    });

    // Initially no section is checked, so no Area select is shown
    expect(screen.queryByTestId("physchem-area-select")).toBeNull();

    // Check MB section: area select should still NOT appear
    const mbCheckbox = screen.getByRole("checkbox", { name: /Microbiology \(MB\)/i });
    fireEvent.click(mbCheckbox);
    expect(screen.queryByTestId("physchem-area-select")).toBeNull();

    // Check FP section: area select appears
    const fpCheckbox = screen.getByRole("checkbox", { name: /Physicochemical \(FP\)/i });
    fireEvent.click(fpCheckbox);

    await waitFor(() => {
      expect(screen.getByTestId("physchem-area-select")).toBeTruthy();
    });
    expect(screen.getAllByTestId("physchem-area-select")).toHaveLength(1);

    // Uncheck FP section: area select disappears
    fireEvent.click(fpCheckbox);
    expect(screen.queryByTestId("physchem-area-select")).toBeNull();
  });

  it("maps Both to null when saving memberships", async () => {
    vi.mocked(labService.getUserMemberships).mockResolvedValue([
      { departmentId: 1, sectionId: 101, physchemArea: null }
    ]);

    const onSuccess = vi.fn();
    const onClose = vi.fn();

    renderDialog({ onSuccess, onClose });

    await waitFor(() => {
      expect(screen.getByTestId("physchem-area-select")).toBeTruthy();
    });

    const saveButton = screen.getByRole("button", { name: /save memberships/i });
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(labService.replaceUserMemberships).toHaveBeenCalledWith(42, [
        {
          departmentId: 1,
          sectionId: 101,
          physchemArea: null
        }
      ]);
      expect(onSuccess).toHaveBeenCalledWith('Laboratory sections updated for "janalyst".');
      expect(onClose).toHaveBeenCalled();
    });
  });

  it("maps FP to FinishedProduct and RM & PM to RawPackaging, and Both back to null", async () => {
    vi.mocked(labService.getUserMemberships).mockResolvedValue([
      { departmentId: 1, sectionId: 101, physchemArea: null }
    ]);

    renderDialog();

    await waitFor(() => {
      expect(screen.getByTestId("physchem-area-select")).toBeTruthy();
    });

    // Open select and choose FP ("FinishedProduct")
    const combobox = screen.getByRole("combobox", { name: /area/i });
    fireEvent.mouseDown(combobox);
    const fpOption = await screen.findByRole("option", { name: /^FP$/i });
    fireEvent.click(fpOption);

    // Save and verify FinishedProduct
    const saveButton = screen.getByRole("button", { name: /save memberships/i });
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(labService.replaceUserMemberships).toHaveBeenLastCalledWith(42, [
        {
          departmentId: 1,
          sectionId: 101,
          physchemArea: "FinishedProduct"
        }
      ]);
    });

    // Now change to RM & PM ("RawPackaging")
    fireEvent.mouseDown(screen.getByRole("combobox", { name: /area/i }));
    const rmPmOption = await screen.findByRole("option", { name: /RM & PM/i });
    fireEvent.click(rmPmOption);

    fireEvent.click(saveButton);
    await waitFor(() => {
      expect(labService.replaceUserMemberships).toHaveBeenLastCalledWith(42, [
        {
          departmentId: 1,
          sectionId: 101,
          physchemArea: "RawPackaging"
        }
      ]);
    });

    // Now change back to Both (maps to null)
    fireEvent.mouseDown(screen.getByRole("combobox", { name: /area/i }));
    const bothOption = await screen.findByRole("option", { name: /^Both$/i });
    fireEvent.click(bothOption);

    fireEvent.click(saveButton);
    await waitFor(() => {
      expect(labService.replaceUserMemberships).toHaveBeenLastCalledWith(42, [
        {
          departmentId: 1,
          sectionId: 101,
          physchemArea: null
        }
      ]);
    });
  });

  it("displays server error verbatim when save fails", async () => {
    vi.mocked(labService.getUserMemberships).mockResolvedValue([
      { departmentId: 1, sectionId: 101, physchemArea: null }
    ]);
    vi.mocked(labService.replaceUserMemberships).mockRejectedValue({
      response: {
        data: {
          message: "Physchem area is not permitted for section id 102."
        }
      }
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByTestId("physchem-area-select")).toBeTruthy();
    });

    fireEvent.click(screen.getByRole("button", { name: /save memberships/i }));

    await waitFor(() => {
      expect(screen.getByText("Physchem area is not permitted for section id 102.")).toBeTruthy();
    });
  });

  it("displays server error verbatim when loading fails", async () => {
    vi.mocked(labService.getUserMemberships).mockRejectedValue({
      response: {
        data: {
          message: "Failed to connect to laboratory service database."
        }
      }
    });

    renderDialog();

    await waitFor(() => {
      expect(screen.getByText("Failed to connect to laboratory service database.")).toBeTruthy();
    });
  });
});
