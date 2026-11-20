import { act, cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { afterEach, expect, it, vi } from "vitest";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { LiveProvider } from "../../../../contexts/LiveContext";
import { createFakeLiveConnection } from "../../../../contexts/testing/fakeLiveConnection";
import { signInForTest } from "../../../../services/drafts/draftTestUtils";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { WaterConfigService } from "../services/WaterConfigService";
import type { SamplingPoint } from "../waterConfigTypes";
import { WaterLocationPanel } from "./WaterLocationPanel";

vi.mock("../services/WaterConfigService", () => ({
  WaterConfigService: { getSamplingPoints: vi.fn() }
}));
vi.mock("../../../../services/masterDataOptions", () => ({
  masterDataOptions: { getTestDefinitions: vi.fn() }
}));

afterEach(() => {
  cleanup();
  localStorage.clear();
});

it("reloads a changed lab override without resetting edits to the water location", async () => {
  const user = userEvent.setup();
  const connection = createFakeLiveConnection();
  signInForTest(7);
  const override = { id: 41, sectionId: 1, sectionCode: "MICRO", frequencyType: "Daily", frequencyInterval: 1, description: "Original schedule" } as const;
  const point: SamplingPoint = {
    id: 10, code: "PW-01", location: "Loop A", waterDepartmentId: 1, testingFrequency: "Daily",
    assignedTestCodes: ["TAMC-Water"], labFrequencies: [override]
  };
  vi.mocked(masterDataOptions.getTestDefinitions).mockResolvedValue([{
    id: 1, code: "TAMC-Water", displayName: "TAMC for Water", isActive: true, workflowType: "CountTest",
    section: { id: 1, name: "Microbiology", code: "MICRO" }
  }]);
  vi.mocked(WaterConfigService.getSamplingPoints).mockResolvedValue([{
    ...point, labFrequencies: [{ ...override, frequencyInterval: 2, description: "Updated schedule" }]
  }]);

  render(
    <PinnedLightTheme>
      <AuthProvider>
        <LiveProvider connection={connection}>
          <MemoryRouter>
            <WaterLocationPanel
              open point={point} defaultDepartmentId={1} departments={[{ id: 1, name: "Purified Water", samplingPoints: [] }]}
              configs={[]} isCountTest={() => true} onClose={vi.fn()} onSaved={vi.fn()}
            />
          </MemoryRouter>
        </LiveProvider>
      </AuthProvider>
    </PinnedLightTheme>
  );

  const row = within(await screen.findByTestId("lab-freq-row-MICRO"));
  const location = screen.getByDisplayValue<HTMLInputElement>("Loop A");
  await user.type(location, " Updated locally");
  await user.click(row.getByRole("button", { name: "Edit" }));
  await waitFor(() => expect(connection.invoke).toHaveBeenCalledWith("WatchRecord", "waterPointLabFrequency", 41));

  act(() => connection.emitSignal({ kind: "record", key: "waterPointLabFrequency:41", actorUserId: 99 }));
  await user.click(await row.findByRole("button", { name: "Reload" }));
  expect(WaterConfigService.getSamplingPoints).not.toHaveBeenCalled();
  await user.click(await screen.findByRole("button", { name: "Discard and reload" }));

  expect(await row.findByText("Updated schedule")).toBeTruthy();
  expect(location.value).toBe("Loop A Updated locally");
});
