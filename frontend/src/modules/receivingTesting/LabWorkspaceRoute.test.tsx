import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";

const getSample = vi.fn();
vi.mock("../receiving/services/ReceiveService", () => ({ ReceiveService: { getSample: (...a: unknown[]) => getSample(...a) } }));
vi.mock("../../hooks/useMyLabs", () => ({
  useMyLabs: () => ({ codes: ["FP"], physchemAreas: ["fp", "rmpm"], loading: false })
}));
vi.mock("../../services/laboratorySectionService", () => ({
  laboratorySectionService: { getSections: () => Promise.resolve([{ sectionId: 7, sectionCode: "FP", sectionName: "Physchem" }]) }
}));
vi.mock("./ReceivingTestingWorkspacePage", () => ({
  ReceivingTestingWorkspacePage: ({ lab }: { lab: { area?: string } }) => <div>workspace-{lab.area}</div>
}));

import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { LabWorkspaceRoute } from "./LabWorkspaceRoute";

function renderAt(url: string) {
  // The app theme: the loading skeleton reads its table-head colours.
  return render(
    <ThemeProvider theme={lightTheme}>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path="/physicochemical/workspace" element={<LabWorkspaceRoute code="FP" area="fp" />} />
          <Route path="/physicochemical/rm-pm-workspace" element={<LabWorkspaceRoute code="FP" area="rmpm" />} />
        </Routes>
      </MemoryRouter>
    </ThemeProvider>
  );
}

afterEach(() => {
  cleanup();
  getSample.mockReset();
});

describe("LabWorkspaceRoute per-sample deep link", () => {
  it("redirects to the other area when the sample is not found in this one", async () => {
    getSample.mockImplementation((_id: number, _s: number, area: string) =>
      area === "fp" ? Promise.reject({ response: { status: 404 } }) : Promise.resolve({}));
    renderAt("/physicochemical/workspace?sampleId=12");
    expect(await screen.findByText("workspace-rmpm")).toBeTruthy();
  });

  it("stays when the sample loads, and does not redirect on other errors", async () => {
    getSample.mockResolvedValue({});
    renderAt("/physicochemical/workspace?sampleId=12");
    expect(await screen.findByText("workspace-fp")).toBeTruthy();
    cleanup();
    getSample.mockRejectedValue({ response: { status: 500 } });
    renderAt("/physicochemical/workspace?sampleId=12");
    await waitFor(() => expect(screen.getByText("workspace-fp")).toBeTruthy());
  });

  it("does not bounce back when the sample is missing in both areas", async () => {
    getSample.mockRejectedValue({ response: { status: 404 } });
    renderAt("/physicochemical/workspace?sampleId=12");
    expect(await screen.findByText("workspace-rmpm")).toBeTruthy();
    expect(getSample).toHaveBeenCalledTimes(1);
  });
});
