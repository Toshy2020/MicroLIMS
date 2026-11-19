import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { toast } from "sonner";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { type LaboratorySection, laboratorySectionService } from "../../services/laboratorySectionService";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { NotificationMandatoryPanel } from "./NotificationMandatoryPanel";
import { type MandatoryNotificationTypeDto, notificationService } from "./services/notificationService";

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn()
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  laboratorySectionService: {
    getMySections: vi.fn()
  }
}));

vi.mock("./services/notificationService", () => ({
  notificationService: {
    getMandatory: vi.fn(),
    putMandatory: vi.fn()
  }
}));

const mySections: LaboratorySection[] = [
  {
    sectionId: 11,
    sectionCode: "MICRO",
    sectionName: "Microbiology",
    departmentId: 1,
    departmentCode: "QC",
    departmentName: "Quality Control"
  },
  {
    sectionId: 12,
    sectionCode: "CHEM",
    sectionName: "Chemistry",
    departmentId: 1,
    departmentCode: "QC",
    departmentName: "Quality Control"
  }
];

const microTypes: MandatoryNotificationTypeDto[] = [
  {
    type: "sample.approved",
    title: "Sample approved",
    emailAllowed: true,
    mandatory: false,
    setByUserId: null,
    setAt: null
  },
  {
    type: "oos.raised",
    title: "Out-of-specification raised",
    emailAllowed: true,
    mandatory: true,
    setByUserId: 2,
    setAt: "2026-11-14T10:00:00Z"
  }
];

const chemTypes: MandatoryNotificationTypeDto[] = [
  {
    type: "sample.approved",
    title: "Sample approved",
    emailAllowed: true,
    mandatory: false,
    setByUserId: null,
    setAt: null
  },
  {
    type: "oos.raised",
    title: "Out-of-specification raised",
    emailAllowed: true,
    mandatory: false,
    setByUserId: null,
    setAt: null
  }
];

function makeAxiosError(data: unknown, message = "Request failed"): AxiosError {
  return new AxiosError(
    message,
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      data,
      status: 400,
      statusText: "Bad Request",
      headers: {},
      config: { headers: new AxiosHeaders() }
    }
  );
}

function renderPanel(permissions: string[], onSaved = vi.fn()) {
  localStorage.clear();
  localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <NotificationMandatoryPanel onSaved={onSaved} />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

describe("NotificationMandatoryPanel", () => {
  it("is hidden when user lacks Notifications.Configure permission", () => {
    const { container } = renderPanel([PERMISSIONS.SAMPLES_REVIEW]);

    expect(container.firstChild).toBeNull();
    expect(laboratorySectionService.getMySections).not.toHaveBeenCalled();
    expect(notificationService.getMandatory).not.toHaveBeenCalled();
  });

  it("loads user sections, switches section, toggles a type, and saves mandatory list", async () => {
    const user = userEvent.setup();
    const onSaved = vi.fn();
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue(mySections);
    vi.mocked(notificationService.getMandatory)
      .mockResolvedValueOnce(microTypes)
      .mockResolvedValueOnce(chemTypes);
    vi.mocked(notificationService.putMandatory).mockResolvedValue([
      { ...chemTypes[0], mandatory: true, setByUserId: 7, setAt: "2026-11-15T05:00:00Z" },
      chemTypes[1]
    ]);

    renderPanel([PERMISSIONS.NOTIFICATIONS_CONFIGURE], onSaved);

    await screen.findByText("Section mandatory notifications");
    await screen.findByRole("switch", { name: "Out-of-specification raised mandatory" });
    expect(notificationService.getMandatory).toHaveBeenCalledWith(11, expect.any(AbortSignal));

    // Switch to Chemistry section
    await user.click(screen.getByRole("combobox", { name: "Laboratory section" }));
    await user.click(await screen.findByRole("option", { name: "Chemistry" }));

    await waitFor(() => {
      expect(notificationService.getMandatory).toHaveBeenCalledWith(12, expect.any(AbortSignal));
    });

    const approvedToggle = (await screen.findByRole("switch", { name: "Sample approved mandatory" })) as HTMLInputElement;
    expect(approvedToggle.checked).toBe(false);
    await user.click(approvedToggle);
    expect(approvedToggle.checked).toBe(true);

    await user.click(screen.getByRole("button", { name: "Save mandatory types" }));

    await waitFor(() => {
      expect(notificationService.putMandatory).toHaveBeenCalledWith(12, ["sample.approved"]);
    });
    expect(toast.success).toHaveBeenCalledWith("Mandatory notification types saved.");
    expect(onSaved).toHaveBeenCalledTimes(1);
  });

  it("shows empty message when user has no laboratory sections", async () => {
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([]);

    renderPanel([PERMISSIONS.NOTIFICATIONS_CONFIGURE]);

    expect(await screen.findByText("You are not assigned to any laboratory sections.")).toBeTruthy();
  });

  it("shows server error when saving mandatory types fails", async () => {
    const user = userEvent.setup();
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue(mySections);
    vi.mocked(notificationService.getMandatory).mockResolvedValue(microTypes);
    vi.mocked(notificationService.putMandatory).mockRejectedValue(
      makeAxiosError({
        success: false,
        message: "Only email-enabled notification types can be marked mandatory.",
        fieldErrors: [{ field: "types[0]", message: "Only email-enabled notification types can be marked mandatory." }]
      })
    );

    renderPanel([PERMISSIONS.NOTIFICATIONS_CONFIGURE]);

    await screen.findByRole("switch", { name: "Out-of-specification raised mandatory" });
    await user.click(screen.getByRole("button", { name: "Save mandatory types" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Only email-enabled notification types can be marked mandatory.");
    expect(toast.error).toHaveBeenCalledWith("Only email-enabled notification types can be marked mandatory.");
  });
});
