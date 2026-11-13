import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { FindingDialog } from "./FindingDialog";
import type { AuditLogItem } from "../auditSearch/types/auditTypes";

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const entry: AuditLogItem = {
  id: 105,
  entityName: "Sample",
  entityId: "55",
  action: "Update",
  previousValue: null,
  newValue: null,
  userId: 2,
  userName: "Alice Analyst",
  userCurrentRole: "Analyst",
  userUsername: "alice",
  timestamp: "2026-10-02T12:30:00Z",
  batchNumber: null,
  controlNumber: null,
  sampleReferenceNumber: "SMP-55",
  mediaLotNumber: null,
  referenceStrainCode: null,
  cryovialCode: null,
  sampleId: 55,
  testOrderId: null,
  actionCategory: null,
  actorType: null,
  correlationId: null,
  reason: null,
  entityIdNegative: false,
  correctedByAuditLogId: null
};

describe("FindingDialog", () => {
  it("requires a 1-2000 character note and submits the trimmed value", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(
      <PinnedLightTheme>
        <FindingDialog open entry={entry} onClose={() => {}} onSubmit={onSubmit} />
      </PinnedLightTheme>
    );

    expect(screen.getByText(/#105/)).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Record finding" }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/Finding note is required/i)).toBeTruthy();

    await user.type(screen.getByLabelText(/Finding note/i), "  Missing reason for manual edit  ");
    await user.click(screen.getByRole("button", { name: "Record finding" }));

    expect(onSubmit).toHaveBeenCalledWith("Missing reason for manual edit");
  });
});
