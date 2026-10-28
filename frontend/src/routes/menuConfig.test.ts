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

describe("menuConfig document audit link", () => {
  const hasAuditLink = (role: "Analyst" | "Reviewer" | "SectionHead" | "SystemAdministrator", permissions: string[]) => {
    const groups = getGroupedMenu({ role, permissions, labCodes: [] });
    return findItemByPath(groups.flatMap((g) => g.items), "/document-control/audit") !== undefined;
  };

  // Mirrors the server rule: Reviewers and Section Heads by role, anyone else only with Audit.View.
  it("is offered to Reviewers, Section Heads and Audit.View holders, and to nobody else", () => {
    expect(hasAuditLink("Reviewer", [])).toBe(true);
    expect(hasAuditLink("SectionHead", [])).toBe(true);
    expect(hasAuditLink("Analyst", [PERMISSIONS.AUDIT_VIEW])).toBe(true);
    expect(hasAuditLink("SystemAdministrator", [PERMISSIONS.AUDIT_VIEW])).toBe(true);
    expect(hasAuditLink("Analyst", [])).toBe(false);
    expect(hasAuditLink("SystemAdministrator", [])).toBe(false);
  });
});

describe("menuConfig Administration group", () => {
  const adminPaths = (permissions: string[]) => {
    const group = getGroupedMenu({ role: "Analyst", permissions, labCodes: [] }).find((g) => g.groupName === "ADMINISTRATION");
    return group?.items.map((i) => i.path) ?? [];
  };

  it("offers each admin page only to the permission that guards it", () => {
    expect(adminPaths([])).toEqual([]);
    expect(adminPaths([PERMISSIONS.USERS_MANAGE])).toEqual(["/admin/users"]);
    expect(adminPaths([PERMISSIONS.SYSTEM_CONFIGURE_SETTINGS])).toEqual(["/admin/settings"]);
    expect(adminPaths([PERMISSIONS.ROLES_MANAGE])).toEqual(["/admin/roles"]);
    expect(adminPaths([PERMISSIONS.AUDIT_VIEW])).toEqual(["/admin/audit"]);
    expect(adminPaths([PERMISSIONS.SYSTEM_VIEW_ERROR_LOG])).toEqual(["/admin/error-log"]);
  });

  it("lists all five pages, in tab order, for a full administrator", () => {
    expect(adminPaths([PERMISSIONS.USERS_MANAGE, PERMISSIONS.ROLES_MANAGE, PERMISSIONS.AUDIT_VIEW, PERMISSIONS.SYSTEM_VIEW_ERROR_LOG, PERMISSIONS.SYSTEM_CONFIGURE_SETTINGS]))
      .toEqual(["/admin/users", "/admin/roles", "/admin/audit", "/admin/error-log", "/admin/settings"]);
  });

  it("no longer scatters admin entries under the old groups or paths", () => {
    const groups = getGroupedMenu({
      role: "SystemAdministrator",
      permissions: [PERMISSIONS.USERS_MANAGE, PERMISSIONS.ROLES_MANAGE, PERMISSIONS.AUDIT_VIEW, PERMISSIONS.SYSTEM_VIEW_ERROR_LOG, PERMISSIONS.OOS_MANAGE],
      labCodes: []
    });
    const all = groups.flatMap((g) => g.items);
    for (const old of ["/users", "/roles", "/audit-search", "/error-monitoring"]) expect(findItemByPath(all, old)).toBeUndefined();
    expect(groups.find((g) => g.groupName === "SYSTEM")).toBeUndefined();
  });
});

describe("Master Testing Materials menu entry", () => {
  const labels = (groups: ReturnType<typeof getGroupedMenu>) => {
    const out: string[] = [];
    const walk = (items: MenuItem[]) => items.forEach((i) => { out.push(i.label); if (i.children) walk(i.children); });
    groups.forEach((g) => walk(g.items));
    return out;
  };

  it("is one shared entry that replaces Reagents & Standards, Media Configurations and Organisms", () => {
    const groups = getGroupedMenu({ role: "Analyst", permissions: [PERMISSIONS.MASTER_DATA_MANAGE], labCodes: ["MICRO", "FP"] });
    const all = labels(groups);
    expect(all.filter((l) => l === "Master Testing Materials")).toHaveLength(1);
    expect(all).not.toContain("Reagents & Standards");
    expect(all).not.toContain("Media Configurations");
    expect(all).not.toContain("Organisms");
    const shared = groups.find((g) => g.groupName === "GENERAL LABORATORY CONFIGURATION");
    expect(findItemByPath(shared?.items ?? [], "/laboratory-configuration/master-testing-materials")).toBeDefined();
  });

  it("is shown to either lab alone, and only with the master data permission", () => {
    for (const lab of ["MICRO", "FP"]) {
      const withPerm = getGroupedMenu({ role: "Analyst", permissions: [PERMISSIONS.MASTER_DATA_MANAGE], labCodes: [lab] });
      expect(labels(withPerm)).toContain("Master Testing Materials");
    }
    const without = getGroupedMenu({ role: "Analyst", permissions: [], labCodes: ["MICRO"] });
    expect(labels(without)).not.toContain("Master Testing Materials");
  });
});
