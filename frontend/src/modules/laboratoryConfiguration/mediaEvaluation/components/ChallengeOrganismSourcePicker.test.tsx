import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { ChallengeOrganismSourcePicker } from "./ChallengeOrganismSourcePicker";
import { MediaEvaluationService } from "../services/MediaEvaluationService";
import type { MediaEvaluationChallenge, ThawedVialOption } from "../types/mediaEvaluationTypes";
import type { CryovialItem } from "../../cryovials/types/cryovialTypes";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";

vi.mock("../services/MediaEvaluationService", () => ({
  MediaEvaluationService: {
    getThawedVials: vi.fn(),
    selectThawedVial: vi.fn(),
    thawAndSelectVial: vi.fn(),
    selectLyophilizedDisk: vi.fn()
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

function renderPicker(onAssigned = vi.fn(), onError = vi.fn()) {
  render(
    <PinnedLightTheme>
      <ChallengeOrganismSourcePicker
        challenge={challenge}
        batches={[batch]}
        refreshKey={0}
        onAssigned={onAssigned}
        onError={onError}
      />
    </PinnedLightTheme>
  );
  return { onAssigned, onError };
}

async function choose(label: string, optionText: RegExp) {
  fireEvent.mouseDown(within((await screen.findByLabelText(label)).parentElement!).getByRole("combobox"));
  fireEvent.click(await screen.findByRole("option", { name: optionText }));
}

describe("ChallengeOrganismSourcePicker", () => {
  beforeEach(() => vi.clearAllMocks());
  afterEach(cleanup);

  it("tells the analyst no vial is thawed and lets them thaw and use one", async () => {
    service.getThawedVials.mockResolvedValue([]);
    service.thawAndSelectVial.mockResolvedValue({});
    const { onAssigned } = renderPicker();

    expect(await screen.findByText(/No thawed vial of E\. coli is available/)).toBeTruthy();

    await choose("Cryovial batch to thaw from", /ECO\/01\/26 \(4 of 5 vials left\)/);
    fireEvent.click(screen.getByRole("button", { name: "Thaw & use" }));

    await waitFor(() => expect(service.thawAndSelectVial).toHaveBeenCalledWith(7, 11, ""));
    expect(onAssigned).toHaveBeenCalled();
  });

  it("offers already-thawed vials, showing the lots each one served", async () => {
    service.getThawedVials.mockResolvedValue([thawed]);
    service.selectThawedVial.mockResolvedValue({});
    const { onAssigned } = renderPicker();

    await choose("Thawed vial or disk", /ECO\/01\/26, vial thawed .* by Jane Analyst \(used in TSA-0001\)/);
    fireEvent.click(screen.getByRole("button", { name: "Assign" }));

    await waitFor(() => expect(service.selectThawedVial).toHaveBeenCalledWith(7, 21));
    expect(onAssigned).toHaveBeenCalled();
    expect(screen.queryByText(/No thawed vial/)).toBeNull();
  });
});
