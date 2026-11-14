import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import {
  Alert, Box, Button, IconButton, Link, List, ListItemButton, ListItemText, Skeleton, TextField, Typography, useMediaQuery
} from "@mui/material";
import { keyframes } from "@mui/material/styles";
import CloseIcon from "@mui/icons-material/Close";
import { Link as RouterLink, useLocation } from "react-router-dom";
import { chapterForPath } from "./content/helpMap";
import { loadChapterText } from "./content/guideLoader";
import { searchGuide } from "./content/searchIndex";
import { MarkdownView } from "./MarkdownView";
import { useHelpPanel } from "./HelpPanelContext";
import type { HelpPanelServices, HelpSearchResult, HelpTarget } from "./HelpPanelContext";
import { scrollToHeading } from "./scrollToHeading";
import { useChapterText } from "./useChapterText";

export const HELP_GUIDANCE = "Guidance only. Your approved SOPs take precedence.";
const PANEL_WIDTH = 420;
const SEARCH_DELAY_MS = 250;

const slideIn = keyframes`
  from { transform: translateX(24px); opacity: 0; }
  to { transform: translateX(0); opacity: 1; }
`;

type SearchState =
  | { status: "idle" }
  | { status: "searching" }
  | { status: "done"; query: string; results: HelpSearchResult[] }
  | { status: "error" };

export function HelpPanel({ services }: { services?: HelpPanelServices }) {
  const { closePanel } = useHelpPanel();
  const location = useLocation();
  const reducedMotion = useMediaQuery("(prefers-reduced-motion: reduce)");
  const loadChapter = services?.loadChapter ?? loadChapterText;
  const search = services?.search ?? searchGuide;
  const resolveTarget = services?.resolveTarget ?? chapterForPath;

  const headingRef = useRef<HTMLHeadingElement>(null);
  const bodyRef = useRef<HTMLDivElement>(null);

  // What the panel shows: the chapter for the page, until the reader picks
  // another; a route change resets it to the new page's chapter.
  const routeKey = `${location.pathname}${location.search}`;
  const [target, setTarget] = useState<HelpTarget>(() => resolveTarget(location.pathname, location.search));
  const [shownFor, setShownFor] = useState(routeKey);
  if (routeKey !== shownFor) {
    setShownFor(routeKey);
    setTarget(resolveTarget(location.pathname, location.search));
  }

  const { state, retry } = useChapterText(target.chapterId, loadChapter);

  useEffect(() => {
    headingRef.current?.focus();
  }, []);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !event.defaultPrevented) closePanel();
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [closePanel]);

  useLayoutEffect(() => {
    const body = bodyRef.current;
    if (state.status !== "ready" || !body) return;
    if (target.heading && scrollToHeading(body, target.heading, !reducedMotion)) return;
    body.scrollTop = 0;
  }, [state, target, reducedMotion]);

  // Search: debounced, latest query wins.
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<SearchState>({ status: "idle" });
  useEffect(() => {
    const text = query.trim();
    if (text.length < 2) {
      setFound({ status: "idle" });
      return;
    }
    let current = true;
    setFound({ status: "searching" });
    const timer = window.setTimeout(() => {
      search(text).then(
        (results) => {
          if (current) setFound({ status: "done", query: text, results });
        },
        () => {
          if (current) setFound({ status: "error" });
        }
      );
    }, SEARCH_DELAY_MS);
    return () => {
      current = false;
      window.clearTimeout(timer);
    };
  }, [query, search]);

  const openResult = useCallback((result: HelpSearchResult) => {
    setTarget({ chapterId: result.chapterId, heading: result.heading });
    setQuery("");
  }, []);

  const onNavigate = useCallback((chapterId: string, heading?: string) => {
    setTarget({ chapterId, heading });
  }, []);

  let announcement = "";
  if (found.status === "searching") announcement = "Searching";
  else if (found.status === "error") announcement = "Search failed. Try again.";
  else if (found.status === "done") {
    announcement =
      found.results.length === 0
        ? `No results for "${found.query}"`
        : `${found.results.length} result${found.results.length === 1 ? "" : "s"} for "${found.query}"`;
  }

  let content;
  if (found.status === "done" && found.results.length > 0) {
    content = (
      <List aria-label="Search results" dense disablePadding>
        {found.results.map((r) => (
          <li key={`${r.chapterId}:${r.heading}`}>
            <ListItemButton onClick={() => openResult(r)}>
              <ListItemText
                primary={`${r.title}: ${r.heading}`}
                secondary={r.excerpt}
                slotProps={{ secondary: { sx: { overflowWrap: "anywhere" } } }}
              />
            </ListItemButton>
          </li>
        ))}
      </List>
    );
  } else if (state.status === "loading") {
    content = (
      <Box role="status" aria-label="Loading chapter">
        <Skeleton variant="text" width="70%" height={36} />
        <Skeleton variant="text" />
        <Skeleton variant="text" />
        <Skeleton variant="rectangular" height={120} sx={{ my: 1 }} />
        <Skeleton variant="text" width="85%" />
      </Box>
    );
  } else if (state.status === "error") {
    content = (
      <Alert severity="error" action={<Button color="inherit" size="small" onClick={retry}>Retry</Button>}>
        This chapter could not be loaded.
      </Alert>
    );
  } else {
    content = <MarkdownView markdown={state.markdown} onNavigate={onNavigate} />;
  }

  return (
    <Box
      component="section"
      role="region"
      aria-labelledby="help-panel-heading"
      data-motion={reducedMotion ? "none" : "slide"}
      sx={(theme) => ({
        position: { xs: "absolute", md: "static" },
        inset: { xs: 0, md: "auto" },
        zIndex: { xs: theme.zIndex.drawer + 1, md: "auto" },
        flex: { md: `0 0 ${PANEL_WIDTH}px` },
        width: { xs: "100%", md: PANEL_WIDTH },
        height: "100%",
        display: "flex",
        flexDirection: "column",
        bgcolor: "background.paper",
        color: "text.primary",
        borderLeft: { md: 1 },
        borderColor: "divider",
        animation: reducedMotion ? "none" : `${slideIn} 180ms ease-out`
      })}
    >
      <Box sx={{ px: 2, pt: 1.5, pb: 1, borderBottom: 1, borderColor: "divider" }}>
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <Typography
            id="help-panel-heading"
            component="h2"
            variant="h6"
            tabIndex={-1}
            ref={headingRef}
            sx={{ outline: "none" }}
          >
            Help
          </Typography>
          <IconButton aria-label="Close help" onClick={closePanel} size="small">
            <CloseIcon fontSize="small" />
          </IconButton>
        </Box>
        <Typography variant="body2" sx={{ fontWeight: 600 }}>{HELP_GUIDANCE}</Typography>
        <Typography variant="caption" color="text.secondary" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {`Version ${__APP_VERSION__}`}
        </Typography>
        <TextField
          type="search"
          label="Search the guide"
          size="small"
          fullWidth
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          sx={{ mt: 1 }}
        />
        <Box role="status" aria-live="polite" sx={{ minHeight: 20, mt: 0.5 }}>
          <Typography variant="caption" color="text.secondary">{announcement}</Typography>
        </Box>
      </Box>

      <Box ref={bodyRef} sx={{ flex: 1, minHeight: 0, overflowY: "auto", px: 2, py: 1 }}>
        {content}
      </Box>

      <Box sx={{ px: 2, py: 1, borderTop: 1, borderColor: "divider" }}>
        <Link component={RouterLink} to={`/help/${target.chapterId}`}>
          Open the full guide
        </Link>
      </Box>
    </Box>
  );
}
