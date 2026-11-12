import { cleanup, render, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterAll, afterEach, beforeAll, describe, expect, it } from "vitest";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import type { ControlChartPoint, ControlChartResult } from "../../types/controlChartTypes";
import { SpcChart, SpcChartOptions } from "./SpcCharts";

// jsdom has no layout, and Recharts turns a pointer position into chart
// coordinates from the measured size of its wrapper element. Report the
// chart's size for that one element so a hover resolves to a data point;
// the chart itself, its legend and its axes are untouched.
const CHART_WIDTH = 900;
const CHART_HEIGHT = 400;
const isChartWrapper = (el: Element) => el.classList.contains("recharts-wrapper");
const originals = {
  offsetWidth: Object.getOwnPropertyDescriptor(HTMLElement.prototype, "offsetWidth"),
  offsetHeight: Object.getOwnPropertyDescriptor(HTMLElement.prototype, "offsetHeight"),
  rect: HTMLElement.prototype.getBoundingClientRect
};

beforeAll(() => {
  Object.defineProperty(HTMLElement.prototype, "offsetWidth", {
    configurable: true,
    get(this: HTMLElement) { return isChartWrapper(this) ? CHART_WIDTH : 0; }
  });
  Object.defineProperty(HTMLElement.prototype, "offsetHeight", {
    configurable: true,
    get(this: HTMLElement) { return isChartWrapper(this) ? CHART_HEIGHT : 0; }
  });
  HTMLElement.prototype.getBoundingClientRect = function (this: HTMLElement) {
    return isChartWrapper(this)
      ? ({ x: 0, y: 0, left: 0, top: 0, right: CHART_WIDTH, bottom: CHART_HEIGHT, width: CHART_WIDTH, height: CHART_HEIGHT, toJSON: () => ({}) } as DOMRect)
      : originals.rect.call(this);
  };
});

afterAll(() => {
  if (originals.offsetWidth) Object.defineProperty(HTMLElement.prototype, "offsetWidth", originals.offsetWidth);
  if (originals.offsetHeight) Object.defineProperty(HTMLElement.prototype, "offsetHeight", originals.offsetHeight);
  HTMLElement.prototype.getBoundingClientRect = originals.rect;
});

afterEach(cleanup);

const OPTIONS: SpcChartOptions = { showLowerLimits: false, showConfiguredLimits: false, showRuleNumbers: false, showValueLabels: false };

function point(recordId: number, referenceNumber: string, date: string, value: number, reportedValue: string, isImputed: boolean): ControlChartPoint {
  return {
    recordId, referenceNumber, date, value, reportedValue, isImputed, movingRange: null, resultLevel: "WithinLimit",
    inCalculation: true, rules: [], alertLimit: null, actionLimit: null, specLimit: null
  };
}

// Three samples share 10 Sept, as in production: several samples are tested
// on one day, so the date label repeats along the X axis.
const POINTS: ControlChartPoint[] = [
  point(1, "FP0926052", "2026-09-09T08:00:00Z", 30, "30 cfu/g", false),
  point(2, "FP0926055", "2026-09-10T08:00:00Z", 10, "<20 cfu/g", true),
  point(3, "FP0926056", "2026-09-10T08:00:00Z", 10, "<20 cfu/g", true),
  point(4, "FP0926058", "2026-09-10T08:00:00Z", 140, "140 cfu/g", false),
  point(5, "FP0926060", "2026-09-12T08:00:00Z", 12, "12 cfu/g", false)
];

const RESULT = {
  testCode: "TAMC", testDisplayName: "TAMC", subjectName: "Perfectil Hair Extra Tab", unit: "cfu/g",
  statistics: { limits: { actionUpper: 59, alertUpper: 29, alertLower: null, actionLower: null }, mean: 20, movingRangeMean: null, movingRangeUcl: null },
  points: POINTS, monthly: [], limitTargets: []
} as unknown as ControlChartResult;

describe("SpcChart tooltip", () => {
  it.each(POINTS.map((p, i) => [p.referenceNumber, i] as const))(
    "shows the sample under the cursor, not the first sample of the same day (%s)",
    async (referenceNumber, index) => {
      const user = userEvent.setup();
      const { container } = render(
        <PinnedLightTheme><SpcChart kind="individuals" result={RESULT} options={OPTIONS} width={CHART_WIDTH} height={CHART_HEIGHT} /></PinnedLightTheme>
      );

      // The "Result" line is the last .recharts-line-dots group; its dots are in data order.
      // Recharts draws them a tick after mounting.
      const resultDots = () => {
        const groups = container.querySelectorAll(".recharts-line-dots");
        return groups.length === 0 ? [] : Array.from(groups[groups.length - 1].children);
      };
      await waitFor(() => expect(resultDots()).toHaveLength(POINTS.length));
      const dot = resultDots()[index].querySelector("circle, rect")!;
      const x = Number(dot.getAttribute("cx") ?? Number(dot.getAttribute("x")) + 4.5);
      const y = Number(dot.getAttribute("cy") ?? Number(dot.getAttribute("y")) + 4.5);

      await user.pointer({ target: container.querySelector(".recharts-wrapper")!, coords: { clientX: x, clientY: y } });

      // Recharts applies a hover on an animation frame: wait for the tooltip to
      // name *some* sample, then check it names the one that was hovered.
      const tooltipText = () => container.querySelector(".recharts-tooltip-wrapper")?.textContent ?? "";
      await waitFor(() => expect(tooltipText()).toMatch(/FP\d+/));
      expect(tooltipText()).toContain(referenceNumber);
      expect(tooltipText()).toContain(`Result: ${POINTS[index].reportedValue}`);
    }
  );
});
