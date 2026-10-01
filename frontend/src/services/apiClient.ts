import axios, { AxiosError, InternalAxiosRequestConfig } from "axios";
import { readTokenClaims, TOKEN_REFRESHED_EVENT } from "../modules/authentication/tokenClaims";

// Single axios instance every module service goes through. No business
// logic here - it only attaches the auth token and handles 401s
// (Frozen Principle #3 - Frontend never implements laboratory rules).
function getApiBaseUrl(): string {
  const envUrl = import.meta.env.VITE_API_BASE_URL || import.meta.env.VITE_API_URL;
  if (envUrl) {
    const trimmed = String(envUrl).trim().replace(/\/+$/, "");
    return trimmed.endsWith("/api") ? trimmed : `${trimmed}/api`;
  }
  // When running online in production (Cloudflare Pages, Workers, or custom domain),
  // always connect to the production Render backend API.
  if (
    typeof window !== "undefined" &&
    window.location.hostname !== "localhost" &&
    window.location.hostname !== "127.0.0.1"
  ) {
    return "https://microlims.onrender.com/api";
  }
  return "http://localhost:5000/api";
}

const baseURL = getApiBaseUrl();

export const apiClient = axios.create({ baseURL });

// The backend stamps every response with X-Correlation-Id and exposes it
// via CORS. Holding on to the most recent failing one lets a client-side
// crash report attach to the same Incident as the backend error behind
// it, instead of the admin seeing two unrelated rows for one user action.
let lastCorrelationId: string | undefined;

// Stale ids are worse than none: attaching a fresh crash to an unrelated
// failure from ten minutes ago silently merges two incidents.
const CORRELATION_ID_TTL_MS = 30_000;
let lastCorrelationAt = 0;

export function getLastCorrelationId(): string | undefined {
  if (!lastCorrelationId) return undefined;
  return Date.now() - lastCorrelationAt <= CORRELATION_ID_TTL_MS ? lastCorrelationId : undefined;
}

function rememberCorrelationId(error: AxiosError): void {
  const header = error.response?.headers?.["x-correlation-id"];
  if (typeof header === "string" && header) {
    lastCorrelationId = header;
    lastCorrelationAt = Date.now();
  }
}

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem("microlims_token");
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

const AUTH_STORAGE_KEYS = [
  "microlims_token",
  "microlims_refresh_token",
  "microlims_username",
  "microlims_role",
  "microlims_permissions",
  "microlims_full_name",
  "microlims_user_id",
  "microlims_must_change_password"
];

function clearAuthStorageAndRedirect() {
  AUTH_STORAGE_KEYS.forEach((key) => localStorage.removeItem(key));
  window.location.href = "/login";
}

// Coalesce concurrent 401s into a single refresh call instead of firing
// one refresh request per failed request.
let refreshPromise: Promise<string | null> | null = null;

function refreshAccessToken(): Promise<string | null> {
  const storedRefreshToken = localStorage.getItem("microlims_refresh_token");
  if (!storedRefreshToken) return Promise.resolve(null);

  if (!refreshPromise) {
    refreshPromise = axios
      .post(`${baseURL}/auth/refresh`, { refreshToken: storedRefreshToken })
      .then((res) => {
        const { token, refreshToken } = res.data.data as { token: string; refreshToken: string };
        localStorage.setItem("microlims_token", token);
        localStorage.setItem("microlims_refresh_token", refreshToken);
        // The new token carries the user's current role and permissions -
        // store them and tell AuthContext, so menus and page gates change
        // as soon as an administrator's change reaches this session.
        const { role, permissions } = readTokenClaims(token);
        if (role) localStorage.setItem("microlims_role", role);
        localStorage.setItem("microlims_permissions", JSON.stringify(permissions));
        window.dispatchEvent(new Event(TOKEN_REFRESHED_EVENT));
        return token;
      })
      .catch(() => null)
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
}

// File view and download requests use responseType "blob", so their error
// bodies arrive as Blobs too and every caller reading
// err.response.data.message sees nothing. Decode JSON error bodies back
// into objects so the backend's message reaches the UI.
async function decodeBlobErrorBody(error: AxiosError): Promise<void> {
  const response = error.response;
  const body = response?.data;
  if (!response || !(body instanceof Blob) || !body.type.includes("json")) return;
  try {
    response.data = JSON.parse(await body.text());
  } catch {
    // Not valid JSON - leave the original body in place.
  }
}

// What a laboratory user is told when the server gave no message of its own.
// Screens show `err.response?.data?.message ?? err.message`; without this,
// err.message was axios's own text - "Request failed with status code 500",
// "Network Error" - which tells an analyst nothing about what to do next.
function friendlyTransportMessage(error: AxiosError): string | null {
  const status = error.response?.status;
  if (!error.response) {
    if (error.code === "ECONNABORTED" || error.code === "ETIMEDOUT") {
      return "The server took too long to respond. Check your connection and try again.";
    }
    return "Could not reach the MicroLIMS server. Check your network connection and try again.";
  }
  if (status === 400) return "The request could not be processed. Check the entered values and try again.";
  if (status === 401) return "Your sign-in is no longer valid. Please sign in again.";
  if (status === 403) return "You do not have permission to perform this action.";
  if (status === 404) return "The requested record could not be found. It may have been removed.";
  if (status === 409) return "This record was changed by someone else. Reload it and make your change again.";
  if (status === 413) return "The file is too large to upload.";
  if (status === 429) return "Too many requests in a short time. Wait a moment and try again.";
  if (status !== undefined && status >= 500) {
    return "The server could not complete the request. Try again; if it keeps happening, contact your system administrator.";
  }
  return null;
}

// Keeps the technical text for the error reporter and the browser console,
// and puts a readable explanation where screens look for one.
function humanizeError(error: AxiosError): void {
  if (axios.isCancel(error)) return;
  const friendly = friendlyTransportMessage(error);
  if (!friendly) return;
  (error as AxiosError & { technicalMessage?: string }).technicalMessage = error.message;
  error.message = friendly;
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    rememberCorrelationId(error);
    await decodeBlobErrorBody(error);

    const config = error.config as RetryableRequestConfig | undefined;
    const isAuthEndpoint = config?.url?.includes("/auth/refresh") || config?.url?.includes("/auth/login");

    if (error.response?.status !== 401 || !config || config._retry || isAuthEndpoint) {
      humanizeError(error);
      return Promise.reject(error);
    }

    config._retry = true;
    const newToken = await refreshAccessToken();
    if (!newToken) {
      clearAuthStorageAndRedirect();
      humanizeError(error);
      return Promise.reject(error);
    }

    config.headers.Authorization = `Bearer ${newToken}`;
    return apiClient(config);
  }
);

// Request config for an edit: sends the Version of the record the form was
// loaded with as If-Match, so the server refuses the save (409, "reload and
// make your change again") if someone else changed the record since.
export function ifMatch(version: number | null | undefined): { headers?: Record<string, string> } {
  return version == null ? {} : { headers: { "If-Match": `"${version}"` } };
}
