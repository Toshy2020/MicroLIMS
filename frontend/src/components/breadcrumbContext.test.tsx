import { useState } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { AppBreadcrumbs } from "./AppBreadcrumbs";
import { BreadcrumbProvider, useBreadcrumb } from "./breadcrumbContext";
import type { MenuGroup } from "../routes/menuConfig";
import type { NavTrail } from "../routes/navigation";

const registerItem = { label: "Document Library", path: "/document-control/library" };
const groups: MenuGroup[] = [
  { groupName: "DOCUMENT CONTROL", items: [registerItem, { label: "Audit", path: "/document-control/audit" }] }
];
const trail: NavTrail = { group: "DOCUMENT CONTROL", item: registerItem, exact: false };

function Page({ label }: { label: string | null }) {
  useBreadcrumb(label);
  return <p>page body</p>;
}

function Harness({ label, startMounted = true }: { label: string | null; startMounted?: boolean }) {
  const [mounted, setMounted] = useState(startMounted);
  return (
    <MemoryRouter>
      <BreadcrumbProvider>
        <AppBreadcrumbs trail={trail} groups={groups} />
        {mounted && <Page label={label} />}
        <button type="button" onClick={() => setMounted(false)}>leave page</button>
      </BreadcrumbProvider>
    </MemoryRouter>
  );
}

afterEach(() => {
  cleanup();
  document.title = "";
});

describe("useBreadcrumb", () => {
  it('page-supplied label is the last crumb and has aria-current="page"', () => {
    render(<Harness label="SOP-QC-001 Sampling" />);
    const nav = screen.getByRole("navigation", { name: "Breadcrumb" });
    const current = nav.querySelector('[aria-current="page"]');
    expect(current?.textContent).toBe("SOP-QC-001 Sampling");
    // It is the last crumb, after the menu item, which now links back to its own page.
    const crumbs = nav.querySelectorAll("li");
    expect(crumbs[crumbs.length - 1].contains(current as HTMLElement)).toBe(true);
    expect(screen.getByRole("link", { name: "Document Library" }).getAttribute("href")).toBe("/document-control/library");
    expect(nav.querySelectorAll('[aria-current="page"]').length).toBe(1);
  });

  it("label removed on unmount", async () => {
    const user = userEvent.setup();
    render(<Harness label="SOP-QC-001 Sampling" />);
    expect(screen.getByText("SOP-QC-001 Sampling")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "leave page" }));
    expect(screen.queryByText("SOP-QC-001 Sampling")).toBeNull();
    // Back to the trail without a page crumb, and the tab title drops the label.
    const nav = screen.getByRole("navigation", { name: "Breadcrumb" });
    expect(nav.querySelector('[aria-current="page"]')).toBeNull();
    expect(document.title).toBe("Document Library · MicroLIMS");
  });

  it("document.title includes the label", () => {
    render(<Harness label="SOP-QC-001 Sampling" />);
    expect(document.title).toBe("SOP-QC-001 Sampling · Document Library · MicroLIMS");
  });

  it("truncated crumb exposes its full text", () => {
    const long = "SOP-QC-001 Sampling of raw materials, packaging components and finished products at every site";
    render(<Harness label={long} />);
    const nav = screen.getByRole("navigation", { name: "Breadcrumb" });
    const current = nav.querySelector('[aria-current="page"]') as HTMLElement;
    expect(current.getAttribute("title")).toBe(long);
    // The visible text is shortened; the accessible name is the full text.
    expect(current.querySelector('[aria-hidden="true"]')?.textContent?.endsWith("…")).toBe(true);
    expect(current.textContent).toContain(long);
    expect(screen.getByText(long)).toBeTruthy();
  });

  it("loading shows a skeleton, not the id", () => {
    render(
      <MemoryRouter initialEntries={["/document-control/documents/4711"]}>
        <BreadcrumbProvider>
          <AppBreadcrumbs trail={trail} groups={groups} />
          <Page label={null} />
        </BreadcrumbProvider>
      </MemoryRouter>
    );
    const nav = screen.getByRole("navigation", { name: "Breadcrumb" });
    const current = nav.querySelector('[aria-current="page"]') as HTMLElement;
    expect(current.querySelector(".MuiSkeleton-root")).not.toBeNull();
    expect(current.getAttribute("aria-busy")).toBe("true");
    expect(nav.textContent).not.toContain("4711");
    // The tab title leaves the unknown part out rather than showing a placeholder.
    expect(document.title).toBe("Document Library · MicroLIMS");
  });
});
