import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ThemeProvider } from "@mui/material/styles";
import { SampleStatusKpiCards } from "./SampleStatusKpiCards";
import { getTheme } from "../../../theme";

describe("SampleStatusKpiCards loading state", () => {
  it("does not show 0 for tiles while counts are still loading", () => {
    render(
      <ThemeProvider theme={getTheme("light")}>
        <SampleStatusKpiCards counts={null} activeKey={null} onSelect={() => {}} onClear={() => {}} allCount={null} canAssignAnalyst />
      </ThemeProvider>
    );
    expect(screen.queryByText("0", { selector: "p, span:not([aria-hidden])" })).toBeNull();
  });

  it("shows real zeros once counts have loaded", () => {
    const counts = new Proxy({}, { get: () => 0 }) as never;
    render(
      <ThemeProvider theme={getTheme("light")}>
        <SampleStatusKpiCards counts={counts} activeKey={null} onSelect={() => {}} onClear={() => {}} allCount={0} canAssignAnalyst />
      </ThemeProvider>
    );
    expect(screen.getAllByText("0").length).toBeGreaterThan(0);
  });
});
