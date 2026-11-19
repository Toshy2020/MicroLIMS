import { useMemo } from "react";
import type { ComponentPropsWithoutRef, ReactNode } from "react";
import Markdown from "react-markdown";
import type { Components } from "react-markdown";
// GitHub table syntax: 12 guide chapters use tables. remark-gfm only adds
// markdown syntax (tables, autolinks, strikethrough); raw HTML stays disabled.
import remarkGfm from "remark-gfm";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";

export interface MarkdownViewProps {
  markdown: string;
  onNavigate(chapterId: string, heading?: string): void;
}

// Guide text is rendered with raw HTML disabled (react-markdown's default;
// rehype-raw is deliberately not installed) so markup or script in a chapter
// is shown as text and never becomes an element. No dangerouslySetInnerHTML.
const SCREENSHOT = /^(?:\.\.\/|\.\/)*screenshots\/(.+)$/;
const CHAPTER_LINK = /^(?:\.\/)?(\d\d-[a-z0-9-]+)(?:\.md)?(?:#(.*))?$/i;

export function rewriteImageSrc(src: string): string {
  const match = SCREENSHOT.exec(src);
  return match ? `/help/screenshots/${match[1]}` : src;
}

const REMARK_PLUGINS = [remarkGfm];

type HeadingTag ="h2" | "h3" | "h4" | "h5" | "h6";
type HeadingVariant = "h5" | "h6" | "subtitle1" | "subtitle2";

// The chapter title (## N. Title) becomes h2 and deeper levels follow, capped
// at h6. The panel supplies the h1 above, so nothing here is an h1.
function headingRenderer(tag: HeadingTag, variant: HeadingVariant) {
  return function GuideHeading({ children }: { children?: ReactNode } & ComponentPropsWithoutRef<"h2">) {
    return (
      <Typography component={tag} variant={variant} sx={{ mt: 2, mb: 1, fontWeight: 600 }}>
        {children}
      </Typography>
    );
  };
}

const HEADINGS = {
  h1: headingRenderer("h2", "h5"),
  h2: headingRenderer("h2", "h5"),
  h3: headingRenderer("h3", "h6"),
  h4: headingRenderer("h4", "subtitle1"),
  h5: headingRenderer("h5", "subtitle2"),
  h6: headingRenderer("h6", "subtitle2")
};

export function MarkdownView({ markdown, onNavigate }: MarkdownViewProps) {
  const components = useMemo<Components>(
    () => ({
      ...HEADINGS,
      a({ href, children }) {
        const chapter = href ? CHAPTER_LINK.exec(href) : null;
        if (chapter) {
          return (
            <a
              href={`/help/${chapter[1]}`}
              onClick={(event) => {
                event.preventDefault();
                onNavigate(chapter[1], chapter[2] || undefined);
              }}
            >
              {children}
            </a>
          );
        }
        return (
          <a href={href} target="_blank" rel="noopener noreferrer">
            {children}
          </a>
        );
      },
      img({ src, alt }) {
        return (
          <Box
            component="img"
            src={typeof src === "string" ? rewriteImageSrc(src) : undefined}
            alt={alt ?? ""}
            loading="lazy"
            sx={{ maxWidth: "100%", height: "auto", display: "block", my: 1 }}
          />
        );
      }
    }),
    [onNavigate]
  );

  return (
    <Box
      sx={{
        overflowWrap: "anywhere",
        "& table": { borderCollapse: "collapse", display: "block", overflowX: "auto" },
        "& th, & td": { border: 1, borderColor: "divider", px: 1, py: 0.5, textAlign: "left" },
        "& code": { fontFamily: "monospace" },
        "& pre": { overflowX: "auto" }
      }}
    >
      <Markdown remarkPlugins={REMARK_PLUGINS} components={components}>{markdown}</Markdown>
    </Box>
  );
}
