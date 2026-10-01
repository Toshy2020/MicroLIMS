import axios from "axios";

// The message to show a laboratory user for a failed request: the server's
// own explanation when it sent one (business-rule messages are written for
// analysts), else the readable transport message apiClient puts on the
// error, else the caller's fallback. Never a stack trace or exception name.
export function getErrorMessage(err: unknown, fallback: string): string {
  if (!axios.isAxiosError(err)) return fallback;
  const serverMessage = (err.response?.data as { message?: unknown } | undefined)?.message;
  if (typeof serverMessage === "string" && serverMessage.trim()) return serverMessage;
  return err.message?.trim() ? err.message : fallback;
}
