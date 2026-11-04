import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { LEGACY_ADMIN_REDIRECTS, LegacyPathRedirect } from "./legacyAdminRedirects";

afterEach(() => cleanup());

function Where() {
  const { pathname, search } = useLocation();
  return <div data-testid="where">{pathname + search}</div>;
}

function renderAt(url: string) {
  render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        {LEGACY_ADMIN_REDIRECTS.map((r) => (
          <Route key={r.from} path={r.from} element={<LegacyPathRedirect to={r.to} />} />
        ))}
        <Route path="*" element={<Where />} />
      </Routes>
    </MemoryRouter>
  );
  return screen.getByTestId("where").textContent;
}

describe("legacy admin URLs", () => {
  it.each([
    ["/users", "/admin/users"],
    ["/roles", "/admin/roles"],
    ["/roles/new", "/admin/roles/new"],
    ["/roles/42", "/admin/roles/42"],
    ["/audit-search", "/admin/audit"],
    ["/error-monitoring", "/admin/error-log"]
  ])("redirects %s to %s", (from, to) => {
    expect(renderAt(from)).toBe(to);
  });

  it("keeps the query string, so a deep link to a filtered audit search survives", () => {
    expect(renderAt("/audit-search?entity=Sample&id=7")).toBe("/admin/audit?entity=Sample&id=7");
  });
});
