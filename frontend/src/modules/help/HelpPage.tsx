import { useCallback, useLayoutEffect, useRef } from "react";
import { Alert, Box, Button, Link, List, ListItem, Skeleton, Typography } from "@mui/material";
import { Link as RouterLink, useLocation, useNavigate, useParams } from "react-router-dom";
import { CHAPTERS, loadChapterText } from "./content/guideLoader";
import { MarkdownView } from "./MarkdownView";
import { scrollToHeading } from "./scrollToHeading";
import { useChapterText } from "./useChapterText";
import { BreadcrumbLabel } from "../../components/breadcrumbContext";
import { useTranslation } from "../../i18n";

interface HelpPageProps {
  loadChapter?: (chapterId: string) => Promise<string>;
}

interface ChapterViewProps {
  chapterId: string;
  title: string;
  loadChapter: (chapterId: string) => Promise<string>;
}

function ChapterView({ chapterId, title, loadChapter }: ChapterViewProps) {
  const { t } = useTranslation("help");
  const navigate = useNavigate();
  const location = useLocation();
  const bodyRef = useRef<HTMLDivElement>(null);
  const { state, retry } = useChapterText(chapterId, loadChapter);
  const heading = (location.state as { heading?: string } | null)?.heading;

  useLayoutEffect(() => {
    if (state.status === "ready" && heading && bodyRef.current) scrollToHeading(bodyRef.current, heading, false);
  }, [state, heading]);

  const onNavigate = useCallback(
    (id: string, target?: string) => navigate(`/help/${id}`, { state: target ? { heading: target } : null }),
    [navigate]
  );

  return (
    <Box>
      <Link component={RouterLink} to="/help">{t("page.allChapters")}</Link>
      <Box ref={bodyRef} sx={{ mt: 1, maxWidth: 900 }}>
        {state.status === "loading" && (
          <Box role="status" aria-label={t("page.loadingAria", { title })}>
            <Skeleton variant="text" width="60%" height={40} />
            <Skeleton variant="text" />
            <Skeleton variant="text" />
            <Skeleton variant="rectangular" height={160} sx={{ my: 1 }} />
          </Box>
        )}
        {state.status === "error" && (
          <Alert severity="error" action={<Button color="inherit" size="small" onClick={retry}>{t("shared.retry")}</Button>}>
            {t("shared.chapterLoadFailed")}
          </Alert>
        )}
        {state.status === "ready" && <MarkdownView markdown={state.markdown} onNavigate={onNavigate} />}
      </Box>
    </Box>
  );
}

export function HelpPage({ loadChapter = loadChapterText }: HelpPageProps) {
  const { t } = useTranslation(["common", "help"]);
  const { chapterId } = useParams();
  const chapter = chapterId ? CHAPTERS.find((c) => c.id === chapterId) : undefined;

  return (
    <Box component="section" aria-label={t("help:page.userGuide")}>
      {/* The chapter list needs no crumb of its own; a chapter (or an unknown one) names the last crumb. */}
      {chapterId !== undefined && <BreadcrumbLabel label={chapter ? `${chapter.number}. ${chapter.title}` : t("help.chapterNotFound")} />}
      <Typography component="h1" variant="h4" sx={{ mb: 0.5 }}>{t("help:page.userGuide")}</Typography>
      <Typography variant="body2" sx={{ fontWeight: 600, mb: 2 }}>{t("help:shared.guidance")}</Typography>
      {chapterId === undefined ? (
        <List aria-label={t("help:page.chaptersAria")} disablePadding>
          {CHAPTERS.map((c) => (
            <ListItem key={c.id} disableGutters sx={{ py: 0.25 }}>
              <Link component={RouterLink} to={`/help/${c.id}`}>
                {`${c.number}. ${c.title}`}
              </Link>
            </ListItem>
          ))}
        </List>
      ) : chapter ? (
        <ChapterView key={chapter.id} chapterId={chapter.id} title={chapter.title} loadChapter={loadChapter} />
      ) : (
        <Box>
          <Typography component="h2" variant="h5" sx={{ mb: 1 }}>{t("help:page.notFoundTitle")}</Typography>
          <Typography sx={{ mb: 1 }}>{t("help:page.notFoundMessage", { id: chapterId })}</Typography>
          <Link component={RouterLink} to="/help">{t("help:page.backToChapters")}</Link>
        </Box>
      )}
    </Box>
  );
}
