import { describe, expect, it } from "vitest";
import { firstWorkspacePath, workspacePathForCategory } from "./physchemWorkspacePath";

describe("physchemWorkspacePath", () => {
  it("maps sample category to its workspace", () => {
    expect(workspacePathForCategory("RawMaterial")).toBe("/physicochemical/rm-pm-workspace");
    expect(workspacePathForCategory("PackagingMaterial")).toBe("/physicochemical/rm-pm-workspace");
    expect(workspacePathForCategory("FinishedProduct")).toBe("/physicochemical/workspace");
    expect(workspacePathForCategory(undefined)).toBe("/physicochemical/workspace");
  });
  it("picks the first workspace the user has", () => {
    expect(firstWorkspacePath(["fp", "rmpm"])).toBe("/physicochemical/workspace");
    expect(firstWorkspacePath(["fp"])).toBe("/physicochemical/workspace");
    expect(firstWorkspacePath(["rmpm"])).toBe("/physicochemical/rm-pm-workspace");
  });
});
