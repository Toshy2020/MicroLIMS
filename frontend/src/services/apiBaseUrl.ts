// Where the API lives. Kept apart from apiClient so small services (the health
// check) can use the address without pulling in the request interceptors.
export function getApiBaseUrl(): string {
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

// The server origin without the /api prefix: where anonymous, non-API
// endpoints such as /health live.
export const apiOrigin = getApiBaseUrl().replace(/\/api$/, "");
