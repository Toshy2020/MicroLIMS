import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { IcpReportAutofill } from "./IcpReportAutofill";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import type { IcpReportValuesDto } from "../types";

vi.mock("../services/IcpWorkspaceService", () => ({ IcpWorkspaceService: { getReportValues: vi.fn() } }));

const dto = (over: Partial<IcpReportValuesDto> = {}): IcpReportValuesDto => ({
  evidenceId: 1,
  fileName: "s.pdf",
  source: "TextLayer",
  requiresVerification: false,
  problems: [],
  solutions: [{ index: 3, label: "FP-7777 Top", page: 1, matchesSample: true, elements: "Zn, Ca" }],
  matchedIndex: 3,
  selectedIndex: 3,
  elements: [
    { icpTestElementId: 1, symbol: "Zn", resultPpm: 9.5, wavelengthNm: 213.857 },
    { icpTestElementId: 2, symbol: "Ca", resultPpm: null, note: "No reading for this element in the chosen solution." }
  ],
  ...over
});

describe("IcpReportAutofill", () => {
  beforeEach(() => vi.clearAllMocks());
  afterEach(cleanup);

  it("is disabled until a report is uploaded", () => {
    render(<IcpReportAutofill runSampleId={5} hasReport={false} onUse={vi.fn()} />);
    expect(screen.getByRole("button", { name: /read values/i })).toHaveProperty("disabled", true);
  });

  it("shows the proposal and only hands values over when the analyst accepts", async () => {
    vi.mocked(IcpWorkspaceService.getReportValues).mockResolvedValue(dto());
    const onUse = vi.fn();
    render(<IcpReportAutofill runSampleId={5} hasReport onUse={onUse} />);
    await userEvent.click(screen.getByRole("button", { name: /read values/i }));
    expect(await screen.findByText(/9.5 ppm/)).toBeTruthy();
    expect(screen.getByText(/No reading for this element/)).toBeTruthy();
    expect(onUse).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole("button", { name: /use these values/i }));
    expect(onUse).toHaveBeenCalledWith(dto().elements);
  });

  it("asks the analyst to choose when no solution is selected, and warns when verification is needed", async () => {
    vi.mocked(IcpWorkspaceService.getReportValues).mockResolvedValue(
      dto({ selectedIndex: null, requiresVerification: true, problems: ["2 solutions in the report match sample FP-7777 - choose the one to use."] })
    );
    render(<IcpReportAutofill runSampleId={5} hasReport onUse={vi.fn()} />);
    await userEvent.click(screen.getByRole("button", { name: /read values/i }));
    expect(await screen.findByText(/choose the one to use/)).toBeTruthy();
    expect(screen.getByText(/read with some uncertainty/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: /use these values/i })).toBeNull();
  });

  it("shows the server's message when the report cannot be read", async () => {
    vi.mocked(IcpWorkspaceService.getReportValues).mockRejectedValue({ response: { data: { message: "Upload the sample result report first." } } });
    render(<IcpReportAutofill runSampleId={5} hasReport onUse={vi.fn()} />);
    await userEvent.click(screen.getByRole("button", { name: /read values/i }));
    await waitFor(() => expect(screen.getByText("Upload the sample result report first.")).toBeTruthy());
  });
});
