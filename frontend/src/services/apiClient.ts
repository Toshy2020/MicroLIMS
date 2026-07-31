import axios from "axios";

// Single axios instance every module service goes through. No business
// logic here - it only attaches the auth token and handles 401s
// (Frozen Principle #3 - Frontend never implements laboratory rules).
export const apiClient = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL ?? "http://localhost:65435/api"
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem("microlims_token");
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem("microlims_token");
      window.location.href = "/login";
    }
    return Promise.reject(error);
  }
);
