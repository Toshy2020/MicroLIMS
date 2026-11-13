import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChallengeOrganismSourcePicker } from "./ChallengeOrganismSourcePicker";
import { MediaEvaluationService } from "../services/MediaEvaluationService";
import {
  labelService,
  type ScanMatch,
  type ScanResolveResponse
} from "../../../../services/labelService";
import type { MediaEvaluationChallenge, ThawedVialOption } from "../types/mediaEvaluationTypes";
import type { CryovialItem } from "../../cryovials/types/cryovialTypes";
import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";

vi.mock("../services/MediaEvaluationService", () => ({
  MediaEvaluationService: {
    getThawedVials: vi.fn(),
    selectThawedVial: vi.fn(),
    thawAndSelectVial: vi.fn(),
    selectLyophilizedDisk: vi.fn()
  }
}));

vi.mock("../../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

const service = vi.mocked(MediaEvaluationService);

const challenge = {
  id: 7,
  mediaEvaluationId: 3,
  organismId: 1,
  organism: { id: 1, scientificName: "E. coli", atccNumber: null, commonName: null, description: null },
  cryovialId: null,
  cryovial: null,
  thawEventId: null,
  thawEvent: null,
  lyophilizedDiskId: null,
  lyophilizedDisk: null
} as unknown as MediaEvaluationChallenge;

const batch = { id: 11, code: "ECO/01/26", vialsRemaining: 4, numberOfVialsPrepared: 5 } as CryovialItem;

const disk = {
  id: 31,
  sectionId: 1,
  materialType: "LyophilizedMicroorganism",
  customType: null,
  materialName: "E. coli Disk",
  manufacturerName: "Microbiologics",
  batchNumber: "LD-2026-01",
  receivingDate: "2026-01-01",
  expiryDate: "2027-12-31",
  code: "DISK-01",
  location: "Fridge 1",
  mediaProductId: null,
  organismId: 1,
  atccNumber: null,
  quantityReceived: 10,
  quantityRemaining: 8,
  unit: "Disc",
  minimumStockLevel: 1,
  status: "InStock"
} as MaterialItem;

const thawed: ThawedVialOption = {
  thawEventId: 21,
  cryovialId: 11,
  cryovialCode: "ECO/01/26",
  thawedAt: "2026-10-15T08:00:00Z",
  thawedByUserId: 1,
  thawedByName: "Jane Analyst",
  notes: null,
  usedInMediaLots: ["TSA-0001"]
};

function makeResolveResponse(match: Partial<ScanMatch>): ScanResolveResponse {
  return {
    outcome: "Found",
    layoutCorrected: false,
    normalizedValue: match.code ?? "",
    match: {
      entityType: "Cryovial",
      id: 11,
      code: "ECO/01/26",
      displayName: "E. coli",
      status: "Approved",
      usable: true,
      blockedReason: null,
      navigationTarget: "/cryovials/11",
      linkedEquipmentId: null,
      ...match
    },
    candidates: [],
    hint: null
  };
}

function renderPicker(disks: MaterialItem[] = [], onAssigned = vi.fn(), onError = vi.fn()) {
  render(
    <PinnedLightTheme>
      <ChallengeOrganismSourcePicker
        challenge={challenge}
        batches={[batch]}
        disks={disks}
        refreshKey={0}
        onAssigned={onAssigned}
        onError={onError}
      />
    </PinnedLightTheme>
  );
  return { onAssigned, onError };
}

async function choose(user: ReturnType<typeof userEvent.setup>, label: string, optionText: RegExp) {
  await user.click(within((await screen.findByLabelText(label)).parentElement!).getByRole("combobox"));
  await user.click(await screen.findByRole("option", { name: optionText }));
}

describe("ChallengeOrganismSourcePicker", () => {
  beforeEach(() => vi.clearAllMocks());
  afterEach(cleanup);

  it("tells the analyst no vial is thawed and lets them thaw and use one", async () => {
    const user = userEvent.setup();
    service.getThawedVials.mockResolvedValue([]);
    service.thawAndSelectVial.mockResolvedValue({});
    const { onAssigned } = renderPicker();

    expect(await screen.findByText(/No vial of E\. coli thawed in the last 24 hours is available/)).toBeTruthy();

    await choose(user, "Cryovial batch to thaw from", /ECO\/01\/26 \(4 of 5 vials left\)/);
    await user.click(screen.getByRole("button", { name: "Thaw & use" }));

    await waitFor(() => expect(service.thawAndSelectVial).toHaveBeenCalledWith(7, 11, ""));
    expect(onAssigned).toHaveBeenCalled();
  });

  it("offers already-thawed vials, showing the lots each one served", async () => {
    const user = userEvent.setup();
    service.getThawedVials.mockResolvedValue([thawed]);
    service.selectThawedVial.mockResolvedValue({});
    const { onAssigned } = renderPicker();

    await choose(user, "Thawed vial or disk", /ECO\/01\/26, vial thawed .* by Jane Analyst \(used in TSA-0001\)/);
    await user.click(screen.getByRole("button", { name: "Assign" }));

    await waitFor(() => expect(service.selectThawedVial).toHaveBeenCalledWith(7, 21));
    expect(onAssigned).toHaveBeenCalled();
    expect(screen.queryByText(/thawed in the last 24 hours/)).toBeNull();
  });

  it("scanning an offered cryovial batch selects it", async () => {
    const user = userEvent.setup();
    service.getThawedVials.mockResolvedValue([]);
    service.thawAndSelectVial.mockResolvedValue({});
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ entityType: "Cryovial", id: 11, code: "ECO/01/26" })
    );

    renderPicker();
    const scanInput = await screen.findByLabelText("Scan cryovial batch");
    await user.type(scanInput, "CRY:ECO/01/26{Enter}");

    await waitFor(() => {
      const combobox = within((screen.getByLabelText("Cryovial batch to thaw from")).parentElement!).getByRole("combobox");
      expect(combobox.textContent).toContain("ECO/01/26");
    });
    expect(screen.getByRole("status").textContent).toContain("Selected ECO/01/26");

    await user.click(screen.getByRole("button", { name: "Thaw & use" }));
    await waitFor(() => expect(service.thawAndSelectVial).toHaveBeenCalledWith(7, 11, ""));
  });

  it("scanning a cryovial batch that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    service.getThawedVials.mockResolvedValue([]);
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ entityType: "Cryovial", id: 99, code: "ECO/99/26" })
    );

    renderPicker();
    const scanInput = await screen.findByLabelText("Scan cryovial batch");
    await user.type(scanInput, "CRY:ECO/99/26{Enter}");

    const refusal = await screen.findByText(/ECO\/99\/26 is not available for this step\./);
    expect(refusal.closest('[role="alert"]')).toBeTruthy();
    const combobox = within((screen.getByLabelText("Cryovial batch to thaw from")).parentElement!).getByRole("combobox");
    expect(combobox.textContent).toContain("Cryovial batch");
    expect((screen.getByRole("button", { name: "Thaw & use" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("scanning an offered lyophilized disk selects it", async () => {
    const user = userEvent.setup();
    service.getThawedVials.mockResolvedValue([]);
    service.selectLyophilizedDisk.mockResolvedValue({});
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ entityType: "MaterialStockLot", id: 31, code: "DISK-01", displayName: "E. coli Disk" })
    );

    renderPicker([disk]);
    const scanInput = await screen.findByLabelText("Scan lyophilized disk");
    await user.type(scanInput, "MAT:DISK-01{Enter}");

    await waitFor(() => {
      const combobox = within((screen.getByLabelText("Thawed vial or disk")).parentElement!).getByRole("combobox");
      expect(combobox.textContent).toContain("E. coli Disk");
    });
    expect(screen.getByRole("status").textContent).toContain("Selected DISK-01");

    await user.click(screen.getByRole("button", { name: "Assign" }));
    await waitFor(() => expect(service.selectLyophilizedDisk).toHaveBeenCalledWith(7, 31));
  });

  it("scanning a lyophilized disk that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    service.getThawedVials.mockResolvedValue([]);
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ entityType: "MaterialStockLot", id: 999, code: "DISK-999", displayName: "Other Disk" })
    );

    renderPicker([disk]);
    const scanInput = await screen.findByLabelText("Scan lyophilized disk");
    await user.type(scanInput, "MAT:DISK-999{Enter}");

    const refusal = await screen.findByText(/DISK-999 is not available for this step\./);
    expect(refusal.closest('[role="alert"]')).toBeTruthy();
    const combobox = within((screen.getByLabelText("Thawed vial or disk")).parentElement!).getByRole("combobox");
    expect(combobox.textContent).toContain("Select thawed vial or disk");
    expect((screen.getByRole("button", { name: "Assign" }) as HTMLButtonElement).disabled).toBe(true);
  });
});
