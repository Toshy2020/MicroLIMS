import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { Checkbox, FormControlLabel, Stack, TextField } from "@mui/material";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../contexts/AuthContext";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { rawStoredDrafts, resetDraftDb, signInForTest } from "../../services/drafts/draftTestUtils";
import type { FieldError } from "../../services/apiTypes";
import type { UsageSummary } from "../../services/masterDataUsageService";
import { ConfigWizard, type ConfigWizardProps, type WizardStep } from "./ConfigWizard";

interface SampleModel {
  name: string;
  code: string;
  includeDetails: boolean;
  details: string;
  ephemeralSecretNote: string;
}

const EMPTY_MODEL: SampleModel = {
  name: "",
  code: "",
  includeDetails: true,
  details: "",
  ephemeralSecretNote: "",
};

const SAMPLE_STEPS: WizardStep<SampleModel>[] = [
  {
    key: "identity",
    title: "Identity",
    validate: (m) => {
      const errs: Record<string, string> = {};
      if (!m.name.trim()) errs.name = "Name is required";
      if (!m.code.trim()) errs.code = "Code is required";
      return errs;
    },
    render: ({ model, set, errors }) => (
      <Stack spacing={2}>
        <TextField
          name="name"
          label="Name"
          value={model.name}
          onChange={(e) => set({ name: e.target.value })}
          error={Boolean(errors.name)}
          helperText={errors.name}
        />
        <TextField
          name="code"
          label="Code"
          value={model.code}
          onChange={(e) => set({ code: e.target.value })}
          error={Boolean(errors.code)}
          helperText={errors.code}
        />
        <FormControlLabel
          control={
            <Checkbox
              checked={model.includeDetails}
              onChange={(e) => set({ includeDetails: e.target.checked })}
            />
          }
          label="Include details"
        />
        <TextField
          name="ephemeralSecretNote"
          label="Session note"
          value={model.ephemeralSecretNote}
          onChange={(e) => set({ ephemeralSecretNote: e.target.value })}
        />
      </Stack>
    ),
    summary: (m) => [
      { field: "name", label: "Name", value: m.name },
      { field: "code", label: "Code", value: m.code },
    ],
  },
  {
    key: "details",
    title: "Details",
    applies: (m) => m.includeDetails,
    validate: (m) => {
      const errs: Record<string, string> = {};
      if (!m.details.trim()) errs.details = "Details are required";
      return errs;
    },
    render: ({ model, set, errors }) => (
      <TextField
        name="details"
        label="Details"
        value={model.details}
        onChange={(e) => set({ details: e.target.value })}
        error={Boolean(errors.details)}
        helperText={errors.details}
      />
    ),
    summary: (m) => [{ field: "details", label: "Details", value: m.details }],
  },
];

function wrap(ui: ReactNode) {
  return (
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>{ui}</MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

function renderWizard(overrides: Partial<ConfigWizardProps<SampleModel>> = {}) {
  const onSave = vi.fn().mockResolvedValue(undefined);
  const onClose = vi.fn();
  const props: ConfigWizardProps<SampleModel> = {
    title: "Sample Wizard",
    mode: "create",
    steps: SAMPLE_STEPS,
    initial: EMPTY_MODEL,
    draftKey: "sample-wizard-1",
    draftFields: ["name", "code", "includeDetails", "details"],
    onSave,
    onClose,
    ...overrides,
  };
  const rendered = render(wrap(<ConfigWizard {...props} />));
  return { ...rendered, onSave, onClose, props };
}

async function advanceToReview(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText("Name"), "Plate Count");
  await user.type(screen.getByLabelText("Code"), "PC-01");
  await user.click(screen.getByRole("button", { name: "Next" }));
  await user.type(screen.getByLabelText("Details"), "TSA 30-35C");
  await user.click(screen.getByRole("button", { name: "Next" }));
}

function makeConflictError() {
  return new AxiosError(
    "Conflict",
    "409",
    undefined,
    undefined,
    {
      status: 409,
      statusText: "Conflict",
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { message: "Record was modified by another user." },
    }
  );
}

function makeReasonRequiredError() {
  return new AxiosError(
    "Reason required",
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      status: 400,
      statusText: "Bad Request",
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: {
        message: "A reason of at least 10 characters is required.",
        fieldErrors: [{ field: "reason", message: "Reason must be at least 10 characters." }],
      },
    }
  );
}

const OPEN_USAGE: UsageSummary = {
  type: "Specification",
  id: 12,
  openWorkCount: 4,
  historicalCount: 9,
  lines: [
    {
      key: "open-test-orders",
      label: "Open test orders",
      count: 4,
      isOpenWork: true,
      listRoute: "/receiving-testing",
    },
  ],
};

describe("ConfigWizard", () => {
  beforeEach(async () => {
    localStorage.clear();
    await resetDraftDb();
    signInForTest(7);
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("blocks Next on invalid step, shows errors, marks step, and focuses first invalid field", async () => {
    const user = userEvent.setup();
    renderWizard();

    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(screen.getByText("Name is required")).toBeTruthy();
    expect(screen.getByText("Code is required")).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByLabelText("Name"));

    const stepper = screen.getByRole("list", { name: "Wizard steps" });
    expect(within(stepper).getByText("needs attention")).toBeTruthy();
    expect(stepper.querySelector('[data-testid="WarningIcon"]')).toBeTruthy();
  });

  it("keeps entered values when navigating Back", async () => {
    const user = userEvent.setup();
    renderWizard();

    await user.type(screen.getByLabelText("Name"), "Bioburden");
    await user.type(screen.getByLabelText("Code"), "BB-1");
    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(screen.getByLabelText("Details")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Back" }));

    expect((screen.getByLabelText("Name") as HTMLInputElement).value).toBe("Bioburden");
    expect((screen.getByLabelText("Code") as HTMLInputElement).value).toBe("BB-1");
  });

  it("omits steps where applies(model) is false and excludes them from Review summary", async () => {
    const user = userEvent.setup();
    renderWizard();

    await user.type(screen.getByLabelText("Name"), "pH Check");
    await user.type(screen.getByLabelText("Code"), "PH-1");
    await user.click(screen.getByRole("checkbox", { name: "Include details" }));

    const stepper = screen.getByRole("list", { name: "Wizard steps" });
    expect(within(stepper).queryByText("Details")).toBeNull();

    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(screen.getByRole("button", { name: "Save" })).toBeTruthy();
    expect(screen.queryByRole("region", { name: "Details" })).toBeNull();
    expect(screen.getByRole("region", { name: "Identity" })).toBeTruthy();
  });

  it("groups summary rows by step on Review, and Edit jumps to step and focuses field", async () => {
    const user = userEvent.setup();
    renderWizard();

    await advanceToReview(user);

    const identitySection = screen.getByRole("region", { name: "Identity" });
    const detailsSection = screen.getByRole("region", { name: "Details" });
    expect(within(identitySection).getByText("Plate Count")).toBeTruthy();
    expect(within(identitySection).getByText("PC-01")).toBeTruthy();
    expect(within(detailsSection).getByText("TSA 30-35C")).toBeTruthy();

    await user.click(within(detailsSection).getByRole("button", { name: "Edit" }));
    expect(document.activeElement).toBe(screen.getByLabelText("Details"));
  });

  it("shows before/after diff in edit mode only for changed values", async () => {
    const user = userEvent.setup();
    const original: SampleModel = {
      name: "Original Name",
      code: "ORIG-1",
      includeDetails: true,
      details: "Old details",
      ephemeralSecretNote: "",
    };
    renderWizard({ mode: "edit", initial: original, original });

    const nameInput = screen.getByLabelText("Name");
    await user.clear(nameInput);
    await user.type(nameInput, "Renamed Test");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    const changesSection = screen.getByRole("region", { name: "Changes" });
    expect(within(changesSection).getByText("Original Name")).toBeTruthy();
    expect(within(changesSection).getByText("Renamed Test")).toBeTruthy();
    expect(within(changesSection).queryByText("ORIG-1")).toBeNull();
    expect(within(changesSection).queryByText("Old details")).toBeNull();
  });

  it("displays server FieldErrors on Review, marks their step, and blocks Save until fixed", async () => {
    const user = userEvent.setup();
    const validateOnServer = vi
      .fn<(m: SampleModel) => Promise<FieldError[]>>()
      .mockResolvedValueOnce([{ field: "code", message: "Code already exists" }])
      .mockResolvedValueOnce([]);

    renderWizard({ validateOnServer });

    await advanceToReview(user);

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Code already exists");
    expect((screen.getByRole("button", { name: "Save" }) as HTMLButtonElement).disabled).toBe(true);

    const stepper = screen.getByRole("list", { name: "Wizard steps" });
    expect(within(stepper).getByText("needs attention")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Go to Identity" }));
    expect(document.activeElement).toBe(screen.getByLabelText("Code"));
    await user.type(screen.getByLabelText("Code"), "-NEW");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    await waitFor(() =>
      expect((screen.getByRole("button", { name: "Save" }) as HTMLButtonElement).disabled).toBe(false)
    );
  });

  it("blocks Save with Retry when usage fails and a reason may be required", async () => {
    const user = userEvent.setup();
    const usage = vi
      .fn<() => Promise<UsageSummary>>()
      .mockRejectedValueOnce(new Error("Usage service down"))
      .mockResolvedValueOnce(OPEN_USAGE);

    renderWizard({
      mode: "edit",
      initial: { ...EMPTY_MODEL, name: "A", code: "B", details: "C" },
      original: { ...EMPTY_MODEL, name: "A", code: "B", details: "C" },
      usage,
      reasonRequired: (u) => (u?.openWorkCount ?? 0) > 0,
    });

    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(await screen.findByText(/Usage could not be checked/)).toBeTruthy();
    expect((screen.getByRole("button", { name: "Save" }) as HTMLButtonElement).disabled).toBe(true);

    await user.click(screen.getByRole("button", { name: "Retry" }));

    expect(await screen.findByRole("link", { name: "Open test orders" })).toBeTruthy();
    expect((screen.getByRole("button", { name: "Save" }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("opens ChangeReasonDialog when reasonRequired is true, enforces 10 chars, and passes trimmed reason", async () => {
    const user = userEvent.setup();
    const { onSave } = renderWizard({
      mode: "edit",
      initial: { ...EMPTY_MODEL, name: "A", code: "B", details: "C" },
      original: { ...EMPTY_MODEL, name: "A", code: "B", details: "C" },
      usage: () => Promise.resolve(OPEN_USAGE),
      reasonRequired: (u) => (u?.openWorkCount ?? 0) > 0,
    });

    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    await screen.findByRole("link", { name: "Open test orders" });
    await user.click(screen.getByRole("button", { name: "Save" }));

    const reasonInput = await screen.findByRole("textbox", { name: /Reason for change/ });
    const confirmBtn = screen.getByRole("button", { name: "Confirm" }) as HTMLButtonElement;

    await user.type(reasonInput, "  too short ");
    expect(confirmBtn.disabled).toBe(true);
    expect(onSave).not.toHaveBeenCalled();

    await user.clear(reasonInput);
    await user.type(reasonInput, "   Valid audit reason 123   ");
    expect(confirmBtn.disabled).toBe(false);
    await user.click(confirmBtn);

    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    expect(onSave.mock.calls[0][1]).toBe("Valid audit reason 123");
  });

  it("keeps user model on 409 conflict, reloads original, shows diff, and offers Review and save again", async () => {
    const user = userEvent.setup();
    const initial: SampleModel = {
      name: "Initial",
      code: "C-1",
      includeDetails: true,
      details: "Original details",
      ephemeralSecretNote: "",
    };
    const serverLatest: SampleModel = {
      ...initial,
      details: "Server updated details",
    };
    const onSave = vi
      .fn()
      .mockRejectedValueOnce(makeConflictError())
      .mockResolvedValueOnce(undefined);
    const reloadOriginal = vi.fn().mockResolvedValue(serverLatest);

    renderWizard({
      mode: "edit",
      initial,
      original: initial,
      reloadOriginal,
      onSave,
    });

    const nameInput = screen.getByLabelText("Name");
    await user.clear(nameInput);
    await user.type(nameInput, "My Local Name");
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    await user.click(screen.getByRole("button", { name: "Save" }));

    const conflictAlert = await screen.findByRole("alert");
    expect(reloadOriginal).toHaveBeenCalledTimes(1);
    expect(conflictAlert.textContent).toContain("Server updated details");
    expect(conflictAlert.textContent).toContain("My Local Name");

    await user.click(screen.getByRole("button", { name: "Review and save again" }));
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(2));
    expect(onSave.mock.calls[1][0].name).toBe("My Local Name");
  });

  it("persists and restores only draftFields across close and reopen", async () => {
    const user = userEvent.setup();
    const first = renderWizard({ draftKey: "draft-restore-test" });

    await user.type(screen.getByLabelText("Name"), "Saved Draft Name");
    await user.type(screen.getByLabelText("Session note"), "Must NOT persist");

    // Wait for debounced draft write, then unmount (simulates close without discard).
    await new Promise((r) => setTimeout(r, 500));
    // What is written to the tablet holds only the allow-listed fields.
    const stored = JSON.stringify(await rawStoredDrafts());
    expect(stored).toContain("Saved Draft Name");
    expect(stored).not.toContain("Must NOT persist");
    first.unmount();

    renderWizard({ draftKey: "draft-restore-test" });

    const notice = await screen.findByRole("region", { name: "Unsaved entry found" });
    await user.click(within(notice).getByRole("button", { name: "Restore entry" }));

    expect((screen.getByLabelText("Name") as HTMLInputElement).value).toBe("Saved Draft Name");
    expect((screen.getByLabelText("Session note") as HTMLInputElement).value).toBe("");
  });

  it("asks before closing on Escape when dirty with Cancel focused, and closes directly when clean", async () => {
    const user = userEvent.setup();
    const clean = renderWizard();
    await user.keyboard("{Escape}");
    expect(clean.onClose).toHaveBeenCalledTimes(1);
    clean.unmount();

    const dirty = renderWizard();
    await user.type(screen.getByLabelText("Name"), "Unsaved");
    await user.keyboard("{Escape}");

    const confirm = await screen.findByRole("alertdialog");
    expect(dirty.onClose).not.toHaveBeenCalled();
    // Focus moves to Cancel once the dialog has opened; under load that lands after the first render.
    await waitFor(() => expect(document.activeElement).toBe(within(confirm).getByRole("button", { name: "Cancel" })));

    await user.click(within(confirm).getByRole("button", { name: "Cancel" }));
    await waitFor(() => expect(screen.queryByRole("alertdialog")).toBeNull());
    expect(dirty.onClose).not.toHaveBeenCalled();

    await user.keyboard("{Escape}");
    const confirm2 = await screen.findByRole("alertdialog");
    await user.click(within(confirm2).getByRole("button", { name: "Discard" }));
    // Discard clears the stored draft first, then closes.
    await waitFor(() => expect(dirty.onClose).toHaveBeenCalledTimes(1));
  });

  it("renders Stepper with list semantics, aria-current='step', and non-linear jump after visiting all steps", async () => {
    const user = userEvent.setup();
    renderWizard();

    const stepper = screen.getByRole("list", { name: "Wizard steps" });
    const items = within(stepper).getAllByRole("listitem");
    expect(items).toHaveLength(3);
    expect(items[0].getAttribute("aria-current")).toBe("step");
    expect(items[1].getAttribute("aria-current")).toBeNull();
    expect(within(stepper).queryByRole("button", { name: "Identity" })).toBeNull();

    await advanceToReview(user);

    const reviewItems = within(stepper).getAllByRole("listitem");
    expect(reviewItems[2].getAttribute("aria-current")).toBe("step");

    await user.click(within(stepper).getByRole("button", { name: "Identity" }));
    expect(screen.getByLabelText("Name")).toBeTruthy();
    expect(within(stepper).getAllByRole("listitem")[0].getAttribute("aria-current")).toBe("step");
  });

  it("opens ChangeReasonDialog and retries when server returns reason-required error on save with no reason", async () => {
    const user = userEvent.setup();
    const onSave = vi
      .fn()
      .mockRejectedValueOnce(makeReasonRequiredError())
      .mockResolvedValueOnce(undefined);

    const { onClose } = renderWizard({
      mode: "edit",
      initial: { ...EMPTY_MODEL, name: "A", code: "B", details: "C" },
      original: { ...EMPTY_MODEL, name: "A", code: "B", details: "C" },
      onSave,
    });

    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Save" }));

    const reasonInput = await screen.findByRole("textbox", { name: /Reason for change/ });
    expect(screen.queryByText(/A reason of at least 10 characters is required/)).toBeNull();
    expect(screen.queryByRole("alert")).toBeNull();

    await user.type(reasonInput, "Updated per QAR-2026-44");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(onSave).toHaveBeenCalledTimes(2));
    expect(onSave.mock.calls[0][1]).toBeNull();
    expect(onSave.mock.calls[1][1]).toBe("Updated per QAR-2026-44");
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
