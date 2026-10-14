import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { workload, workloadRow } from "../kpiFixtures";
import type { AnalystWorkload } from "../types/workloadTypes";
import { WorkloadTable } from "./WorkloadTable";

afterEach(cleanup);

function renderTable(data: AnalystWorkload | null, over: { loading?: boolean; error?: string | null; onOpenTests?: (id: number) => void } = {}) {
  const onOpenTests = over.onOpenTests ?? vi.fn();
  return {
    onOpenTests,
    ...render(
      <PinnedLightTheme>
        <WorkloadTable data={data} loading={over.loading ?? false} error={over.error ?? null} onOpenTests={onOpenTests} />
      </PinnedLightTheme>
    )
  };
}

const names = () => screen.getAllByRole("button", { name: /^Show tests for/ }).map((b) => b.textContent);

// The API sends these ordered by delivered units, highest first.
const byOutput = workload([
  workloadRow({ userId: 1, fullName: "Amy Analyst", deliveredUnits: 30 }),
  workloadRow({ userId: 3, fullName: "Zed Analyst", deliveredUnits: 20 }),
  workloadRow({ userId: 2, fullName: "Bob Analyst", deliveredUnits: 5 })
]);

describe("WorkloadTable", () => {
  it("lists analysts alphabetically even when the API sends them by output", () => {
    renderTable(byOutput);
    expect(names()).toEqual(["Amy Analyst", "Bob Analyst", "Zed Analyst"]);
  });

  it("sorts on an explicit header click, then descending, then back to alphabetical", async () => {
    renderTable(byOutput);
    const header = () => screen.getByRole("button", { name: /^Delivered units/ });
    await userEvent.click(header());
    expect(names()).toEqual(["Bob Analyst", "Zed Analyst", "Amy Analyst"]);
    await userEvent.click(header());
    expect(names()).toEqual(["Amy Analyst", "Zed Analyst", "Bob Analyst"]);
    await userEvent.click(header());
    expect(names()).toEqual(["Amy Analyst", "Bob Analyst", "Zed Analyst"]);
  });

  it("does not remember an explicit sort", async () => {
    const first = renderTable(byOutput);
    await userEvent.click(screen.getByRole("button", { name: /^Delivered units/ }));
    expect(names()[0]).toBe("Bob Analyst");
    first.unmount();
    renderTable(byOutput);
    expect(names()).toEqual(["Amy Analyst", "Bob Analyst", "Zed Analyst"]);
  });

  it("puts rows with a missing value last whichever way it sorts", async () => {
    renderTable(workload([
      workloadRow({ userId: 1, fullName: "Amy Analyst", utilisation: null }),
      workloadRow({ userId: 2, fullName: "Bob Analyst", utilisation: 0.5 })
    ]));
    await userEvent.click(screen.getByRole("button", { name: /^Utilisation/ }));
    expect(names()).toEqual(["Bob Analyst", "Amy Analyst"]);
    await userEvent.click(screen.getByRole("button", { name: /^Utilisation/ }));
    expect(names()).toEqual(["Bob Analyst", "Amy Analyst"]);
  });

  it("says so when only the viewer's own row is returned", () => {
    renderTable(workload([workloadRow()], { onlyOwnRow: true }));
    expect(screen.getByText("Showing your own numbers only.")).toBeTruthy();
    expect(names()).toEqual(["Amy Analyst"]);
  });

  it("says nothing about own numbers when the team is returned", () => {
    renderTable(byOutput);
    expect(screen.queryByText("Showing your own numbers only.")).toBeNull();
  });

  it("shows the capacity-defaults banner", () => {
    renderTable(workload([workloadRow()], { capacityUsesDefaults: true }));
    expect(screen.getByText("Capacity assumes full availability and the default hours.")).toBeTruthy();
  });

  it("flags forward load over capacity and utilisation to check, only on the rows that need it", () => {
    renderTable(workload([
      workloadRow({ userId: 1, fullName: "Amy Analyst", forwardLoad: 1.4, forwardLoadOverCapacity: true, utilisation: 1.2, utilisationCheckProfile: true }),
      workloadRow({ userId: 2, fullName: "Bob Analyst" })
    ]));
    expect(screen.getAllByText("Over capacity").length).toBe(1);
    expect(screen.getAllByText("check profile").length).toBe(1);
    expect(screen.getByText("140%")).toBeTruthy();
  });

  it("shows a dash, not 0%, for a rate with no basis", () => {
    renderTable(workload([workloadRow({ utilisation: null, weightedOnTime: null, weightedRightFirstTime: null, forwardLoad: null })]));
    expect(screen.getAllByText("—").length).toBe(4);
    expect(screen.queryByText("0%")).toBeNull();
  });

  it("shows gap chips for unweighted and default-target tests and for unassigned open tests", () => {
    renderTable(workload(
      [workloadRow({ unweightedTests: 2, unweightedOpenTests: 1, testsOnDefaultTarget: 3 })],
      { unassignedOpenTests: 4, gaps: [{ code: "x", message: "5 tests have no profile", count: 5 }] }
    ));
    expect(screen.getByText("2 delivered tests unweighted")).toBeTruthy();
    expect(screen.getByText("1 open tests unweighted")).toBeTruthy();
    expect(screen.getByText("3 tests on the default target")).toBeTruthy();
    expect(screen.getByText("4 open tests have no analyst")).toBeTruthy();
    expect(screen.getByText("5 tests have no profile")).toBeTruthy();
  });

  it("opens the tests behind a row", async () => {
    const { onOpenTests } = renderTable(byOutput);
    await userEvent.click(screen.getByRole("button", { name: "Show tests for Zed Analyst" }));
    expect(onOpenTests).toHaveBeenCalledWith(3);
  });

  it("states that units are a planning estimate, not a score", () => {
    renderTable(byOutput);
    expect(screen.getByText(/planning estimate,\s+not a performance score/)).toBeTruthy();
  });

  it("shows loading, error and empty states", () => {
    const loading = renderTable(null, { loading: true });
    expect(screen.getByRole("progressbar")).toBeTruthy();
    loading.unmount();
    const failed = renderTable(null, { error: "Not permitted" });
    expect(screen.getByText("Not permitted")).toBeTruthy();
    failed.unmount();
    renderTable(workload([]));
    expect(screen.getByText("No analysts to show for this scope.")).toBeTruthy();
  });

  it("renders nothing when there is no data, no error and no request in flight", () => {
    const { container } = renderTable(null);
    expect(container.textContent).toBe("");
  });
});
