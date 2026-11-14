import axios from "axios";
import { apiOrigin } from "./apiBaseUrl";

// GET {apiOrigin}/health: anonymous and outside /api. A bare axios call, not
// apiClient: no token, no refresh handling, and a failure here must not itself
// be reported as a request failure. Resolves when the server answered; rejects
// on a network error or an HTTP error status.
export const healthService = {
  async check(signal?: AbortSignal): Promise<void> {
    await axios.get(`${apiOrigin}/health`, { signal, timeout: 5000 });
  }
};
