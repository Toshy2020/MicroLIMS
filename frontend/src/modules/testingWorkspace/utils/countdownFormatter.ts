/**
 * Formats seconds remaining into an exact, ticking countdown string:
 * e.g. "18h 24m 12s", "45m 03s", "12s", "0s".
 */
export function formatRemainingCountdown(seconds: number): string {
  if (seconds <= 0) return "0s";
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;
  if (hours > 0) {
    return `${hours}h ${minutes.toString().padStart(2, "0")}m ${secs.toString().padStart(2, "0")}s`;
  }
  if (minutes > 0) {
    return `${minutes}m ${secs.toString().padStart(2, "0")}s`;
  }
  return `${secs}s`;
}

/**
 * Formats seconds remaining into a compact format:
 * e.g. "18h 24m", "45m 03s", "12s", "0s".
 */
export function formatCountdownCompact(seconds: number): string {
  if (seconds <= 0) return "0s";
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const secs = seconds % 60;
  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  if (minutes > 0) {
    return `${minutes}m ${secs.toString().padStart(2, "0")}s`;
  }
  return `${secs}s`;
}
