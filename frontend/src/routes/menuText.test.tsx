import { afterEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import enNavigation from "../locales/en/navigation.json";
import arNavigation from "../locales/ar/navigation.json";
import { i18n } from "../i18n";
import { renderInArabic, resetLanguage } from "../i18n/testUtils";
import { getGroupedMenu, type MenuItem } from "./menuConfig";
import { accountPageTrail } from "./navigation";
import { menuKey, useMenuText } from "./menuText";
import { PERMISSIONS } from "./routes";

const ACCOUNT_PATHS = ["/profile", "/change-password", "/set-signing-password", "/messages", "/discussions", "/notifications", "/receiving-testing"];

function everyLabelAndGroup(): { labels: Set<string>; groups: Set<string> } {
  const labels = new Set<string>();
  const groups = new Set<string>();
  const walk = (item: MenuItem) => {
    labels.add(item.label);
    item.children?.forEach(walk);
  };
  // The menu of a user who may see everything, in both laboratories.
  const menus = [
    getGroupedMenu({ role: "SectionHead", permissions: Object.values(PERMISSIONS), labCodes: ["MICRO", "FP"], physchemAreas: ["fp", "rmpm"] }),
    getGroupedMenu({ role: "Reviewer", permissions: Object.values(PERMISSIONS), labCodes: ["MICRO", "FP"] })
  ];
  for (const menu of menus) {
    for (const group of menu) {
      groups.add(group.groupName);
      group.items.forEach(walk);
    }
  }
  for (const path of ACCOUNT_PATHS) {
    const trail = accountPageTrail(path);
    if (trail) {
      labels.add(trail.item.label);
      groups.add(trail.group);
    }
  }
  return { labels, groups };
}

function Probe({ label, group }: { label: string; group: string }) {
  const menu = useMenuText();
  return (
    <p>
      <span aria-label="label">{menu.label(label)}</span>
      <span aria-label="group">{menu.group(group)}</span>
    </p>
  );
}

afterEach(async () => {
  await resetLanguage();
});

describe("menu text", () => {
  it("makes a key from the English label", () => {
    expect(menuKey("My Work")).toBe("myWork");
    expect(menuKey("RM & PM Test Master")).toBe("rmPmTestMaster");
    expect(menuKey("QUALITY & COMPLIANCE")).toBe("qualityCompliance");
  });

  it("has an English and an Arabic text for every menu label and group, and English matches the menu", () => {
    const { labels, groups } = everyLabelAndGroup();
    expect(labels.size).toBeGreaterThan(50);
    const en = enNavigation as { item: Record<string, string>; group: Record<string, string> };
    const ar = arNavigation as { item: Record<string, string>; group: Record<string, string> };
    for (const label of labels) {
      expect(en.item[menuKey(label)], `English text for "${label}"`).toBe(label);
      expect(ar.item[menuKey(label)]?.trim(), `Arabic text for "${label}"`).toBeTruthy();
    }
    for (const group of groups) {
      expect(en.group[menuKey(group)], `English text for group "${group}"`).toBe(group);
      expect(ar.group[menuKey(group)]?.trim(), `Arabic text for group "${group}"`).toBeTruthy();
    }
  });

  it("gives Arabic in Arabic, English in English, and the English label when a key is missing", async () => {
    render(<Probe label="My Work" group="OVERVIEW" />);
    expect(screen.getByLabelText("label").textContent).toBe("My Work");
    expect(screen.getByLabelText("group").textContent).toBe("OVERVIEW");
    await resetLanguage();
    document.body.innerHTML = "";

    await renderInArabic(<Probe label="My Work" group="OVERVIEW" />, ["common", "navigation"]);
    expect(screen.getByLabelText("label").textContent).toBe("عملي");
    expect(screen.getByLabelText("group").textContent).toBe("نظرة عامة");
    expect(i18n.language).toBe("ar");
  });

  it("falls back to the English label for a menu entry that has no key yet", async () => {
    await renderInArabic(<Probe label="Brand New Page" group="NEW GROUP" />, ["common", "navigation"]);
    expect(screen.getByLabelText("label").textContent).toBe("Brand New Page");
    expect(screen.getByLabelText("group").textContent).toBe("NEW GROUP");
  });
});
