# Third-Party Package Qualification: react-markdown

**Status:** Draft - awaiting approval

**Prepared by:** ____________________  **Date:** ____________
**Reviewed / Approved by:** ____________________  **Date:** ____________

## Package Details

- **Package Name:** `react-markdown`
- **Version:** `10.1.0` (exact pinned version in `frontend/package.json` and `frontend/package-lock.json`)
- **Licence:** `MIT`
- **Source:** npm registry (`https://registry.npmjs.org/react-markdown/-/react-markdown-10.1.0.tgz`)
- **Project Reference:** `frontend/package.json`; imported only by `frontend/src/modules/help/MarkdownView.tsx`

### Transitive Components

- The unified / remark / mdast / hast packages that `react-markdown` depends on (all MIT) are resolved to exact versions in `frontend/package-lock.json`. `rehype-raw` is **not** installed and must not be added: raw HTML in markdown stays disabled.

## Intended Use

- Renders the in-app user guide (`documentation/guide/*.md`) inside the Help panel and the full guide page (`MarkdownView`).
- Chapter text is loaded on demand from the build (`import.meta.glob(..., { query: "?raw" })`), so it is not part of the main application entry chunk.

## Excluded / Not Used For

- **GMP Impact Assessment:** Guidance text only. No GMP, workflow, specification, calculation or eligibility decision is taken from rendered guide content. In-app help is guidance; approved SOPs govern.
- **Risk - rendering untrusted-ish text:** Guide chapters are repository documents, but are treated as untrusted at render time. `react-markdown` is used with raw HTML disabled (its default), no `rehype-raw`, and no `dangerouslySetInnerHTML` anywhere in `frontend/src/modules/help/`. HTML or script in a chapter is therefore displayed as text and never becomes a DOM element. Links to other chapters are handled in-app; external links open with `target="_blank"` and `rel="noopener noreferrer"`; `javascript:` URLs are removed by the library's default URL sanitising.
- Not used to render user-entered, sample, result or other regulated data.

## Verification

- Verified in Vitest (`frontend/src/modules/help/MarkdownView.test.tsx`, `frontend/src/modules/help/content/helpContent.test.ts`, `frontend/scripts/copy-help-assets.test.mjs`):
  - `renders a script tag as text, not an element`
  - `renders an img with onerror as text, not an element`
  - `does not execute inline html, iframes or javascript: links`
  - `rewrites the image src, keeps alt, and lazy-loads`
  - `shifts headings so the chapter title is an h2`
  - `a chapter link calls onNavigate; an external link opens safely in a new tab`
  - `every menuConfig path has a HELP_MAP entry`, `every HELP_MAP chapterId exists in CHAPTERS`, `CHAPTERS has 44 entries numbered 1-44 with titles`
  - `fails and names the chapter and file when a chapter references a missing screenshot`
- Verified in production Vite build (`npm run build`): `dist/help/screenshots/` contains the guide screenshots, and the build fails when a chapter references a missing screenshot.

## Manual Verification Required Before Go-Live (Not Verified Automatically)

- [ ] Open the Help panel on a production build as each role and confirm chapters render with their screenshots, and that no guide chapter contains raw HTML that was intended to render (such content is shown as text by design).
