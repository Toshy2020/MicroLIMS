import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider, type Role } from "../../contexts/AuthContext";
import { PERMISSIONS } from "../../routes/routes";
import { QualityConfigPage } from "./QualityConfigPage";
import { qualityConfigService } from "../../services/qualityConfigService";
import { getMySections } from "../../services/laboratorySectionService";

vi.mock("../../services/qualityConfigService", () => ({
  qualityConfigService: {
    getTemplate: vi.fn(),
    saveTemplate: vi.fn(),
    listClassifications: vi.fn(),
    createClassification: vi.fn(),
    saveClassification: vi.fn(),
    listDueRules: vi.fn(),
    saveDueRule: vi.fn(),
    getSectionSetting: vi.fn(),
    setSectionSetting: vi.fn()
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  getMySections: vi.fn()
}));

function seedAuth(role: Role, permissions: string[]) {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_full_name", "Quinn QA");
  localStorage.setItem("microlims_username", "qa");
  localStorage.setItem("microlims_user_id", "10");
}

function renderConfigPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <QualityConfigPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("QualityConfigPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(getMySections).mockResolvedValue([
      {
        sectionId: 1,
        sectionCode: "MICRO",
        sectionName: "Microbiology",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "QC"
      },
      {
        sectionId: 2,
        sectionCode: "FP",
        sectionName: "Physicochemical",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "QC"
      }
    ]);
    vi.mocked(qualityConfigService.getTemplate).mockResolvedValue({
      id: 1,
      type: "Deviation",
      sectionId: null,
      version: 3,
      items: [{ key: "k1", order: 1, text: "Verify batch impact", isActive: true }]
    });
    vi.mocked(qualityConfigService.listClassifications).mockResolvedValue([
      { id: 1, name: "Minor", order: 1, isActive: true, version: 2 }
    ]);
    vi.mocked(qualityConfigService.listDueRules).mockResolvedValue([
      { id: 1, type: "Deviation", classificationId: 1, workingDays: 10, version: 4 }
    ]);
    vi.mocked(qualityConfigService.getSectionSetting).mockResolvedValue({
      sectionId: 1,
      changeControlRequired: false,
      version: 5
    });
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("saves checklist template with the loaded version", async () => {
    seedAuth("Qa", [PERMISSIONS.QUALITY_VIEW, PERMISSIONS.QUALITY_APPROVE]);
    vi.mocked(qualityConfigService.saveTemplate).mockResolvedValue({
      id: 1,
      type: "Deviation",
      sectionId: null,
      version: 4,
      items: [
        { key: "k1", order: 1, text: "Verify batch impact", isActive: true },
        { key: "k2", order: 2, text: "Check retain sample", isActive: true }
      ]
    });
    const user = userEvent.setup();
    renderConfigPage();

    expect(await screen.findByDisplayValue("Verify batch impact")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: /add item/i }));
    const inputs = screen.getAllByLabelText(/checklist item text/i);
    await user.type(inputs[1], "Check retain sample");
    await user.click(screen.getByRole("button", { name: /save template/i }));

    await waitFor(() => expect(qualityConfigService.saveTemplate).toHaveBeenCalledTimes(1));
    expect(qualityConfigService.saveTemplate).toHaveBeenCalledWith(
      "Deviation",
      null,
      {
        items: [
          { key: "k1", order: 1, text: "Verify batch impact", isActive: true },
          expect.objectContaining({ order: 2, text: "Check retain sample", isActive: true })
        ]
      },
      3
    );
  });

  it("lets Quality.Investigate without Quality.Approve save a section template but not the site-wide one", async () => {
    seedAuth("SectionHead", [PERMISSIONS.QUALITY_VIEW, PERMISSIONS.QUALITY_INVESTIGATE]);
    const user = userEvent.setup();
    renderConfigPage();

    // Site-wide scope is the default: read-only, with the reason.
    const textbox = (await screen.findByDisplayValue("Verify batch impact")) as HTMLInputElement;
    expect(textbox.disabled).toBe(true);
    expect(screen.queryByRole("button", { name: /save template/i })).toBeNull();
    expect(screen.queryByRole("button", { name: /add item/i })).toBeNull();
    expect(screen.getByText("Only QA can change the site-wide checklist template.")).toBeTruthy();

    await user.click(screen.getByRole("combobox", { name: /scope/i }));
    await user.click(await screen.findByRole("option", { name: "Microbiology" }));

    expect(await screen.findByRole("button", { name: /save template/i })).toBeTruthy();
    expect(screen.queryByText("Only QA can change the site-wide checklist template.")).toBeNull();
  });

  it("lets Quality.Approve without Quality.Investigate save the site-wide template but not a section one", async () => {
    seedAuth("Qa", [PERMISSIONS.QUALITY_VIEW, PERMISSIONS.QUALITY_APPROVE]);
    const user = userEvent.setup();
    renderConfigPage();

    expect(await screen.findByRole("button", { name: /save template/i })).toBeTruthy();

    await user.click(screen.getByRole("combobox", { name: /scope/i }));
    await user.click(await screen.findByRole("option", { name: "Microbiology" }));

    await waitFor(() => expect(screen.queryByRole("button", { name: /save template/i })).toBeNull());
    expect(screen.getByText("Changing a section checklist template needs the Quality.Investigate permission.")).toBeTruthy();
  });

  it("gives a view-only user a read-only template with the reason", async () => {
    seedAuth("Analyst", [PERMISSIONS.QUALITY_VIEW]);
    renderConfigPage();

    expect(await screen.findByDisplayValue("Verify batch impact")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /save template/i })).toBeNull();
    expect(screen.getByText("Only QA can change the site-wide checklist template.")).toBeTruthy();
  });

  it("toggles section change-control gate with reason, signature dialog, wrong-password retry, and stale-version error", async () => {
    seedAuth("Qa", [PERMISSIONS.QUALITY_VIEW, PERMISSIONS.QUALITY_APPROVE]);
    const wrongPasswordErr = { response: { data: { message: "Invalid password." } } };
    const staleErr = { response: { status: 409, data: { message: "The change-control settings changed after they were loaded. Reload and try again." } } };
    vi.mocked(qualityConfigService.setSectionSetting)
      .mockRejectedValueOnce(wrongPasswordErr)
      .mockRejectedValueOnce(staleErr);

    const user = userEvent.setup();
    renderConfigPage();

    await user.click(await screen.findByRole("tab", { name: /section settings/i }));

    const gateSwitch = await screen.findByRole("switch", { name: /change-control gate/i });
    await user.click(gateSwitch);
    await user.type(screen.getByLabelText(/reason/i), "Enforce change control for Microbiology");
    await user.click(screen.getByRole("button", { name: /save gate setting/i }));

    // SignatureDialog opens with meaning statement
    expect(
      await screen.findByText("I am changing the change-control gate setting for this laboratory section.")
    ).toBeTruthy();
    await user.type(screen.getByLabelText(/password/i), "wrong-pass");
    await user.click(screen.getByRole("button", { name: /^sign$/i }));

    // Wrong password keeps dialog open and displays error
    expect(await screen.findByText("Invalid password.")).toBeTruthy();
    expect(screen.getByRole("button", { name: /^sign$/i })).toBeTruthy();

    // Retry with valid password triggers stale-version error from server
    await user.type(screen.getByLabelText(/password/i), "correct-pass");
    await user.click(screen.getByRole("button", { name: /^sign$/i }));

    await waitFor(() => expect(qualityConfigService.setSectionSetting).toHaveBeenCalledTimes(2));
    expect(qualityConfigService.setSectionSetting).toHaveBeenLastCalledWith(
      1,
      {
        changeControlRequired: true,
        reason: "Enforce change control for Microbiology",
        password: "correct-pass"
      },
      5
    );
    expect(await screen.findByText(/changed after they were loaded/i)).toBeTruthy();
  });
});
