import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, fireEvent, within } from "@testing-library/react";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { IcpSpecFields } from "./IcpSpecFields";

afterEach(cleanup);

const assayElements = [
  { id: 1, symbol: "Zn", targetPpm: 10 },
  { id: 2, symbol: "Fe", targetPpm: 5.5 }
];
const impurityElements = [
  { id: 3, symbol: "Pb" },
  { id: 4, symbol: "As" }
];

const renderFields = (props: Partial<React.ComponentProps<typeof IcpSpecFields>> = {}) =>
  render(
    <PinnedLightTheme>
      <IcpSpecFields
        mode="Assay"
        elements={assayElements}
        selectedElementId=""
        onElementChange={vi.fn()}
        {...props}
      />
    </PinnedLightTheme>
  );

describe("IcpSpecFields", () => {
  it("titles an assay specification and explains the limits apply to the assay %", () => {
    renderFields();
    expect(screen.getByText("Assay (ICP) Specification")).toBeTruthy();
    expect(screen.getByText(/assay % of the test's target concentration/i)).toBeTruthy();
  });

  it("titles an impurity specification and explains 0 means not detected", () => {
    renderFields({ mode: "Impurity", elements: impurityElements });
    expect(screen.getByText("Elemental Impurity (ICP) Specification")).toBeTruthy();
    expect(screen.getByText(/0 means not detected/i)).toBeTruthy();
  });

  it("lists the test's elements with their targets for an assay and reports the choice", () => {
    const onElementChange = vi.fn();
    renderFields({ onElementChange });

    fireEvent.mouseDown(screen.getByRole("combobox"));
    const list = within(screen.getByRole("listbox"));
    expect(list.getByText("Zn (target 10 ppm)")).toBeTruthy();
    fireEvent.click(list.getByText("Fe (target 5.5 ppm)"));

    expect(onElementChange).toHaveBeenCalledWith(2, assayElements[1]);
  });

  it("lists bare symbols for an impurity test (no target)", () => {
    renderFields({ mode: "Impurity", elements: impurityElements });
    fireEvent.mouseDown(screen.getByRole("combobox"));
    const list = within(screen.getByRole("listbox"));
    expect(list.getByText("Pb")).toBeTruthy();
    expect(list.queryByText(/target/)).toBeNull();
  });

  it("says so when the test has no elements", () => {
    renderFields({ elements: [] });
    fireEvent.mouseDown(screen.getByRole("combobox"));
    expect(screen.getByText("No elements configured on this test")).toBeTruthy();
  });

  it("shows the selected element", () => {
    renderFields({ selectedElementId: 1 });
    expect(screen.getByRole("combobox").textContent).toContain("Zn");
  });
});
