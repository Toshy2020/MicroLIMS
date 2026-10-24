import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ReportBuilderDrawer, errorMessage } from "./ReportBuilderDrawer";
import { DEFAULT_SETTINGS, ReportItem } from "../../types/controlChartTypes";

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const item = (id: string, subjectName: string): ReportItem => ({
  id, title: `EM TAMC - ${subjectName}`, chartKind: "imr", chartLabel: "I-MR chart", subjectName, testDisplayName: "EM TAMC",
  criteria: {
    category: "EnvironmentalMonitoring", testCode: "EM_TAMC", subjectName, fromDate: "2026-01-01", toDate: "2026-12-31",
    calculationFrom: "", calculationTo: "", approvedOnly: false, capabilityBasis: "SpecLimit", customLsl: "", customUsl: ""
  },
  settings: DEFAULT_SETTINGS,
  image: "data:image/png;base64,AAAA"
});

describe("ReportBuilderDrawer", () => {
  it("reorders and removes queued charts", () => {
    const onChange = vi.fn();
    render(<ReportBuilderDrawer open items={[item("a", "Filling Room"), item("b", "Weighing Room")]}
      onClose={() => {}} onChange={onChange} onGenerate={vi.fn()} />);

    fireEvent.click(screen.getAllByRole("button", { name: "Move down" })[0]);
    expect(onChange).toHaveBeenLastCalledWith([expect.objectContaining({ id: "b" }), expect.objectContaining({ id: "a" })]);

    fireEvent.click(screen.getAllByRole("button", { name: "Remove" })[1]);
    expect(onChange).toHaveBeenLastCalledWith([expect.objectContaining({ id: "a" })]);
  });

  it("generates the chosen format with the title, notes and summary option", async () => {
    const onGenerate = vi.fn().mockResolvedValue(undefined);
    render(<ReportBuilderDrawer open items={[item("a", "Filling Room")]} onClose={() => {}} onChange={vi.fn()} onGenerate={onGenerate} />);

    fireEvent.change(screen.getByLabelText(/Report title/), { target: { value: "Annual EM review" } });
    fireEvent.change(screen.getByLabelText(/Notes/), { target: { value: "For QA" } });
    fireEvent.click(screen.getByRole("checkbox"));
    fireEvent.click(screen.getByRole("button", { name: /Download Word/ }));

    await waitFor(() => expect(onGenerate).toHaveBeenCalledWith("Annual EM review", "For QA", false, "Word"));
  });

  it("cannot generate an empty report", () => {
    render(<ReportBuilderDrawer open items={[]} onClose={() => {}} onChange={vi.fn()} onGenerate={vi.fn()} />);
    expect((screen.getByRole("button", { name: /Download PDF/ }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("shows the server's message from a blob error response", async () => {
    const blob = new Blob([JSON.stringify({ message: "Chart 2 image is not a PNG." })], { type: "application/json" });
    expect(await errorMessage({ response: { data: blob } })).toBe("Chart 2 image is not a PNG.");
    expect(await errorMessage({ response: { data: { message: "plain" } } })).toBe("plain");
  });
});
