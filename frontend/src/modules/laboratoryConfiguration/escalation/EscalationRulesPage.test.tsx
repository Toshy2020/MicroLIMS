import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { toast } from "sonner";
import { type LaboratorySection, getMySections } from "../../../services/laboratorySectionService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { EscalationRulesPage } from "./EscalationRulesPage";
import {
  type EscalationRuleDto,
  escalationRulesService
} from "./services/escalationRulesService";
import {
  type WorkingHoursDto,
  workingHoursService
} from "./services/workingHoursService";

vi.mock("../../../services/laboratorySectionService", () => ({
  getMySections: vi.fn()
}));

vi.mock("./services/workingHoursService", () => ({
  workingHoursService: {
    get: vi.fn(),
    update: vi.fn()
  }
}));

vi.mock("./services/escalationRulesService", async () => {
  const actual = await vi.importActual<typeof import("./services/escalationRulesService")>(
    "./services/escalationRulesService"
  );
  return {
    ...actual,
    escalationRulesService: {
      list: vi.fn(),
      create: vi.fn(),
      replace: vi.fn(),
      retire: vi.fn()
    }
  };
});

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn()
  }
}));

const SECTIONS: LaboratorySection[] = [
  {
    sectionId: 10,
    sectionCode: "MICRO",
    sectionName: "Microbiology Lab",
    departmentId: 1,
    departmentCode: "QC",
    departmentName: "Quality Control"
  },
  {
    sectionId: 20,
    sectionCode: "CHEM",
    sectionName: "Physicochemical Lab",
    departmentId: 1,
    departmentCode: "QC",
    departmentName: "Quality Control"
  }
];

function makeSchedule(isConfigured = true): WorkingHoursDto {
  return {
    timeZoneId: "Africa/Cairo",
    timeZoneDisplayName: "(UTC+02:00) Cairo",
    isConfigured,
    days: [
      { dayOfWeek: "Sunday", isWorkingDay: isConfigured, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Monday", isWorkingDay: isConfigured, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Tuesday", isWorkingDay: isConfigured, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Wednesday", isWorkingDay: isConfigured, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Thursday", isWorkingDay: isConfigured, startMinute: 8 * 60, endMinute: 16 * 60 },
      { dayOfWeek: "Friday", isWorkingDay: false, startMinute: 0, endMinute: 0 },
      { dayOfWeek: "Saturday", isWorkingDay: false, startMinute: 0, endMinute: 0 }
    ]
  };
}

function makeRule(over: Partial<EscalationRuleDto> = {}): EscalationRuleDto {
  return {
    id: 1,
    sectionId: 10,
    sectionName: "Microbiology Lab",
    trigger: "ReviewWaiting",
    thresholdWorkingHours: 8,
    notifyRoleType: "SectionHead",
    isActive: true,
    createdByUserId: 7,
    createdByName: "Dr. Sara",
    createdAt: "2026-11-15T06:00:00Z",
    retiredAt: null,
    retiredByUserId: null,
    retiredByName: null,
    replacesRuleId: null,
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
      <MemoryRouter initialEntries={["/laboratory-configuration/escalation-rules"]}>
        <Routes>
          <Route path="/laboratory-configuration/escalation-rules" element={<EscalationRulesPage />} />
          <Route path="/laboratory-configuration/working-hours" element={<div>Working Hours Calendar Page</div>} />
        </Routes>
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(getMySections).mockResolvedValue(SECTIONS);
  vi.mocked(workingHoursService.get).mockResolvedValue(makeSchedule(true));
  vi.mocked(escalationRulesService.list).mockResolvedValue([makeRule()]);
});

afterEach(() => {
  cleanup();
});

describe("EscalationRulesPage", () => {
  it("lists rules by section with trigger, hours, role, status, and switches sections", async () => {
    const chemRule = makeRule({
      id: 2,
      sectionId: 20,
      sectionName: "Physicochemical Lab",
      trigger: "ApprovalWaiting",
      thresholdWorkingHours: 12,
      notifyRoleType: "QcManager"
    });
    vi.mocked(escalationRulesService.list).mockImplementation(async (params) =>
      params?.sectionId === 20 ? [chemRule] : [makeRule()]
    );

    const user = userEvent.setup();
    renderPage();

    const table = await screen.findByRole("table", { name: "Escalation rules" });
    expect(within(table).getByText("Review waiting")).toBeTruthy();
    expect(within(table).getByText("8 working hours")).toBeTruthy();
    expect(within(table).getByText("Section Head")).toBeTruthy();
    expect(within(table).getByText("Active")).toBeTruthy();

    await user.click(screen.getByRole("combobox", { name: "Section" }));
    await user.click(await screen.findByRole("option", { name: "Physicochemical Lab" }));

    await waitFor(() => {
      expect(escalationRulesService.list).toHaveBeenCalledWith(
        { sectionId: 20, includeRetired: false },
        expect.any(AbortSignal)
      );
    });
    expect(await screen.findByText("Approval waiting")).toBeTruthy();
    expect(screen.getByText("12 working hours")).toBeTruthy();
    expect(screen.getByText("QC Manager")).toBeTruthy();
  });

  it("shows retired and replaced rules with their dates when Show retired is toggled on", async () => {
    const activeReplacement = makeRule({
      id: 2,
      thresholdWorkingHours: 4,
      replacesRuleId: 1
    });
    const replacedOld = makeRule({
      id: 1,
      thresholdWorkingHours: 8,
      isActive: false,
      retiredAt: "2026-11-15T07:30:00Z",
      retiredByUserId: 7,
      retiredByName: "Dr. Sara"
    });
    vi.mocked(escalationRulesService.list).mockImplementation(async (params) =>
      params?.includeRetired ? [activeReplacement, replacedOld] : [activeReplacement]
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("4 working hours");
    expect(screen.queryByText("Replaced")).toBeNull();

    await user.click(screen.getByRole("switch", { name: "Show retired" }));

    await waitFor(() => {
      expect(escalationRulesService.list).toHaveBeenCalledWith(
        { sectionId: 10, includeRetired: true },
        expect.any(AbortSignal)
      );
    });
    expect(await screen.findByText("Replaced")).toBeTruthy();
    expect(screen.getByText(/Retired .* by Dr\. Sara/i)).toBeTruthy();
  });

  it("shows blocked state when working hours are missing, with the reason and a link to working hours", async () => {
    vi.mocked(workingHoursService.get).mockResolvedValue(makeSchedule(false));
    vi.mocked(escalationRulesService.list).mockResolvedValue([]);

    const user = userEvent.setup();
    renderPage();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toMatch(/working hours must be configured/i);
    expect(screen.queryByRole("button", { name: "Add rule" })).toBeNull();

    const link = within(alert).getByRole("link", { name: /working hours calendar/i });
    expect(link.getAttribute("href")).toBe("/laboratory-configuration/working-hours");

    await user.click(link);
    expect(await screen.findByText("Working Hours Calendar Page")).toBeTruthy();
  });

  it("dialog validates hours (1-999), shows 'working hours' unit with link to calendar, and displays server field errors inline", async () => {
    vi.mocked(escalationRulesService.list).mockResolvedValue([]);
    vi.mocked(escalationRulesService.create)
      .mockRejectedValueOnce(
        makeAxiosError({
          message: "Escalation rule validation failed.",
          fieldErrors: [{ field: "thresholdWorkingHours", message: "An active rule with those settings already exists." }]
        })
      )
      .mockResolvedValueOnce(makeRule({ id: 9, thresholdWorkingHours: 24, notifyRoleType: "QcManager" }));

    const user = userEvent.setup();
    renderPage();

    await screen.findByText(/No active escalation rules/i);
    await user.click(screen.getByRole("button", { name: "Add rule" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("working hours")).toBeTruthy();
    const calLink = within(dialog).getByRole("link", { name: /working hours calendar/i });
    expect(calLink.getAttribute("href")).toBe("/laboratory-configuration/working-hours");

    const thresholdInput = within(dialog).getByLabelText("Threshold");
    await user.clear(thresholdInput);
    await user.type(thresholdInput, "0");
    await user.click(within(dialog).getByRole("button", { name: "Create rule" }));

    expect(await within(dialog).findByText("Threshold must be between 1 and 999 working hours.")).toBeTruthy();
    expect(escalationRulesService.create).not.toHaveBeenCalled();

    await user.clear(thresholdInput);
    await user.type(thresholdInput, "1000");
    await user.click(within(dialog).getByRole("button", { name: "Create rule" }));
    expect(await within(dialog).findByText("Threshold must be between 1 and 999 working hours.")).toBeTruthy();
    expect(escalationRulesService.create).not.toHaveBeenCalled();

    // Server field error displays inline
    await user.clear(thresholdInput);
    await user.type(thresholdInput, "24");
    await user.click(within(dialog).getByLabelText("Notify role"));
    await user.click(await screen.findByRole("option", { name: "QC Manager" }));
    await user.click(within(dialog).getByRole("button", { name: "Create rule" }));

    expect(await within(dialog).findByText("An active rule with those settings already exists.")).toBeTruthy();
    expect(toast.error).toHaveBeenCalled();

    // Second submit succeeds
    await user.click(within(dialog).getByRole("button", { name: "Create rule" }));
    await waitFor(() => {
      expect(escalationRulesService.create).toHaveBeenLastCalledWith({
        sectionId: 10,
        trigger: "ReviewWaiting",
        thresholdWorkingHours: 24,
        notifyRoleType: "QcManager"
      });
    });
    expect(toast.success).toHaveBeenCalledWith("Escalation rule created.");
  });

  it("edit says 'This replaces the rule; the old one is kept in history' and calls replace", async () => {
    vi.mocked(escalationRulesService.replace).mockResolvedValue(
      makeRule({ id: 2, thresholdWorkingHours: 6, notifyRoleType: "Reviewer", replacesRuleId: 1 })
    );

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Review waiting");
    await user.click(screen.getByRole("button", { name: "Edit" }));

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("This replaces the rule; the old one is kept in history.")).toBeTruthy();

    const thresholdInput = within(dialog).getByLabelText("Threshold");
    await user.clear(thresholdInput);
    await user.type(thresholdInput, "6");
    await user.click(within(dialog).getByLabelText("Notify role"));
    await user.click(await screen.findByRole("option", { name: "Reviewer" }));
    await user.click(within(dialog).getByRole("button", { name: "Replace rule" }));

    await waitFor(() => {
      expect(escalationRulesService.replace).toHaveBeenCalledWith(1, {
        thresholdWorkingHours: 6,
        notifyRoleType: "Reviewer"
      });
    });
    expect(toast.success).toHaveBeenCalledWith("Escalation rule replaced.");
  });

  it("retire asks for confirmation via ConfirmationDialog with Cancel focused and retires on confirm", async () => {
    vi.mocked(escalationRulesService.retire).mockResolvedValue();

    const user = userEvent.setup();
    renderPage();

    await screen.findByText("Review waiting");
    await user.click(screen.getByRole("button", { name: "Retire" }));

    const confirmDialog = await screen.findByRole("alertdialog");
    const cancelBtn = within(confirmDialog).getByRole("button", { name: "Cancel" });
    await waitFor(() => {
      expect(document.activeElement).toBe(cancelBtn);
    });

    await user.click(cancelBtn);
    await waitFor(() => {
      expect(screen.queryByRole("alertdialog")).toBeNull();
    });
    expect(escalationRulesService.retire).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Retire" }));
    const confirmDialog2 = await screen.findByRole("alertdialog");
    await user.click(within(confirmDialog2).getByRole("button", { name: "Retire" }));

    await waitFor(() => {
      expect(escalationRulesService.retire).toHaveBeenCalledWith(1);
    });
    expect(toast.success).toHaveBeenCalledWith("Escalation rule retired.");
  });

  it("renders loading, no-sections empty, and retryable error states", async () => {
    let resolveSections!: (value: LaboratorySection[]) => void;
    vi.mocked(getMySections).mockImplementationOnce(
      () => new Promise<LaboratorySection[]>((resolve) => { resolveSections = resolve; })
    );

    const user = userEvent.setup();
    renderPage();

    expect(screen.getByRole("status")).toBeTruthy();
    expect(screen.getByText("Loading escalation configuration...")).toBeTruthy();

    resolveSections([]);
    expect(await screen.findByText("You are not assigned to any laboratory sections.")).toBeTruthy();

    cleanup();
    vi.mocked(getMySections).mockResolvedValue(SECTIONS);
    vi.mocked(escalationRulesService.list)
      .mockRejectedValueOnce(makeAxiosError({ message: "Database timeout" }, 500))
      .mockResolvedValueOnce([]);

    renderPage();
    expect(await screen.findByText("Database timeout")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText(/No active escalation rules/i)).toBeTruthy();
  });
});
