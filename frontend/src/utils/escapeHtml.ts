// Escapes a value for interpolation into the HTML built by the report PDF
// exports. A missing or empty value renders as an em dash.
export function escapeHtml(str: string | null | undefined): string {
  if (!str) return "—";
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}
