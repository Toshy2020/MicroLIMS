import { afterEach, describe, expect, it } from "vitest";
import { AxiosError, type AxiosAdapter, type InternalAxiosRequestConfig } from "axios";
import { apiClient, CHANGE_CONTROL_HEADER, withChangeControl, withChangeReason } from "./apiClient";

// The real apiClient; only the network adapter is replaced so the headers that would go out can be read.
const originalAdapter = apiClient.defaults.adapter;

afterEach(() => {
  apiClient.defaults.adapter = originalAdapter;
  localStorage.clear();
});

function captureHeaders(): { sent: Array<Record<string, unknown>>; adapter: AxiosAdapter } {
  const sent: Array<Record<string, unknown>> = [];
  const adapter: AxiosAdapter = (config: InternalAxiosRequestConfig) => {
    sent.push({ ...config.headers.toJSON() });
    return Promise.resolve({ status: 200, statusText: "OK", data: {}, headers: {}, config });
  };
  return { sent, adapter };
}

describe("withChangeControl", () => {
  it("adds only the change-control header when nothing else is set", () => {
    expect(withChangeControl(undefined, "CC-2026-0007")).toEqual({ headers: { [CHANGE_CONTROL_HEADER]: "CC-2026-0007" } });
  });

  it("keeps If-Match and the change reason next to the change-control header", () => {
    const base = withChangeReason(7, "Corrected limit");
    const config = withChangeControl(base, "CC-1");
    expect(config.headers).toEqual({ ...base.headers, "X-Change-Control": "CC-1" });
  });

  it("returns the config untouched when there is no number", () => {
    const base = withChangeReason(7);
    expect(withChangeControl(base, undefined)).toBe(base);
    expect(withChangeControl(undefined, "  ")).toEqual({});
  });

  it("does not change the config it was given", () => {
    const base = { headers: { "If-Match": '"3"' } };
    withChangeControl(base, "CC-1");
    expect(base.headers).toEqual({ "If-Match": '"3"' });
  });
});

describe("apiClient per-request change-control header", () => {
  it("sends the header only on the request that asked for it", async () => {
    const { sent, adapter } = captureHeaders();
    apiClient.defaults.adapter = adapter;

    await apiClient.put("/masterdata/specifications/5", { a: 1 }, withChangeControl(withChangeReason(4), "CC-9"));
    await apiClient.put("/masterdata/specifications/5", { a: 1 }, withChangeReason(4));

    expect(sent[0][CHANGE_CONTROL_HEADER]).toBe("CC-9");
    expect(sent[0]["If-Match"]).toBe('"4"');
    expect(sent[1][CHANGE_CONTROL_HEADER]).toBeUndefined();
  });

  it("keeps the header when a 401 is refreshed and the request is replayed", async () => {
    localStorage.setItem("microlims_token", "old");
    localStorage.setItem("microlims_refresh_token", "refresh");
    const sent: Array<Record<string, unknown>> = [];
    let calls = 0;
    apiClient.defaults.adapter = (config: InternalAxiosRequestConfig) => {
      sent.push({ ...config.headers.toJSON() });
      calls += 1;
      if (calls === 1) {
        const response = { status: 401, statusText: "", data: {}, headers: {}, config };
        return Promise.reject(new AxiosError("unauthorised", "ERR_BAD_REQUEST", config, null, response));
      }
      return Promise.resolve({ status: 200, statusText: "OK", data: {}, headers: {}, config });
    };
    // The refresh call goes through plain axios, not apiClient; give it a token through the default adapter.
    const axios = (await import("axios")).default;
    const original = axios.defaults.adapter;
    axios.defaults.adapter = (config: InternalAxiosRequestConfig) =>
      Promise.resolve({
        status: 200,
        statusText: "OK",
        data: { data: { token: "new-token", refreshToken: "new-refresh" } },
        headers: {},
        config
      });
    try {
      await apiClient.delete("/masterdata/specifications/5", withChangeControl(withChangeReason(undefined, "r"), "CC-9"));
    } finally {
      axios.defaults.adapter = original;
    }

    expect(sent).toHaveLength(2);
    expect(sent[1][CHANGE_CONTROL_HEADER]).toBe("CC-9");
    expect(sent[1].Authorization).toBe("Bearer new-token");
  });
});
