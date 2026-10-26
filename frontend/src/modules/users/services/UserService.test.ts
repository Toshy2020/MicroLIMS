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

    await UserService.updateProfile(7, "Jane Analyst", "janalyst", "jane@example.com", "Senior Analyst", 3);

    expect(apiClient.put).toHaveBeenCalledWith(
      "/users/7",
      { fullName: "Jane Analyst", username: "janalyst", email: "jane@example.com", jobTitle: "Senior Analyst" },
      { headers: { "If-Match": "\"3\"" } }
    );
  });

  it("sends an explicit null job title when the field is left empty", async () => {
    vi.mocked(apiClient.put).mockResolvedValue({ data: { data: { id: 7 } } } as never);

    await UserService.updateProfile(7, "Jane Analyst", "janalyst", null, null);

    expect(apiClient.put).toHaveBeenCalledWith(
      "/users/7",
      { fullName: "Jane Analyst", username: "janalyst", email: null, jobTitle: null },
      {}
    );
  });
});

describe("UserService.create", () => {
  it("sends the job title with the new user", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { data: { id: 9 } } } as never);

    await UserService.create("Jane Analyst", "janalyst", "Password123", 2, "", "Senior Analyst");

    expect(apiClient.post).toHaveBeenCalledWith("/users", {
      fullName: "Jane Analyst",
      username: "janalyst",
      password: "Password123",
      roleId: 2,
      email: null,
      jobTitle: "Senior Analyst"
    });
  });
});
