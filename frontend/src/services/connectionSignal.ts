// apiClient raises this event when a request fails with no HTTP answer, so the
// connection status hook can flip to offline without apiClient depending on React.
export const NETWORK_FAILURE_EVENT = "microlims:network-failure";

export function notifyNetworkFailure(): void {
  window.dispatchEvent(new Event(NETWORK_FAILURE_EVENT));
}
