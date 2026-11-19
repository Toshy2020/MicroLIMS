import { describe, expect, it } from "vitest";
import { MY_WORK_TARGET_TYPES, type MyWorkTarget } from "./myWorkTypes";
import { routeForGroup, routeForTarget } from "./targetRoutes";

// Query parameters that ReceivingTestingWorkspacePage.tsx reads
// (frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx ~L173, ~L240-262, ~L360-372),
// plus `open` and `incubationId` (read in Part B).
const ALLOWED_WORKSPACE_PARAMS = new Set([
  "sampleId",
  "testOrderId",
  "incubationId",
  "open",
  "workload",
  "scope",
  "status"
]);

const WHITELISTED_WORKLOADS = new Set([
  "needsPreparation",
  "readyToRead",
  "awaitingReview",
  "overdue",
  "mine",
  "unassigned",
  "retestInProgress",
  "reviewOverdue",
  "approvalOverdue"
]);

describe("routeForTarget", () => {
  it.each(MY_WORK_TARGET_TYPES)("maps canonical target type %s to an existing route", (type) => {
    const target: MyWorkTarget = {
      type,
      sampleId: 10,
      testOrderId: 20,
      incubationId: 30,
      documentId: 40,
      taskId: 50,
      equipmentId: 60,
      mediaId: 70
    };
    const route = routeForTarget(target, "MICRO");
    expect(typeof route).toBe("string");
    expect(route).toMatch(/^\//);
  });

  it("routes workspace targets to microbiology or physicochemical workspace by lab", () => {
    expect(
      routeForTarget({ type: "test-step", sampleId: 12, testOrderId: 34 }, "MICRO")
    ).toBe("/microbiology/workspace?sampleId=12&testOrderId=34&open=step");

    expect(
      routeForTarget({ type: "test-step", sampleId: 12, testOrderId: 34 }, "FP")
    ).toBe("/physicochemical/workspace?sampleId=12&testOrderId=34&open=step");

    expect(
      routeForTarget({ type: "incubation-read", sampleId: 12, testOrderId: 34, incubationId: 56 }, "MICRO")
    ).toBe("/microbiology/workspace?sampleId=12&testOrderId=34&open=incubation&incubationId=56");

    expect(
      routeForTarget({ type: "sample-review", sampleId: 12 }, "FP")
    ).toBe("/physicochemical/workspace?sampleId=12&open=review");

    expect(
      routeForTarget({ type: "sample-approval", sampleId: 12 }, "MICRO")
    ).toBe("/microbiology/workspace?sampleId=12&open=approval");

    expect(
      routeForTarget({ type: "sample-assign", sampleId: 12, testOrderId: 34 }, "MICRO")
    ).toBe("/microbiology/workspace?sampleId=12&testOrderId=34&open=assign");
  });

  it("returns null for an unknown target type", () => {
    expect(routeForTarget({ type: "unknown-future-type" })).toBeNull();
  });
});

describe("routeForGroup", () => {
  it("maps known group keys to filtered workspace or list routes", () => {
    expect(routeForGroup("readyToRead")).toBe("/microbiology/workspace?workload=readyToRead");
    expect(routeForGroup("returned")).toBe("/microbiology/workspace?scope=mine");
    expect(routeForGroup("endingToday")).toBe("/microbiology/workspace?scope=mine");
    expect(routeForGroup("next24Hours")).toBe("/microbiology/workspace?scope=mine");
    expect(routeForGroup("toPerform")).toBe("/microbiology/workspace?scope=mine");
    expect(routeForGroup("review")).toBe("/microbiology/workspace?status=UnderReview");
    expect(routeForGroup("approval")).toBe("/microbiology/workspace?status=UnderApproval");
    expect(routeForGroup("available")).toBe("/microbiology/workspace?workload=unassigned");
    expect(routeForGroup("unassigned")).toBe("/microbiology/workspace?workload=unassigned");
    expect(routeForGroup("documents")).toBe("/document-control/my-reading-list");
    expect(routeForGroup("equipment")).toBe("/inventory/equipment");
    expect(routeForGroup("materials")).toBe("/laboratory-configuration/media");
  });
});

describe("workspace query parameter allow-list", () => {
  const WORKSPACE_PATHS = new Set(["/microbiology/workspace", "/physicochemical/workspace"]);
  const GROUP_KEYS = [
    "readyToRead",
    "returned",
    "endingToday",
    "next24Hours",
    "toPerform",
    "review",
    "approval",
    "available",
    "unassigned",
    "documents",
    "equipment",
    "materials"
  ];

  function assertWorkspaceParams(url: string) {
    const parsed = new URL(url, "http://localhost");
    if (!WORKSPACE_PATHS.has(parsed.pathname)) return;
    for (const [key, value] of parsed.searchParams.entries()) {
      expect(ALLOWED_WORKSPACE_PARAMS.has(key), `Unexpected workspace query param "${key}" in ${url}`).toBe(true);
      if (key === "workload") {
        expect(WHITELISTED_WORKLOADS.has(value), `Unexpected workload "${value}" in ${url}`).toBe(true);
      }
      if (key === "scope") {
        expect(value).toBe("mine");
      }
    }
  }

  it("only emits workspace query parameters that ReceivingTestingWorkspacePage reads", () => {
    for (const type of MY_WORK_TARGET_TYPES) {
      const url = routeForTarget(
        {
          type,
          sampleId: 10,
          testOrderId: 20,
          incubationId: 30,
          documentId: 40,
          taskId: 50,
          equipmentId: 60,
          mediaId: 70
        },
        "MICRO"
      );
      if (url) assertWorkspaceParams(url);
    }

    for (const key of GROUP_KEYS) {
      assertWorkspaceParams(routeForGroup(key, "MICRO"));
      assertWorkspaceParams(routeForGroup(key, "FP"));
    }
  });
});
