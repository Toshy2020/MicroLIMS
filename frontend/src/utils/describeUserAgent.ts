/** Returns a concise browser and OS label for display in trusted-device lists. */
export function describeUserAgent(ua: string | null | undefined): string {
  if (!ua || !ua.trim()) return "Unknown browser";
  const browser =
    /Edg\//i.test(ua) ? "Edge"
      : /Chrome\//i.test(ua) ? "Chrome"
        : /Firefox\//i.test(ua) ? "Firefox"
          : /Safari\//i.test(ua) ? "Safari"
            : "Browser";
  const os =
    /Windows/i.test(ua) ? "Windows"
      : /Mac OS X|Macintosh/i.test(ua) ? "macOS"
        : /Linux/i.test(ua) ? "Linux"
          : /Android/i.test(ua) ? "Android"
            : /iPhone|iPad/i.test(ua) ? "iOS"
              : null;
  return os ? `${browser} on ${os}` : browser;
}
