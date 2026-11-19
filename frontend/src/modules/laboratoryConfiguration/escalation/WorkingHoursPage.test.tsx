import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { toast } from "sonner";
import { i18n } from "../../../i18n";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { WorkingHoursPage } from "./WorkingHoursPage";
import {
  type WorkingHoursDto,
  workingHoursService
} from "./services/workingHoursService";

vi.mock("./services/workingHoursService", () => ({
  workingHoursService: {
    get: vi.fn(),
    update: vi.fn()
  }
}));

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn()
  }
}));

function makeSchedule(over: Partial<WorkingHoursDto> = {}): WorkingHoursDto {
  return {
    timeZoneId: "Africa/Cairo",
    timeZoneDisplayName: "(UTC+02:00) Cairo",
    isConfigured: true,
    days: [
      { dayOfWeek: "Sunday", isWorkingDay: true, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Monday", isWorkingDay: true, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Tuesday", isWorkingDay: true, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Wednesday", isWorkingDay: true, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Thursday", isWorkingDay: true, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Friday", isWorkingDay: false, startMinute: 0, endMinute: 0 },
      { dayOfWeek: "Saturday", isWorkingDay: false, startMinute: 0, endMinute: 0 }
    ],
    ...over
  };
}

function makeAxiosError(data: unknown, status = 400): AxiosError {
  return new AxiosError(
    "Request failed",
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      data,
      status,
      statusText: "Bad Request",
      headers: {},
      config: { headers: new AxiosHeaders() }
    }
  );
}

function renderPage() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={["/laboratory-configuration/working-hours"]}>
        <Routes>
          <Route path="/laboratory-configuration/working-hours" element={<WorkingHoursPage />} />
          <Route path="/monitoring-plan" element={<div>Monitoring Plan</div>} />
        </Routes>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe("WorkingHoursPage", () => {
  it("renders seven rows with working-day switches, start/end times, time zone, and non-working dates link", async () => {
    vi.mocked(workingHoursService.get).mockResolvedValue(makeSchedule());

    renderPage();

    const table = await screen.findByRole("table", { name: "Laboratory working hours" });
    const rows = within(table).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(7);

    expect(screen.getByText(/Africa\/Cairo/)).toBeTruthy();
    expect(screen.getByText(/\(UTC\+02:00\) Cairo/)).toBeTruthy();

    const datesLink = screen.getByRole("link", { name: /non-working dates/i });
    expect(datesLink.getAttribute("href")).toBe("/monitoring-plan");

    const sundaySwitch = screen.getByRole("switch", { name: "Sunday working day" }) as HTMLInputElement;
    const fridaySwitch = screen.getByRole("switch", { name: "Friday working day" }) as HTMLInputElement;
    expect(sundaySwitch.checked).toBe(true);
    expect(fridaySwitch.checked).toBe(false);

    const sundayStart = screen.getByLabelText("Sunday start time") as HTMLInputElement;
    const sundayEnd = screen.getByLabelText("Sunday end time") as HTMLInputElement;
    const fridayStart = screen.getByLabelText("Friday start time") as HTMLInputElement;
    expect(sundayStart.value).toBe("08:00");
    expect(sundayEnd.value).toBe("16:00");
    expect(sundayStart.disabled).toBe(false);
    expect(fridayStart.disabled).toBe(true);
  });

  it("shows loading state, toggles days, edits times, shows saving state, and saves all seven days", async () => {
    const user = userEvent.setup();

    let resolveGet!: (value: WorkingHoursDto) => void;
    vi.mocked(workingHoursService.get).mockImplementation(
      () => new Promise<WorkingHoursDto>((resolve) => { resolveGet = resolve; })
    );

    renderPage();
    expect(screen.getByRole("status")).toBeTruthy();

    resolveGet(makeSchedule({
      isConfigured: false,
      days: makeSchedule().days.map((d) => ({ ...d, isWorkingDay: false, startMinute: 8 * 60, endMinute: 16 * 60 }))
    }));

    expect(await screen.findByText(/Working hours are not configured yet/i)).toBeTruthy();

    await user.click(screen.getByRole("switch", { name: "Sunday working day" }));
    const sundayStart = screen.getByLabelText("Sunday start time") as HTMLInputElement;
    const sundayEnd = screen.getByLabelText("Sunday end time") as HTMLInputElement;
    await user.clear(sundayStart);
    await user.type(sundayStart, "09:00");
    await user.clear(sundayEnd);
    await user.type(sundayEnd, "17:30");

    let resolveUpdate!: (value: WorkingHoursDto) => void;
    vi.mocked(workingHoursService.update).mockImplementation(
      () => new Promise<WorkingHoursDto>((resolve) => { resolveUpdate = resolve; })
    );

    await user.click(screen.getByRole("button", { name: /save working hours/i }));

    const savingBtn = screen.getByRole("button", { name: /saving/i }) as HTMLButtonElement;
    expect(savingBtn.disabled).toBe(true);

    expect(workingHoursService.update).toHaveBeenCalledTimes(1);
    const submitted = vi.mocked(workingHoursService.update).mock.calls[0][0];
    expect(submitted).toHaveLength(7);
    expect(submitted[0]).toEqual({
      dayOfWeek: "Sunday",
      isWorkingDay: true,
      startMinute: 9 * 60,
      endMinute: 17 * 60 + 30
    });

    resolveUpdate(makeSchedule({ isConfigured: true }));

    await waitFor(() => {
      expect(toast.success).toHaveBeenCalledWith("Working hours saved.");
    });
    expect(screen.queryByText(/Working hours are not configured yet/i)).toBeNull();
  });

  it("displays inline server validation messages on the failing row and clears them on edit", async () => {
    const user = userEvent.setup();
    vi.mocked(workingHoursService.get).mockResolvedValue(makeSchedule());
    vi.mocked(workingHoursService.update).mockRejectedValue(
      makeAxiosError({
        success: false,
        message: "End time must be after start time.",
        fieldErrors: [
          { field: "endMinute", message: "End time must be after start time.", row: 2 }
        ]
      })
    );

    renderPage();
    await screen.findByRole("table", { name: "Laboratory working hours" });

    const tuesdayEnd = screen.getByLabelText("Tuesday end time") as HTMLInputElement;
    await user.clear(tuesdayEnd);
    await user.type(tuesdayEnd, "07:00");

    await user.click(screen.getByRole("button", { name: /save working hours/i }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("End time must be after start time.");
    expect(tuesdayEnd.getAttribute("aria-invalid")).toBe("true");
    expect(toast.error).toHaveBeenCalledWith("End time must be after start time.");

    const table = screen.getByRole("table", { name: "Laboratory working hours" });
    const tuesdayRow = within(table).getAllByRole("row")[3];
    expect(within(tuesdayRow).getByText("End time must be after start time.")).toBeTruthy();

    await user.clear(tuesdayEnd);
    await user.type(tuesdayEnd, "16:00");
    await waitFor(() => {
      expect(tuesdayEnd.getAttribute("aria-invalid")).toBe("false");
    });
  });

  it("shows load error alert and retries on failure", async () => {
    const user = userEvent.setup();
    vi.mocked(workingHoursService.get)
      .mockRejectedValueOnce(makeAxiosError({ message: "Database unavailable." }, 500))
      .mockResolvedValueOnce(makeSchedule());

    renderPage();

    expect(await screen.findByText("Database unavailable.")).toBeTruthy();
    expect(toast.error).toHaveBeenCalledWith("Database unavailable.");

    await user.click(screen.getByRole("button", { name: /retry/i }));

    expect(await screen.findByRole("table", { name: "Laboratory working hours" })).toBeTruthy();
  });

  it("shows the page heading and day names in Arabic when Arabic is active", async () => {
    vi.mocked(workingHoursService.get).mockResolvedValue(makeSchedule());
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig"]);
    try {
      renderPage();
      expect(await screen.findByText("الجدول الأسبوعي")).toBeTruthy();
      expect(screen.getByText("الأحد")).toBeTruthy();
      expect(screen.getByRole("button", { name: "حفظ ساعات العمل" })).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
