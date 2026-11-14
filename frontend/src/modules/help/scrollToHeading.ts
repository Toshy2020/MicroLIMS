function normalise(text: string): string {
  return text.replace(/\s+/g, " ").trim().toLowerCase();
}

// Scrolls the heading whose text matches into view; exact match first, then
// "contains". Returns whether a heading was found.
export function scrollToHeading(container: HTMLElement, heading: string, smooth: boolean): boolean {
  const wanted = normalise(heading);
  if (!wanted) return false;
  const headings = Array.from(container.querySelectorAll<HTMLElement>("h2, h3, h4, h5, h6"));
  const target =
    headings.find((h) => normalise(h.textContent ?? "") === wanted) ??
    headings.find((h) => normalise(h.textContent ?? "").includes(wanted));
  if (!target || typeof target.scrollIntoView !== "function") return false;
  target.scrollIntoView({ block: "start", behavior: smooth ? "smooth" : "auto" });
  return true;
}
