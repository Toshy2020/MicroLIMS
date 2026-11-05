import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { TestPreparationForm } from "./TestPreparationForm";

const svc = vi.hoisted(() => ({ prepare: vi.fn(), getLotOptionsForMaster: vi.fn() }));
vi.mock("./services/SamplePreparationService", () => ({ SamplePreparationService: svc }));
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => ({ userId: 1 }) }));
vi.mock("../../components/SignatureDialog", () => ({
  SignatureDialog: ({ open, onConfirm }: any) =>
    open ? <button onClick={() => onConfirm("pw")}>sign</button> : null
}));
vi.mock("../../services/masterDataOptions", () => ({
  masterDataOptions: { getMediaProducts: vi.fn().mockResolvedValue([{ id: 5, name: "TSB", code: "TSB", isActive: true }]) }
}));
vi.mock("../laboratoryConfiguration/masterDataSimple/services/SolutionMasterService", () => ({
  SolutionMasterService: { getAll: vi.fn().mockResolvedValue([{ id: 11, name: "Saline", type: "Diluent", isActive: true }]) }
}));
vi.mock("../laboratoryConfiguration/masterDataSimple/services/MaterialMasterService", () => ({
  MaterialMasterService: { getAll: vi.fn().mockResolvedValue([]) }
}));

const lotGroup = (source: string, id: number, code: string) => ({
  source, name: "x", lotRequired: true,
  options: [{ id, code, expiresAt: null, usable: true, reason: null }]
});

beforeEach(() => {
  svc.prepare.mockReset().mockResolvedValue({});
  svc.getLotOptionsForMaster.mockReset().mockImplementation((source: string, id: number) =>
    Promise.resolve(source === "MediaProduct" ? lotGroup(source, 301, "MD-301") : lotGroup(source, 302, `SP-${id}`)));
});
afterEach(cleanup);

async function pick(name: RegExp, option: RegExp) {
  await waitFor(() => expect(screen.getByRole("combobox", { name }).getAttribute("aria-disabled")).not.toBe("true"));
  fireEvent.mouseDown(screen.getByRole("combobox", { name }));
  fireEvent.click(await screen.findByRole("option", { name: option }));
}

describe("TestPreparationForm manual first entry", () => {
  it("picks source, master and lot for the diluent; the neutralizer contained in the medium needs no lot", async () => {
    render(<PinnedLightTheme><TestPreparationForm sample={{ sampleId: 42, category: "RawMaterial" }} onSaved={vi.fn()} /></PinnedLightTheme>);
    fireEvent.change(screen.getByLabelText(/Sample Amount/), { target: { value: "10" } });

    await pick(/Diluent source/, /Solution recipe/);
    await pick(/^Diluent$/, /Saline/);
    await waitFor(() => expect(svc.getLotOptionsForMaster).toHaveBeenCalledWith("SolutionRecipe", 11));
    await pick(/Neutralizer source/, /Contained in the medium/);

    const start = screen.getByRole("button", { name: "Start Testing" });
    expect((start as HTMLButtonElement).disabled).toBe(true);
    await pick(/Diluent lot/, /SP-11/);
    expect((start as HTMLButtonElement).disabled).toBe(false);

    fireEvent.click(start);
    fireEvent.click(await screen.findByText("sign"));
    await waitFor(() => expect(svc.prepare).toHaveBeenCalled());
    const payload = svc.prepare.mock.calls[0][0];
    expect(payload).toMatchObject({
      sampleId: 42, amount: 10, password: "pw",
      diluentSource: "SolutionRecipe", diluentSolutionMasterId: 11, diluentMediaProductId: null, diluentMaterialMasterEntryId: null,
      diluentLotId: 302,
      neutralizerSource: "ContainedInMedium", neutralizerLotId: null
    });
    expect(payload).not.toHaveProperty("diluent");
    expect(payload).not.toHaveProperty("neutralizer");
  });

  it("sends a media product diluent with its lot", async () => {
    render(<PinnedLightTheme><TestPreparationForm sample={{ sampleId: 42, category: "RawMaterial" }} onSaved={vi.fn()} /></PinnedLightTheme>);
    fireEvent.change(screen.getByLabelText(/Sample Amount/), { target: { value: "5" } });
    await pick(/Diluent source/, /Media product/);
    await pick(/^Diluent$/, /TSB/);
    await pick(/Diluent lot/, /MD-301/);
    await pick(/Neutralizer source/, /Contained in the medium/);
    fireEvent.click(screen.getByRole("button", { name: "Start Testing" }));
    fireEvent.click(await screen.findByText("sign"));
    await waitFor(() => expect(svc.prepare).toHaveBeenCalled());
    expect(svc.prepare.mock.calls[0][0]).toMatchObject({
      diluentSource: "MediaProduct", diluentMediaProductId: 5, diluentSolutionMasterId: null, diluentLotId: 301
    });
  });
});
