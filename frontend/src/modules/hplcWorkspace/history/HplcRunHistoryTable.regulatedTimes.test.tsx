import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { HplcRunListItem } from "../types";
import { HplcRunHistoryTable } from "./HplcRunHistoryTable";

afterEach(cleanup);

const run = {
  id: 1,
  code: "HPLC-0001",
  methodAbbreviation: "ASSAY",
  analystUserName: "Ali Analyst",
  startedAt: "2026-11-17T09:41:07Z",
  closedAt: "2026-11-17T11:05:07Z",
  status: "Closed",
  sstStatus: "Passed"
} as unknown as HplcRunListItem;

describe("HPLC run history shows time zones", () => {
  it("shows the zone beside the start and close time of a run", () => {
    render(
      <PinnedLightTheme>
        <MemoryRouter initialEntries={["/hplc"]}>
          <HplcRunHistoryTable historyRuns={[run]} equipmentId={1} technique="Hplc" onSelectRun={vi.fn()} />
        </MemoryRouter>
      </PinnedLightTheme>
    );
    const zoned = /^17 Nov 2026, \d{2}:\d{2} [A-Za-z][A-Za-z0-9+:-]*$/;
    expect(screen.getAllByText(zoned)).toHaveLength(2);
  });
});
