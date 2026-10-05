import { describe, expect, it } from "vitest";
import { getGroupedMenu, MenuItem } from "./menuConfig";
import { findNavTrail } from "./navigation";
import { PERMISSIONS } from "./routes";

function findItemByPath(items: MenuItem[], path: string): MenuItem | undefined {
  for (const item of items) {
    if (item.path === path) return item;
    if (item.children) {
      const found = findItemByPath(item.children, path);
      if (found) return found;
    }
  }
  return undefined;
}

function getLabItems(groups: ReturnType<typeof getGroupedMenu>): MenuItem[] {
  const labGroup = groups.find((g) => g.groupName === "LABORATORIES");
  return labGroup ? labGroup.items : [];
}

describe("menuConfig area filtering", () => {
  const allPermissions = [PERMISSIONS.MASTER_DATA_MANAGE, PERMISSIONS.SOLUTIONS_PREPARE, PERMISSIONS.MATERIALS_MANAGE];

  it("shows only FP Workspace when physchemAreas is fp only", () => {
    const groups = getGroupedMenu({
      role: "Analyst",
      permissions: allPermissions,
      labCodes: ["FP"],
      physchemAreas: ["fp"]
    });

    const labItems = getLabItems(groups);
    const fpWorkspace = findItemByPath(labItems, "/physicochemical/workspace");
    const rmpmWorkspace = findItemByPath(labItems, "/physicochemical/rm-pm-workspace");

    expect(fpWorkspace).toBeDefined();
    expect(fpWorkspace?.label).toBe("FP Workspace");
    expect(rmpmWorkspace).toBeUndefined();

    // Configuration retains both Test Masters
    const fpTestMaster = findItemByPath(labItems, "/laboratory-configuration/fp-test-master");
    const rmpmTestMaster = findItemByPath(labItems, "/laboratory-configuration/rm-pm-test-master");
    expect(fpTestMaster).toBeDefined();
    expect(fpTestMaster?.label).toBe("FP Test Master");
    expect(rmpmTestMaster).toBeDefined();
    expect(rmpmTestMaster?.label).toBe("RM & PM Test Master");
  });

  it("shows only RM & PM Workspace when physchemAreas is rmpm only", () => {
    const groups = getGroupedMenu({
      role: "Analyst",
      permissions: allPermissions,
      labCodes: ["FP"],
      physchemAreas: ["rmpm"]
    });

    const labItems = getLabItems(groups);
    const fpWorkspace = findItemByPath(labItems, "/physicochemical/workspace");
    const rmpmWorkspace = findItemByPath(labItems, "/physicochemical/rm-pm-workspace");

    expect(fpWorkspace).toBeUndefined();
    expect(rmpmWorkspace).toBeDefined();
    expect(rmpmWorkspace?.label).toBe("RM & PM Workspace");

    const fpTestMaster = findItemByPath(labItems, "/laboratory-configuration/fp-test-master");
    const rmpmTestMaster = findItemByPath(labItems, "/laboratory-configuration/rm-pm-test-master");
    expect(fpTestMaster).toBeDefined();
    expect(rmpmTestMaster).toBeDefined();
  });

  it("shows both FP Workspace and RM & PM Workspace when physchemAreas includes both", () => {
    const groups = getGroupedMenu({
      role: "Analyst",
      permissions: allPermissions,
      labCodes: ["FP"],
      physchemAreas: ["fp", "rmpm"]
    });

    const labItems = getLabItems(groups);
    const fpWorkspace = findItemByPath(labItems, "/physicochemical/workspace");
    const rmpmWorkspace = findItemByPath(labItems, "/physicochemical/rm-pm-workspace");

    expect(fpWorkspace).toBeDefined();
    expect(fpWorkspace?.label).toBe("FP Workspace");
    expect(rmpmWorkspace).toBeDefined();
    expect(rmpmWorkspace?.label).toBe("RM & PM Workspace");

    const fpTestMaster = findItemByPath(labItems, "/laboratory-configuration/fp-test-master");
    const rmpmTestMaster = findItemByPath(labItems, "/laboratory-configuration/rm-pm-test-master");
    expect(fpTestMaster).toBeDefined();
    expect(rmpmTestMaster).toBeDefined();
  });

  it("omits both workspaces when physchemAreas is empty", () => {
    const groups = getGroupedMenu({
      role: "Analyst",
      permissions: allPermissions,
      labCodes: ["FP"],
      physchemAreas: []
    });

    const labItems = getLabItems(groups);
    expect(findItemByPath(labItems, "/physicochemical/workspace")).toBeUndefined();
    expect(findItemByPath(labItems, "/physicochemical/rm-pm-workspace")).toBeUndefined();
  });

  it("correctly resolves nav trails for distinct workspace and test master routes", () => {
    const groups = getGroupedMenu({
      role: "SystemAdministrator",
      permissions: allPermissions,
      labCodes: ["FP"],
      physchemAreas: ["fp", "rmpm"]
    });

    const fpWorkspaceTrail = findNavTrail(groups, "/physicochemical/workspace", "");
    expect(fpWorkspaceTrail).not.toBeNull();
    expect(fpWorkspaceTrail?.item.label).toBe("FP Workspace");
    expect(fpWorkspaceTrail?.exact).toBe(true);

    const rmpmWorkspaceTrail = findNavTrail(groups, "/physicochemical/rm-pm-workspace", "");
    expect(rmpmWorkspaceTrail).not.toBeNull();
    expect(rmpmWorkspaceTrail?.item.label).toBe("RM & PM Workspace");
    expect(rmpmWorkspaceTrail?.exact).toBe(true);

    const fpTestMasterTrail = findNavTrail(groups, "/laboratory-configuration/fp-test-master", "");
    expect(fpTestMasterTrail).not.toBeNull();
    expect(fpTestMasterTrail?.item.label).toBe("FP Test Master");
    expect(fpTestMasterTrail?.exact).toBe(true);

    const rmpmTestMasterTrail = findNavTrail(groups, "/laboratory-configuration/rm-pm-test-master", "");
    expect(rmpmTestMasterTrail).not.toBeNull();
    expect(rmpmTestMasterTrail?.item.label).toBe("RM & PM Test Master");
    expect(rmpmTestMasterTrail?.exact).toBe(true);
  });
});
