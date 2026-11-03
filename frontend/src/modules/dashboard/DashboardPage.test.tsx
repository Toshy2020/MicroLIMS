import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { DashboardPage } from "./DashboardPage";

vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => ({ role: "QcManager", permissions: [] }) }));
vi.mock("../../hooks/useMyLabs", () => ({ useMyLabs: () => ({ codes: [], physchemAreas: [], loading: false }) }));
vi.mock("./AnalystDashboardPage", () => ({ AnalystDashboardPage: () => null }));
vi.mock("./ReviewerDashboardPage", () => ({ ReviewerDashboardPage: () => null }));
vi.mock("./AdminDashboardPage", () => ({ AdminDashboardPage: () => null }));
vi.mock("./SectionHeadDashboardPage", () => ({ SectionHeadDashboardPage: () => <div>Section head dashboard</div> }));

describe("DashboardPage", () => {
  it("sends a QC Manager to the Items page instead of a dashboard it cannot read", () => {
    render(
      <MemoryRouter initialEntries={["/dashboard"]}>
        <Routes>
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/laboratory-configuration/items" element={<div>Items page</div>} />
        </Routes>
      </MemoryRouter>
    );
    expect(screen.getByText("Items page")).toBeTruthy();
    expect(screen.queryByText("Section head dashboard")).toBeNull();
  });
});
