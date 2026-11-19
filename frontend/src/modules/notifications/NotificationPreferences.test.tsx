import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { toast } from "sonner";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import {
  type NotificationPreferenceRowDto,
  notificationService
} from "./services/notificationService";
import { NotificationPreferences } from "./NotificationPreferences";

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn()
  }
}));

vi.mock("./services/notificationService", () => ({
  notificationService: {
    getPreferences: vi.fn(),
    putPreferences: vi.fn()
  }
}));

const sampleRows: NotificationPreferenceRowDto[] = [
  {
    type: "sample.received",
    title: "Sample received",
    emailAllowed: false,
    emailMode: "Off",
    mandatory: false,
    mandatoryReason: null
  },
  {
    type: "sample.approved",
    title: "Sample approved",
    emailAllowed: true,
    emailMode: "Digest",
    mandatory: false,
    mandatoryReason: null
  },
  {
    type: "oos.raised",
    title: "Out-of-specification raised",
    emailAllowed: true,
    emailMode: "Immediate",
    mandatory: true,
    mandatoryReason: "Mandatory for Microbiology"
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

function renderComponent(refreshKey = 0) {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <NotificationPreferences refreshKey={refreshKey} />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("NotificationPreferences", () => {
  it("shows in-app as non-editable always on, email options only where allowed, and mandatory rows locked with reason", async () => {
    vi.mocked(notificationService.getPreferences).mockResolvedValue(sampleRows);

    renderComponent();

    const table = await screen.findByRole("table", { name: "Notification preferences" });
    const rows = within(table).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(3);

    for (const row of rows) {
      expect(within(row).getByText("Always on")).toBeTruthy();
      expect(within(row).getByText(/cannot be turned off/i)).toBeTruthy();
    }

    // In-app-only row has no email combobox
    expect(within(rows[0]).getByText("In-app only")).toBeTruthy();
    expect(within(rows[0]).queryByRole("combobox")).toBeNull();

    // Editable email row exposes Off / Immediate / Digest
    const approvedSelect = within(rows[1]).getByRole("combobox", { name: "Sample approved email delivery" });
    expect(approvedSelect.getAttribute("aria-disabled")).not.toBe("true");
    expect(approvedSelect.textContent).toBe("Digest");

    // Mandatory row is disabled and displays mandatoryReason
    expect(within(rows[2]).getByText("Mandatory for Microbiology")).toBeTruthy();
    const oosSelect = within(rows[2]).getByRole("combobox", { name: "Out-of-specification raised email delivery" });
    expect(oosSelect.getAttribute("aria-disabled")).toBe("true");
  });

  it("saves editable email preferences with saving state and toast.success", async () => {
    const user = userEvent.setup();
    vi.mocked(notificationService.getPreferences).mockResolvedValue(sampleRows);

    let resolveSave!: (value: NotificationPreferenceRowDto[]) => void;
    vi.mocked(notificationService.putPreferences).mockImplementation(
      () => new Promise<NotificationPreferenceRowDto[]>((resolve) => {
        resolveSave = resolve;
      })
    );

    renderComponent();

    const select = await screen.findByRole("combobox", { name: "Sample approved email delivery" });
    await user.click(select);
    await user.click(await screen.findByRole("option", { name: "Immediate" }));

    const saveButton = screen.getByRole("button", { name: "Save preferences" });
    await user.click(saveButton);

    expect((screen.getByRole("button", { name: "Saving..." }) as HTMLButtonElement).disabled).toBe(true);
    expect(notificationService.putPreferences).toHaveBeenCalledWith([
      { type: "sample.approved", emailMode: "Immediate" }
    ]);

    resolveSave([
      sampleRows[0],
      { ...sampleRows[1], emailMode: "Immediate" },
      sampleRows[2]
    ]);

    await waitFor(() => {
      expect(toast.success).toHaveBeenCalledWith("Notification preferences saved.");
    });
    expect((screen.getByRole("button", { name: "Save preferences" }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("displays server error and field error when saving fails", async () => {
    const user = userEvent.setup();
    vi.mocked(notificationService.getPreferences).mockResolvedValue(sampleRows);
    vi.mocked(notificationService.putPreferences).mockRejectedValue(
      makeAxiosError({
        success: false,
        message: "Notification type 'sample.approved' is mandatory for QC Lab and cannot be changed.",
        fieldErrors: [
          {
            field: "preferences[0].emailMode",
            message: "Notification type 'sample.approved' is mandatory for QC Lab and cannot be changed."
          }
        ]
      })
    );

    renderComponent();

    await user.click(await screen.findByRole("button", { name: "Save preferences" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("mandatory for QC Lab");
    expect(screen.getAllByText(/mandatory for QC Lab/i).length).toBeGreaterThanOrEqual(2);
    expect(toast.error).toHaveBeenCalledWith(
      "Notification type 'sample.approved' is mandatory for QC Lab and cannot be changed."
    );
  });

  it("renders error state and retries loading preferences", async () => {
    const user = userEvent.setup();
    vi.mocked(notificationService.getPreferences)
      .mockRejectedValueOnce(new Error("Network error"))
      .mockResolvedValueOnce(sampleRows);

    renderComponent();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Could not load notification preferences.");

    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("Sample approved")).toBeTruthy();
    expect(notificationService.getPreferences).toHaveBeenCalledTimes(2);
  });
});
