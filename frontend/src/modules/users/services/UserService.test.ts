import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { UserService } from "./UserService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  ifMatch: (version: number | null | undefined) => (version == null ? {} : { headers: { "If-Match": `"${version}"` } })
}));

afterEach(() => vi.clearAllMocks());

describe("UserService.updateProfile", () => {
  it("sends the job title in the request body so an edit does not clear it", async () => {
    vi.mocked(apiClient.put).mockResolvedValue({ data: { data: { id: 7 } } } as never);

    await UserService.updateProfile(7, "Jane Analyst", "janalyst", "jane@example.com", "Senior Analyst", "Annual review", 3);

    expect(apiClient.put).toHaveBeenCalledWith(
      "/users/7",
      { fullName: "Jane Analyst", username: "janalyst", email: "jane@example.com", jobTitle: "Senior Analyst", reason: "Annual review" },
      { headers: { "If-Match": "\"3\"" } }
    );
  });

  it("sends an explicit null job title when the field is left empty", async () => {
    vi.mocked(apiClient.put).mockResolvedValue({ data: { data: { id: 7 } } } as never);

    await UserService.updateProfile(7, "Jane Analyst", "janalyst", null, null, "Annual review");

    expect(apiClient.put).toHaveBeenCalledWith(
      "/users/7",
      { fullName: "Jane Analyst", username: "janalyst", email: null, jobTitle: null, reason: "Annual review" },
      {}
    );
  });
});

describe("UserService.create", () => {
  it("sends the job title with the new user", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { data: { id: 9 } } } as never);

    await UserService.create("Jane Analyst", "janalyst", "Password123", 2, "New starter", "", "Senior Analyst");

    expect(apiClient.post).toHaveBeenCalledWith("/users", {
      fullName: "Jane Analyst",
      username: "janalyst",
      password: "Password123",
      roleId: 2,
      email: null,
      jobTitle: "Senior Analyst",
      reason: "New starter"
    });
  });
});

describe("UserService signed and reasoned actions", () => {
  it("sends the reason and the signature password with a role change", async () => {
    vi.mocked(apiClient.put).mockResolvedValue({ data: { data: {} } } as never);

    await UserService.changeRole(7, 3, "Promotion", "pw");

    expect(apiClient.put).toHaveBeenCalledWith("/users/7/role", { roleId: 3, reason: "Promotion", password: "pw" });
  });

  it("sends the reason and password in the body of a hard delete", async () => {
    vi.mocked(apiClient.delete).mockResolvedValue({ data: { data: {} } } as never);

    await UserService.hardDelete(7, "Created by mistake", "pw");

    expect(apiClient.delete).toHaveBeenCalledWith("/users/7", { data: { reason: "Created by mistake", password: "pw" } });
  });

  it("sends the reason with force-password-change, unlock, status and reset", async () => {
    vi.mocked(apiClient.put).mockResolvedValue({ data: { data: {} } } as never);
    vi.mocked(apiClient.post).mockResolvedValue({ data: { data: {} } } as never);

    await UserService.forcePasswordChange(7, "Policy");
    await UserService.unlock(7, "Verified");
    await UserService.setStatus(7, true, "Returned");
    await UserService.initiatePasswordReset(7, "Forgot");

    expect(apiClient.put).toHaveBeenCalledWith("/users/7/force-password-change", { reason: "Policy" });
    expect(apiClient.put).toHaveBeenCalledWith("/users/7/unlock", { reason: "Verified" });
    expect(apiClient.put).toHaveBeenCalledWith("/users/7/status", { isActive: true, reason: "Returned" });
    expect(apiClient.post).toHaveBeenCalledWith("/users/7/password-reset", { reason: "Forgot" });
  });
});
