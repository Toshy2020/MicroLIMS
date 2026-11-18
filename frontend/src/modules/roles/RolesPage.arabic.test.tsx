import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { RolesPage } from "./RolesPage";
import { RoleService } from "./services/RoleService";
import { UserService } from "../users/services/UserService";

vi.mock("./services/RoleService", () => ({ RoleService: { getAll: vi.fn() } }));
vi.mock("../users/services/UserService", () => ({ UserService: { getAll: vi.fn() } }));

describe("RolesPage in Arabic", () => {
  beforeEach(() => {
    vi.mocked(RoleService.getAll).mockResolvedValue([
      { id: 4, name: "Analyst", description: null, type: "Analyst", isSystemRole: true, isActive: true }
    ]);
    vi.mocked(UserService.getAll).mockResolvedValue([]);
  });
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("renders Arabic headings, columns and row action names", async () => {
    await renderInArabic(<MemoryRouter><RolesPage /></MemoryRouter>, ["common", "roles"]);
    expect(await screen.findByRole("heading", { name: "الأدوار" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "إنشاء دور" })).toBeTruthy();
    expect(await screen.findByRole("button", { name: "تعديل Analyst" })).toBeTruthy();
    expect(screen.getByRole("columnheader", { name: "المستخدمون" })).toBeTruthy();
  });
});
