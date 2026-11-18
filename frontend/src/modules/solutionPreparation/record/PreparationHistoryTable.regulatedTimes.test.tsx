import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { ThemeProvider } from "@mui/material/styles";
import { lightTheme } from "../../../theme";
import type { SolutionPreparationStatusHistoryResponse } from "../types";
import { PreparationHistoryTable } from "./PreparationHistoryTable";

afterEach(cleanup);

const history = [
  {
    fromStatus: null,
    toStatus: "Released",
    changedByUserId: 4,
    changedByUserName: "Rana Reviewer",
    changedAt: "2026-11-17T09:41:07Z",
    reason: "Passed QC"
  }
] as unknown as SolutionPreparationStatusHistoryResponse[];

describe("preparation status history shows time zones", () => {
  it("shows the zone beside the time of a status change", () => {
    render(
      <ThemeProvider theme={lightTheme}>
        <PreparationHistoryTable history={history} />
      </ThemeProvider>
    );
    expect(screen.getByText(/^17 Nov 2026, \d{2}:\d{2} [A-Za-z][A-Za-z0-9+:-]*$/)).toBeTruthy();
  });
});
