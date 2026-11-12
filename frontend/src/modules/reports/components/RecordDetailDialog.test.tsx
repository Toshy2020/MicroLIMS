import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, describe, expect, it } from "vitest";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { ResultRecordItem } from "../types/reportingTypes";
import { RecordDetailDialog } from "./RecordDetailDialog";

afterEach(cleanup);

function record(reportedValue: string, unit: string | null): ResultRecordItem {
  return {
    id: 1, sampleId: 1, testOrderId: 1, sourceTable: "CountTestReading", sourceId: 1, round: 1,
    referenceNumber: "FP0926058", category: "FinishedProduct", subjectName: "Perfectil Hair Extra Tab", subjectDetail: null,
    batchNumber: "26031092", controlNumber: "I26090060", testCode: "TAMC", testDisplayName: "TAMC", resultKind: "Quantitative",
    numericValue: 140, reportedValue, unit, isBelowDetectionLimit: false, detectionLimit: null,
    alertLimit: null, actionLimit: null, specLimit: "1000", resultLevel: "WithinLimit",
    resultEnteredAt: "2026-09-15T08:40:36Z", resultEnteredByUserId: 1, resultEnteredByName: "Analyst",
    sampleStatus: "Approved", approvalStatus: "Approved", approvedByUserId: 2, approvedByName: "Head", approvedAt: "2026-09-15T09:32:29Z"
  };
}

function renderDialog(item: ResultRecordItem) {
  return render(
    <PinnedLightTheme><MemoryRouter><RecordDetailDialog open onClose={() => {}} record={item} /></MemoryRouter></PinnedLightTheme>
  );
}

describe("RecordDetailDialog reported result", () => {
  it("shows the unit once when the reported value already carries it", () => {
    renderDialog(record("140 cfu/g", "cfu/g"));

    expect(screen.getByText("140 cfu/g")).toBeTruthy();
    expect(screen.queryByText(/cfu\/g\s+cfu\/g/i)).toBeNull();
  });

  it("adds the unit when the reported value is a bare number", () => {
    renderDialog(record("98.5", "%"));

    expect(screen.getByText("98.5 %")).toBeTruthy();
  });
});
