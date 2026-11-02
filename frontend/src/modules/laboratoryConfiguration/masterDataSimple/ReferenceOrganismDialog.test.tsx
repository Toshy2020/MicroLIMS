import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReferenceOrganismDialog } from "./ReferenceOrganismDialog";
import { ChangeReferenceOrganismCodeDialog } from "./ChangeReferenceOrganismCodeDialog";
import { ReferenceOrganismService, ReferenceOrganism } from "./services/ReferenceOrganismService";

vi.mock("../../../contexts/AuthContext", () => ({
  useHasPermission: () => true,
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));
vi.mock("./services/ReferenceOrganismService", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./services/ReferenceOrganismService")>()),
  ReferenceOrganismService: { create: vi.fn(), update: vi.fn(), changeCode: vi.fn() }
}));
vi.mock("../../../services/laboratorySectionService", () => ({
  getMySections: () => Promise.resolve([{ sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology" }])
}));

const species = [
  { id: 4, scientificName: "Escherichia coli", atccNumber: null, commonName: null, description: null },
  { id: 5, scientificName: "Bacillus subtilis", atccNumber: null, commonName: null, description: null }
];
const sections = [{ sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "QC" }];

const strain = (over: Partial<ReferenceOrganism> = {}): ReferenceOrganism => ({
  id: 11, version: 3, code: "ORG-001", organismId: 4, organismScientificName: "Escherichia coli", cultureCollection: "ATCC",
  strainNumber: "8739", name: "Escherichia coli ATCC 8739", ownerSectionId: 1, ownerSectionName: "Microbiology",
  isActive: true, canEdit: true, hasStock: false, ...over
});

beforeEach(() => {
  vi.mocked(ReferenceOrganismService.create).mockResolvedValue(strain());
  vi.mocked(ReferenceOrganismService.update).mockResolvedValue(strain());
  vi.mocked(ReferenceOrganismService.changeCode).mockResolvedValue(strain());
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const renderDialog = (s: ReferenceOrganism | null, onSaved = vi.fn()) =>
  render(
    <PinnedLightTheme>
      <ReferenceOrganismDialog open strain={s} species={species} sections={sections} onClose={vi.fn()} onSaved={onSaved} />
    </PinnedLightTheme>
  );

describe("ReferenceOrganismDialog", () => {
  it("hints the ORG- prefix and refuses a code without it", async () => {
    renderDialog(null);
    expect(screen.getByText(/Must start with "ORG-"/)).toBeTruthy();
    await userEvent.type(screen.getByLabelText(/^Code/), "XYZ-1");
    await userEvent.click(screen.getByRole("combobox", { name: /Species/ }));
    await userEvent.click(await screen.findByRole("option", { name: "Escherichia coli" }));
    await userEvent.click(screen.getByRole("button", { name: "Create Strain" }));

    expect(await screen.findByText('Code must start with "ORG-".')).toBeTruthy();
    expect(ReferenceOrganismService.create).not.toHaveBeenCalled();
  }, 20000);

  it("creates a strain from an existing species with the chosen collection", async () => {
    const onSaved = vi.fn();
    renderDialog(null, onSaved);
    await userEvent.type(screen.getByLabelText(/^Code/), "org-007");
    await userEvent.click(screen.getByRole("combobox", { name: /Species/ }));
    await userEvent.click(await screen.findByRole("option", { name: "Bacillus subtilis" }));
    await userEvent.type(screen.getByLabelText(/Strain number/), "6633");
    await userEvent.click(screen.getByRole("button", { name: "Create Strain" }));

    await waitFor(() => expect(ReferenceOrganismService.create).toHaveBeenCalledTimes(1));
    expect(vi.mocked(ReferenceOrganismService.create).mock.calls[0][0]).toMatchObject({
      code: "ORG-007", organismId: 5, cultureCollection: "ATCC", strainNumber: "6633"
    });
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
  }, 20000);

  it("freezes code, species, collection and strain number once the strain has stock", () => {
    renderDialog(strain({ hasStock: true }));
    expect((screen.getByLabelText(/^Code/) as HTMLInputElement).disabled).toBe(true);
    expect((screen.getByRole("combobox", { name: /Species/ }) as HTMLInputElement).disabled).toBe(true);
    expect((screen.getByLabelText(/Strain number/) as HTMLInputElement).disabled).toBe(true);
    expect(screen.getByRole("combobox", { name: /Culture collection/ }).getAttribute("aria-disabled")).toBe("true");
    expect(screen.getByText(/use Change code/)).toBeTruthy();
  });

  it("keeps the fields editable while the strain has no stock", () => {
    renderDialog(strain());
    expect((screen.getByLabelText(/^Code/) as HTMLInputElement).disabled).toBe(false);
    expect((screen.getByLabelText(/Strain number/) as HTMLInputElement).disabled).toBe(false);
  });
});

describe("ChangeReferenceOrganismCodeDialog", () => {
  const renderChange = (onSuccess = vi.fn()) =>
    render(
      <PinnedLightTheme>
        <ChangeReferenceOrganismCodeDialog open strain={strain({ hasStock: true })} onClose={vi.fn()} onSuccess={onSuccess} />
      </PinnedLightTheme>
    );

  it("needs a new code and a reason before it goes to signature", async () => {
    renderChange();
    const next = screen.getByRole("button", { name: "Continue to sign" }) as HTMLButtonElement;
    expect(next.disabled).toBe(true);
    await userEvent.type(screen.getByLabelText(/New Code/), "ORG-010");
    expect(next.disabled).toBe(true);
    await userEvent.type(screen.getByLabelText(/Reason/), "Renumbered after audit");
    expect(next.disabled).toBe(false);
  });

  it("sends the code change only after the e-signature", async () => {
    const onSuccess = vi.fn();
    renderChange(onSuccess);
    await userEvent.type(screen.getByLabelText(/New Code/), "org-010");
    await userEvent.type(screen.getByLabelText(/Reason/), "Renumbered after audit");
    await userEvent.click(screen.getByRole("button", { name: "Continue to sign" }));

    expect(ReferenceOrganismService.changeCode).not.toHaveBeenCalled();
    await userEvent.type(await screen.findByLabelText(/Password/), "secret");
    await userEvent.click(screen.getByRole("button", { name: /Sign/ }));

    await waitFor(() => expect(ReferenceOrganismService.changeCode).toHaveBeenCalledWith(11, "ORG-010", "Renumbered after audit", "secret"));
    await waitFor(() => expect(onSuccess).toHaveBeenCalled());
  }, 20000);
});
