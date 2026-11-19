// The user guide chapters (documentation/guide/NN-*.md), loaded lazily: the
// glob is not eager, so chapter text is split into per-chapter chunks and
// stays out of the main bundle. REVIEW_GUIDE_BRIEF.md, WRITING_BRIEF.md and
// _ss/ do not match the NN-*.md pattern.
export interface GuideChapter {
  /** File name without .md, e.g. "12-sample-receiving". */
  id: string;
  number: number;
  title: string;
  load(): Promise<string>;
}

const loaders = import.meta.glob("../../../../../documentation/guide/[0-9][0-9]-*.md", {
  query: "?raw",
  import: "default"
}) as Record<string, () => Promise<string>>;

// The chapter title is the first "## N. Title" line (chapters have no H1).
// Kept as a table so the chapter list renders without loading any chapter;
// guideLoader.test.ts checks every entry against the chapter file.
const TITLES: Record<string, string> = {
  "01": "Introduction",
  "02": "Purpose and Scope",
  "03": "System Overview",
  "04": "User Roles and Permissions",
  "05": "Login, Session and Password",
  "06": "User Interface",
  "07": "Navigation",
  "08": "Dashboard",
  "09": "Notifications, Messages and Discussions",
  "10": "Search and Filters",
  "11": "Sample Management and Tracking Board",
  "12": "Sample Receiving",
  "13": "Test Orders and Analyst Assignment",
  "14": "Analyst Testing Workspace and Sample Preparation",
  "15": "Result Entry (count, observation and location-grid tests)",
  "16": "Pathogen Testing",
  "17": "Review",
  "18": "Return to Analyst, Rework and Retest",
  "19": "Approval",
  "20": "OOS Tracking",
  "21": "Sample Report, Certificate of Analysis and Archived Records",
  "22": "Growth Promotion Testing and Media",
  "23": "Environmental Monitoring",
  "24": "Water Monitoring",
  "25": "After Cleaning Monitoring",
  "26": "Reference Strains (Reference Cryovials)",
  "27": "Physicochemical Testing",
  "28": "Solution and Standard Preparation",
  "29": "Material Stock",
  "30": "Equipment",
  "31": "Item Master, Test Master and Specifications",
  "32": "Laboratory Configuration (Organisms, Receiving Configuration, Master Data Workbook)",
  "33": "Laboratories, Departments and Data Segregation",
  "34": "Reports, KPIs and Trend Analysis",
  "35": "Audit Trail",
  "36": "Traceability",
  "37": "Electronic Signatures and Approval Controls",
  "38": "Document Control",
  "39": "Users, Roles and Administration",
  "40": "Error Monitoring (Incidents and Error Log)",
  "41": "Integrations",
  "42": "Troubleshooting",
  "43": "Frequently Asked Questions",
  "44": "Glossary",
  "45": "My Work"
};

// Chapters carry HTML comments for authors (traceability markers such as
// "<!-- WF-AUTH-001 -->" and review logs, see WRITING_BRIEF.md). The renderer
// shows raw HTML as text, so comments are removed before a chapter is shown.
export function stripAuthorComments(markdown: string): string {
  return markdown.replace(/<!--[\s\S]*?-->/g, "");
}

export const CHAPTERS: GuideChapter[] = Object.keys(loaders)
  .map((file) => {
    const id = file.slice(file.lastIndexOf("/") + 1).replace(/\.md$/, "");
    const number = Number(id.slice(0, 2));
    const load = () => loaders[file]().then(stripAuthorComments);
    return { id, number, title: TITLES[id.slice(0, 2)] ?? id, load };
  })
  .sort((a, b) => a.number - b.number);

export async function loadChapterText(chapterId: string): Promise<string> {
  const chapter = CHAPTERS.find((c) => c.id === chapterId);
  if (!chapter) throw new Error(`Unknown guide chapter ${chapterId}`);
  return chapter.load();
}

export function chapterTitleFromMarkdown(markdown: string): string | null {
  const match = /^##\s+\d+\.\s+(.+)$/m.exec(markdown);
  return match ? match[1].trim() : null;
}
