import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { MemoryRouter } from "react-router-dom";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { labelService, type LabelPrintStatus, type LabelTarget } from "../../services/labelService";
import { PrintLabelButton } from "./PrintLabelButton";

vi.mock("../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

// Unsigned dummy JWT (test data only) so the real AuthProvider reads permissions.
function seedPermissions(permissions: string[]) {
  const b64 = (obj: object) => btoa(JSON.stringify(obj)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role: "SectionHead", permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
}

const sampleTarget: LabelTarget = {
  entityType: "Sample",
  entityId: 42,
  code: "S-2026-042",
  displayName: "Water Sample"
};

function makeStatus(overrides: Partial<LabelPrintStatus> = {}): LabelPrintStatus {
  return {
    entityType: "Sample",
    entityId: 42,
    canPrint: true,
    blockedReason: null,
    printCount: 0,
    defaultCopies: 1,
    ...overrides
  };
}

function renderButton(target: LabelTarget) {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <PrintLabelButton target={target} />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("PrintLabelButton", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it("shows Print label and opens PrintLabelsDialog when clicked", async () => {
    const user = userEvent.setup();
    seedPermissions([PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB]);
    vi.mocked(labelService.getStatus).mockResolvedValue([makeStatus({ printCount: 0, defaultCopies: 2 })]);

    renderButton(sampleTarget);

    const button = await screen.findByRole("button", { name: "Print label" });
    await waitFor(() => {
      expect((button as HTMLButtonElement).disabled).toBe(false);
    });

    await user.click(button);
    expect(await screen.findByText("Print labels")).toBeTruthy();
  });

  it("shows Reprint label when a label was printed before", async () => {
    seedPermissions([PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB, PERMISSIONS.LABELS_REPRINT]);
    vi.mocked(labelService.getStatus).mockResolvedValue([makeStatus({ printCount: 1 })]);

    renderButton(sampleTarget);

    const button = await screen.findByRole("button", { name: "Reprint label" });
    expect((button as HTMLButtonElement).disabled).toBe(false);
  });

  it("is disabled with its reason when the record has no code", async () => {
    seedPermissions([PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB]);

    renderButton({ ...sampleTarget, code: "" });

    const button = screen.getByRole("button", { name: "Print label" }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    const describedBy = button.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    const description = document.getElementById(describedBy!);
    expect(description?.textContent).toMatch(/no code/i);
  });

  it("is disabled with its reason when the user lacks reprint permission", async () => {
    seedPermissions([PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB]);
    vi.mocked(labelService.getStatus).mockResolvedValue([makeStatus({ printCount: 2 })]);

    renderButton(sampleTarget);

    const button = (await screen.findByRole("button", { name: "Reprint label" })) as HTMLButtonElement;
    await waitFor(() => {
      expect(button.disabled).toBe(true);
    });
    const describedBy = button.getAttribute("aria-describedby");
    expect(describedBy).toBeTruthy();
    const description = document.getElementById(describedBy!);
    expect(description?.textContent).toMatch(/permission/i);
  });
});
