import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { MarkdownView } from "./MarkdownView";

function renderMarkdown(markdown: string, onNavigate = vi.fn()) {
  const view = render(
    <PinnedLightTheme>
      <MarkdownView markdown={markdown} onNavigate={onNavigate} />
    </PinnedLightTheme>
  );
  return { ...view, onNavigate };
}

describe("MarkdownView", () => {
  it("renders a script tag as text, not an element", () => {
    const { container } = renderMarkdown("Before <script>alert(1)</script> after");
    expect(container.querySelector("script")).toBeNull();
    expect(screen.getByText(/<script>alert\(1\)<\/script>/)).toBeTruthy();
  });

  it("renders an img with onerror as text, not an element", () => {
    const { container } = renderMarkdown('<img src=x onerror="alert(1)">');
    expect(container.querySelector("img")).toBeNull();
    expect(container.querySelector("[onerror]")).toBeNull();
    expect(screen.getByText(/onerror="alert\(1\)"/)).toBeTruthy();
  });

  it("does not execute inline html, iframes or javascript: links", () => {
    const { container } = renderMarkdown('<iframe src="https://evil.example"></iframe>\n\n<b onclick="x()">bold</b>');
    expect(container.querySelector("iframe")).toBeNull();
    expect(container.querySelector("b")).toBeNull();
    expect(container.querySelector("[onclick]")).toBeNull();
    const { container: c2 } = renderMarkdown("[click](javascript:alert(1))");
    expect(c2.querySelector('a[href^="javascript"]')).toBeNull();
  });

  it("rewrites the image src, keeps alt, and lazy-loads", () => {
    renderMarkdown("![Sign-in page](../screenshots/ss-051-sign-in.png)");
    const image = screen.getByRole("img", { name: "Sign-in page" });
    expect(image.getAttribute("src")).toBe("/help/screenshots/ss-051-sign-in.png");
    expect(image.getAttribute("loading")).toBe("lazy");
  });

  it("shifts headings so the chapter title is an h2", () => {
    renderMarkdown("## 12. Sample Receiving\n\n### 12.1 Receiving\n\n#### Purpose");
    expect(screen.getByRole("heading", { level: 2, name: "12. Sample Receiving" })).toBeTruthy();
    expect(screen.getByRole("heading", { level: 3, name: "12.1 Receiving" })).toBeTruthy();
    expect(screen.getByRole("heading", { level: 4, name: "Purpose" })).toBeTruthy();
    expect(screen.queryByRole("heading", { level: 1 })).toBeNull();
  });

  it("a chapter link calls onNavigate; an external link opens safely in a new tab", async () => {
    const user = userEvent.setup();
    const { onNavigate } = renderMarkdown("[OOS](20-oos-tracking.md#raising-an-oos) and [site](https://example.com/x)");

    await user.click(screen.getByRole("link", { name: "OOS" }));
    expect(onNavigate).toHaveBeenCalledWith("20-oos-tracking", "raising-an-oos");

    const external = screen.getByRole("link", { name: "site" });
    expect(external.getAttribute("target")).toBe("_blank");
    expect(external.getAttribute("rel")).toBe("noopener noreferrer");
    expect(external.getAttribute("href")).toBe("https://example.com/x");
  });
});
