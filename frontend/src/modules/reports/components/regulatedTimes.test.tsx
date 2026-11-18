import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { ResultRecordItem } from "../types/reportingTypes";
import { RecordDetailDialog } from "./RecordDetailDialog";

afterEach(cleanup);

const record = {
  id: 1,
  referenceNumber: "FP1126005",
  category: "FinishedProduct",
  subjectName: "Product",
  subjectDetail: null,
  testCode: "TAMC",
  testDisplayName: "TAMC",
  resultEnteredByName: "Ali Analyst",
  resultEnteredAt: "2026-11-17T09:41:07Z",
  approvedByName: "Sami Head",
  approvedAt: "2026-11-17T10:41:07Z",
  approvalStatus: "Approved"
} as unknown as ResultRecordItem;

describe("result record detail shows time zones", () => {
  it("shows the zone beside the result entry time and the approval time", () => {
    render(
      <PinnedLightTheme>
        <RecordDetailDialog open onClose={vi.fn()} record={record} />
      </PinnedLightTheme>
    );
    const zoned = /^17 Nov 2026, \d{2}:\d{2} [A-Za-z][A-Za-z0-9+:-]*$/;
    expect(screen.getAllByText(zoned)).toHaveLength(2);
  });
});
