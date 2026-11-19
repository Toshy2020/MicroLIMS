import axios from "axios";

// True when a request failed without any HTTP answer (no network, DNS, timeout,
// server unreachable). An HTTP error status such as 500 is an answer, so it is
// not a network error. A cancelled request is not one either.
export function isNetworkError(error: unknown): boolean {
  return axios.isAxiosError(error) && !error.response && !axios.isCancel(error);
}
