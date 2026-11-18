import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { AssignAnalystDialog } from "./AssignAnalystDialog";
import { ReceiveService } from "../services/ReceiveService";
import type { SampleAssignmentOptions, SampleRecord } from "../types/receivingTypes";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

vi.mock("../services/ReceiveService", () => ({
  ReceiveService: {
    getAssignmentOptions: vi.fn(),
    assignTestAnalysts: vi.fn()
  }
}));

const sample = {
  sampleId: 7,
  referenceNumber: "FP1026001",
  category: "FinishedProduct",
  displayName: "Example Tablet",
  batchNumber: "B-1",
  controlNumber: "C-1",
  assignedTests: [
    { testOrderId: 1, sectionId: 1, testCode: "TAMC", status: "Pending", locationCount: 0, assignedAnalystId: 3, assignedAnalystName: "Mona Ali" }
  ]
} as unknown as SampleRecord;

const options: SampleAssignmentOptions = {
  sampleId: 7,
  tests: [
    { testOrderId: 1, testCode: "TAMC", sectionId: 1, sectionName: "Microbiology", status: "Pending", assignedAnalystId: 3, assignedAnalystName: "Mona Ali", canAssign: true, eligibleUserIds: [3, 4] }
  ],
  users: [
    { id: 3, fullName: "Mona Ali", username: "mona", roleName: "Analyst", roleType: "Analyst" },
    { id: 4, fullName: "Omar Saleh", username: "omar", roleName: "Reviewer", roleType: "Reviewer" }
  ]
};

function httpError(status: number, message: string) {
  return new AxiosError("Request failed", String(status), undefined, undefined, {
    status,
    statusText: "",
    headers: {},
    config: { headers: new AxiosHeaders() },
    data: { message }
  });
}

async function chooseOmarAndSave(user: ReturnType<typeof userEvent.setup>) {
  await screen.findByRole("combobox", { name: "Analyst for TAMC" });
  await user.click(screen.getByRole("combobox", { name: "Analyst for TAMC" }));
  await user.click(within(await screen.findByRole("listbox")).getByRole("option", { name: "Omar Saleh (Reviewer)" }));
  await user.click(screen.getByRole("button", { name: "Save Assignment" }));
}

describe("AssignAnalystDialog - refused or failed save", () => {
  beforeEach(() => {
    vi.mocked(ReceiveService.getAssignmentOptions).mockResolvedValue(options);
  });
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("shows a permanent business refusal with Close only, no Retry", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    vi.mocked(ReceiveService.assignTestAnalysts).mockRejectedValue(httpError(400, "Tests with a result cannot be reassigned."));
    render(
      <PinnedLightTheme>
        <AssignAnalystDialog open sample={sample} onClose={onClose} onAssigned={vi.fn()} />
      </PinnedLightTheme>
    );
    await chooseOmarAndSave(user);

    const alert = await screen.findByRole("alert");
    expect(within(alert).getByText("Tests with a result cannot be reassigned.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Retry" })).toBeNull();
    await user.click(within(alert).getByRole("button", { name: "Close" }));
    expect(onClose).toHaveBeenCalled();
  });

  it("keeps Retry for a network failure and repeats the save", async () => {
    const user = userEvent.setup();
    vi.mocked(ReceiveService.assignTestAnalysts)
      .mockRejectedValueOnce(new AxiosError("Network Error", "ERR_NETWORK"))
      .mockResolvedValueOnce(sample);
    const onAssigned = vi.fn();
    render(
      <PinnedLightTheme>
        <AssignAnalystDialog open sample={sample} onClose={vi.fn()} onAssigned={onAssigned} />
      </PinnedLightTheme>
    );
    await chooseOmarAndSave(user);

    await user.click(await screen.findByRole("button", { name: "Retry" }));
    expect(ReceiveService.assignTestAnalysts).toHaveBeenCalledTimes(2);
    expect(onAssigned).toHaveBeenCalledWith(sample);
  });
});
