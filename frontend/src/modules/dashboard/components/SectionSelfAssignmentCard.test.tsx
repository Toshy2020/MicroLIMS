import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { MemoryRouter } from "react-router-dom";
import { toast } from "sonner";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider, type Role } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import {
  sectionSelfAssignmentService,
  type SectionSelfAssignmentDto
} from "../../../services/sectionSelfAssignmentService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { SectionSelfAssignmentCard } from "./SectionSelfAssignmentCard";

vi.mock("../../../services/sectionSelfAssignmentService", () => ({
  sectionSelfAssignmentService: {
    getAll: vi.fn(),
    update: vi.fn()
  }
}));

vi.mock("sonner", () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn()
  }
}));

function seedAuth(permissions: string[], role: Role = "SectionHead") {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_full_name", "Sam Head");
  localStorage.setItem("microlims_username", "shead");
  localStorage.setItem("microlims_user_id", "7");
}

function renderCard() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <SectionSelfAssignmentCard />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

function makeAxiosError(status: number, data: Record<string, unknown>, message = "Request failed"): AxiosError {
  const headers = new AxiosHeaders();
  return new AxiosError(
    message,
    String(status),
    undefined,
    undefined,
    {
      status,
      statusText: String(status),
      headers,
      config: { headers },
      data
    }
  );
}

const CHANGED_AT = "2026-11-15T08:45:00Z";
const MIGRATED_AT = "2026-11-14T12:00:00Z";

const SAMPLE_SECTIONS: SectionSelfAssignmentDto[] = [
  {
    sectionId: 1,
    sectionName: "Microbiology",
    enabled: true,
    changedAt: CHANGED_AT,
    changedByName: "Sam Head",
    version: 3
  },
  {
    sectionId: 2,
    sectionName: "Finished Product",
    enabled: false,
    changedAt: MIGRATED_AT,
    changedByName: null,
    version: 1
  },
  {
    sectionId: 3,
    sectionName: "Raw Materials",
    enabled: true,
    changedAt: null,
    changedByName: null,
    version: 0
  }
];

describe("SectionSelfAssignmentCard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("hides the card and skips fetching when the user lacks Samples.AssignAnalyst", () => {
    seedAuth([PERMISSIONS.TEST_WORKFLOW_EXECUTE], "Analyst");
    renderCard();

    expect(screen.queryByText("Taking tests from My Work")).toBeNull();
    expect(sectionSelfAssignmentService.getAll).not.toHaveBeenCalled();
  });

  it("renders loading skeletons, empty state, and error recovery with Retry", async () => {
    seedAuth([PERMISSIONS.SAMPLES_ASSIGN_ANALYST]);
    const user = userEvent.setup();

    let resolveInitial!: (value: SectionSelfAssignmentDto[]) => void;
    vi.mocked(sectionSelfAssignmentService.getAll).mockImplementationOnce(
      () => new Promise<SectionSelfAssignmentDto[]>((resolve) => {
        resolveInitial = resolve;
      })
    );

    renderCard();
    expect(screen.getByTestId("section-self-assignment-loading")).toBeTruthy();

    resolveInitial([]);
    expect(await screen.findByText("No sections assigned")).toBeTruthy();

    cleanup();
    vi.mocked(sectionSelfAssignmentService.getAll)
      .mockRejectedValueOnce(makeAxiosError(500, { message: "Database temporarily unreachable." }, "Database temporarily unreachable."))
      .mockResolvedValueOnce(SAMPLE_SECTIONS);

    renderCard();
    expect(await screen.findByText("Database temporarily unreachable.")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Retry" }));
    expect(await screen.findByText("Microbiology")).toBeTruthy();
    expect(sectionSelfAssignmentService.getAll).toHaveBeenCalledTimes(3);
  });

  it("lists each section with its On/Off badge, last changed user/time, and Switch", async () => {
    seedAuth([PERMISSIONS.SAMPLES_ASSIGN_ANALYST]);
    vi.mocked(sectionSelfAssignmentService.getAll).mockResolvedValue(SAMPLE_SECTIONS);

    renderCard();

    expect(await screen.findByRole("heading", { name: "Taking tests from My Work" })).toBeTruthy();

    const microRow = screen.getByTestId("section-self-assignment-row-1");
    expect(within(microRow).getByText("Microbiology")).toBeTruthy();
    expect(within(microRow).getByText("On")).toBeTruthy();
    expect(within(microRow).getByText(`Last changed by Sam Head · ${formatDateTimeWithTimeZone(CHANGED_AT)}`)).toBeTruthy();
    expect((within(microRow).getByRole("switch", { name: /Microbiology/i }) as HTMLInputElement).checked).toBe(true);

    const fpRow = screen.getByTestId("section-self-assignment-row-2");
    expect(within(fpRow).getByText("Finished Product")).toBeTruthy();
    expect(within(fpRow).getByText("Off")).toBeTruthy();
    expect(
      within(fpRow).getByText(`Carried over from laboratory-wide setting · ${formatDateTimeWithTimeZone(MIGRATED_AT)}`)
    ).toBeTruthy();
    expect((within(fpRow).getByRole("switch", { name: /Finished Product/i }) as HTMLInputElement).checked).toBe(false);

    const rmRow = screen.getByTestId("section-self-assignment-row-3");
    expect(within(rmRow).getByText("Raw Materials")).toBeTruthy();
    expect(within(rmRow).getByText("On")).toBeTruthy();
    expect(within(rmRow).getByText("Default setting (never changed)")).toBeTruthy();
  });

  it("toggling asks for a reason, sends { enabled, reason, version }, and updates the row", async () => {
    seedAuth([PERMISSIONS.SAMPLES_ASSIGN_ANALYST]);
    const user = userEvent.setup();
    vi.mocked(sectionSelfAssignmentService.getAll).mockResolvedValue([SAMPLE_SECTIONS[0]]);

    const updated: SectionSelfAssignmentDto = {
      sectionId: 1,
      sectionName: "Microbiology",
      enabled: false,
      changedAt: "2026-11-15T10:15:00Z",
      changedByName: "Sam Head",
      version: 4
    };
    vi.mocked(sectionSelfAssignmentService.update).mockResolvedValue(updated);

    renderCard();

    const switchEl = await screen.findByRole("switch", { name: /Taking tests from My Work for Microbiology/i });
    await user.click(switchEl);

    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText("Turn off taking tests from My Work")).toBeTruthy();

    const reasonInput = within(dialog).getByRole("textbox", { name: /Reason for change/i });
    await user.type(reasonInput, "  Audit week workload control in Microbiology  ");
    await user.click(within(dialog).getByRole("button", { name: "Turn off" }));

    await waitFor(() => {
      expect(sectionSelfAssignmentService.update).toHaveBeenCalledWith(1, {
        enabled: false,
        reason: "Audit week workload control in Microbiology",
        version: 3
      });
    });

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    const microRow = screen.getByTestId("section-self-assignment-row-1");
    expect(within(microRow).getByText("Off")).toBeTruthy();
    expect((within(microRow).getByRole("switch", { name: /Microbiology/i }) as HTMLInputElement).checked).toBe(false);
    expect(toast.success).toHaveBeenCalledWith("Taking tests from My Work turned off for Microbiology.");
  });

  it("enforces short-reason guidance and displays the server's field error inside the dialog", async () => {
    seedAuth([PERMISSIONS.SAMPLES_ASSIGN_ANALYST]);
    const user = userEvent.setup();
    vi.mocked(sectionSelfAssignmentService.getAll).mockResolvedValue([SAMPLE_SECTIONS[0]]);
    vi.mocked(sectionSelfAssignmentService.update).mockRejectedValueOnce(
      makeAxiosError(400, {
        message: "Validation failed.",
        fieldErrors: [{ field: "reason", message: "Reason must be at least 10 non-whitespace characters." }]
      })
    );

    renderCard();

    await user.click(await screen.findByRole("switch", { name: /Microbiology/i }));
    const dialog = await screen.findByRole("dialog");
    const reasonInput = within(dialog).getByRole("textbox", { name: /Reason for change/i });
    const confirmBtn = within(dialog).getByRole("button", { name: "Turn off" }) as HTMLButtonElement;

    await user.type(reasonInput, "short");
    expect(confirmBtn.disabled).toBe(true);
    expect(within(dialog).getByText("Enter at least 10 characters.")).toBeTruthy();
    expect(sectionSelfAssignmentService.update).not.toHaveBeenCalled();

    await user.clear(reasonInput);
    await user.type(reasonInput, "1234567890");
    expect(confirmBtn.disabled).toBe(false);
    await user.click(confirmBtn);

    expect(
      await within(dialog).findByText("Reason must be at least 10 non-whitespace characters.")
    ).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });

  it("on 409 conflict shows 'Changed by someone else', closes the dialog, and reloads fresh state", async () => {
    seedAuth([PERMISSIONS.SAMPLES_ASSIGN_ANALYST]);
    const user = userEvent.setup();

    const reloaded: SectionSelfAssignmentDto[] = [
      {
        sectionId: 1,
        sectionName: "Microbiology",
        enabled: false,
        changedAt: "2026-11-15T10:00:00Z",
        changedByName: "Other Section Head",
        version: 9
      }
    ];

    vi.mocked(sectionSelfAssignmentService.getAll)
      .mockResolvedValueOnce([SAMPLE_SECTIONS[0]])
      .mockResolvedValueOnce(reloaded);
    vi.mocked(sectionSelfAssignmentService.update).mockRejectedValueOnce(
      makeAxiosError(409, { message: "Concurrency conflict." }, "This record was changed by someone else.")
    );

    renderCard();

    await user.click(await screen.findByRole("switch", { name: /Microbiology/i }));
    const dialog = await screen.findByRole("dialog");
    await user.type(
      within(dialog).getByRole("textbox", { name: /Reason for change/i }),
      "Shift handover adjustment"
    );
    await user.click(within(dialog).getByRole("button", { name: "Turn off" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(await screen.findByText(/Changed by someone else/i)).toBeTruthy();
    expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/Changed by someone else/i));
    expect(sectionSelfAssignmentService.getAll).toHaveBeenCalledTimes(2);
    expect(await screen.findByText(/Other Section Head/)).toBeTruthy();
    expect(within(screen.getByTestId("section-self-assignment-row-1")).getByText("Off")).toBeTruthy();
  });
});
