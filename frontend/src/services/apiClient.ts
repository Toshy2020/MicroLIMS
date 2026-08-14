import axios, { AxiosError, InternalAxiosRequestConfig } from "axios";

// Single axios instance every module service goes through. No business
// logic here - it only attaches the auth token and handles 401s
// (Frozen Principle #3 - Frontend never implements laboratory rules).
function getApiBaseUrl(): string {
  const envUrl = import.meta.env.VITE_API_BASE_URL || import.meta.env.VITE_API_URL;
  if (!envUrl) {
    return "http://localhost:5000/api";
  }
  const trimmed = String(envUrl).trim().replace(/\/+$/, "");
  return trimmed.endsWith("/api") ? trimmed : `${trimmed}/api`;
}

const baseURL = getApiBaseUrl();

export const apiClient = axios.create({ baseURL });

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
        return token;
      })
      .catch(() => null)
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
}

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetryableRequestConfig | undefined;
    const isAuthEndpoint = config?.url?.includes("/auth/refresh") || config?.url?.includes("/auth/login");

    if (error.response?.status !== 401 || !config || config._retry || isAuthEndpoint) {
      return Promise.reject(error);
    }

    config._retry = true;
    const newToken = await refreshAccessToken();
    if (!newToken) {
      clearAuthStorageAndRedirect();
      return Promise.reject(error);
    }

    config.headers.Authorization = `Bearer ${newToken}`;
    return apiClient(config);
  }
);
