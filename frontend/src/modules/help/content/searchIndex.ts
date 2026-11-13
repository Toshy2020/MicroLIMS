import { CHAPTERS } from "./guideLoader";

export interface GuideSearchResult {
  chapterId: string;
  title: string;
  heading: string;
  excerpt: string;
}

const MAX_RESULTS = 30;
const EXCERPT_RADIUS = 70;

// Plain text used for matching and excerpts: markdown syntax removed so a
// match never lands in a link target or an image path.
function plain(line: string): string {
  return line
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/<!--.*?-->/g, "")
    .replace(/[`*_>|]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

function excerptAround(text: string, index: number, length: number): string {
  const start = Math.max(0, index - EXCERPT_RADIUS);
  const end = Math.min(text.length, index + length + EXCERPT_RADIUS);
  return `${start > 0 ? "…" : ""}${text.slice(start, end).trim()}${end < text.length ? "…" : ""}`;
}

const cache = new Map<string, string>();

async function chapterText(id: string, load: () => Promise<string>): Promise<string> {
  const hit = cache.get(id);
  if (hit !== undefined) return hit;
  const text = await load();
  cache.set(id, text);
  return text;
}

// Case-insensitive search over the chapter text. Chapters load on demand (the
// first search fetches them; later searches reuse them). Returns the first
// match under each heading, with that heading and an excerpt.
export async function searchGuide(query: string): Promise<GuideSearchResult[]> {
  const needle = query.trim().toLowerCase();
  if (needle.length < 2) return [];
  const results: GuideSearchResult[] = [];
  for (const chapter of CHAPTERS) {
    const text = await chapterText(chapter.id, chapter.load);
    let heading = chapter.title;
    const seen = new Set<string>();
    for (const rawLine of text.split(/\r?\n/)) {
      const headingMatch = /^#{2,6}\s+(.*)$/.exec(rawLine);
      if (headingMatch) heading = plain(headingMatch[1]);
      const line = plain(rawLine);
      const index = line.toLowerCase().indexOf(needle);
      if (index < 0 || seen.has(heading)) continue;
      seen.add(heading);
      results.push({ chapterId: chapter.id, title: chapter.title, heading, excerpt: excerptAround(line, index, needle.length) });
      if (results.length >= MAX_RESULTS) return results;
    }
  }
  return results;
}
