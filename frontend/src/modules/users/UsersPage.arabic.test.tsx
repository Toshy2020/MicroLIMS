import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { UsersPage } from "./UsersPage";
import { UserService, UserRecord } from "./services/UserService";
import { RoleService } from "../roles/services/RoleService";

vi.mock("./services/UserService", () => ({ UserService: { getAll: vi.fn() } }));
vi.mock("../roles/services/RoleService", () => ({ RoleService: { getAll: vi.fn() } }));

const target: UserRecord = {
  id: 2, fullName: "Jane Analyst", username: "janalyst", email: "jane@example.com", roleId: 4,
  role: { id: 4, name: "Analyst", type: "Analyst" }, isActive: true, isLocked: true, lockedUntil: null,
  mustChangePassword: false, createdAt: "2026-01-01T00:00:00Z", lastLoginAt: null, passwordChangedAt: null
};

describe("UsersPage in Arabic", () => {
  beforeEach(() => {
    vi.mocked(UserService.getAll).mockResolvedValue([target]);
    vi.mocked(RoleService.getAll).mockResolvedValue([]);
  });
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("renders Arabic headings and row action names", async () => {
    await renderInArabic(<AuthProvider><UsersPage /></AuthProvider>, ["common", "users"]);
    expect(await screen.findByRole("heading", { name: "إدارة المستخدمين" })).toBeTruthy();
    expect(await screen.findByRole("button", { name: "إلغاء قفل الحساب" })).toBeTruthy();
    expect(screen.getByText("Jane Analyst")).toBeTruthy();
  });
});
