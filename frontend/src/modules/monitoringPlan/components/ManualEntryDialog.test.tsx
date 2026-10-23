import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ManualEntryDialog } from "./ManualEntryDialog";
import { apiClient } from "../../../services/apiClient";
import { MonitoringPlanService } from "../services/MonitoringPlanService";

vi.mock("../../../services/apiClient", () => ({ apiClient: { get: vi.fn() } }));
vi.mock("../services/MonitoringPlanService", () => ({ MonitoringPlanService: { createManual: vi.fn() } }));

describe("ManualEntryDialog", () => {
  beforeEach(() => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: [{ id: 7, name: "Filler 1" }] } });
    vi.mocked(MonitoringPlanService.createManual).mockResolvedValue({} as never);
  });
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it("adds an occurrence for the machine itself, without asking for its parts", async () => {
    const onCreated = vi.fn();
    render(<ManualEntryDialog open onClose={() => {}} onCreated={onCreated} />);

    expect(screen.queryByText("Machine parts to test")).toBeNull();
    expect(screen.getByText(/whole machine/)).toBeTruthy();
    const add = screen.getByRole("button", { name: "Add" }) as HTMLButtonElement;
    expect(add.disabled).toBe(true);

    fireEvent.mouseDown(screen.getByLabelText("Machine"));
    fireEvent.click(await screen.findByRole("option", { name: "Filler 1" }));
    fireEvent.change(screen.getByLabelText("Target date"), { target: { value: "2026-10-28" } });
    expect(add.disabled).toBe(false);
    fireEvent.click(add);

    await waitFor(() => expect(MonitoringPlanService.createManual).toHaveBeenCalledTimes(1));
    expect(MonitoringPlanService.createManual).toHaveBeenCalledWith({
      machineId: 7, targetDate: "2026-10-28", kind: "Day", reason: undefined
    });
    await waitFor(() => expect(onCreated).toHaveBeenCalled());
  });
});
