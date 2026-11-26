import { useEffect, useState } from "react";
import { Alert, Box, Button, IconButton, List, ListItem, ListItemButton, ListItemText, Stack, TextField, Typography } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Link as RouterLink } from "react-router-dom";
import { useTranslation } from "../../../i18n";
import { MetaBadge, ToneBadge } from "../../../components/StatusBadge";
import { monospaceFontFamily } from "../../../theme/palette";
import { serverMessage } from "../../../utils/errorMessage";
import { globalSearchApi } from "../../../api/globalSearchApi";
import { qualityService, type QualityLinkDto, type QualityLinkTargetType } from "../../../services/qualityService";
import { PanelFrame } from "./PanelFrame";

// What the global search calls a kind of record, and which quality link target it is. A search hit of any
// other kind (a solution preparation, a material) cannot be linked and is not offered.
const TARGET_BY_ENTITY_TYPE: Record<string, QualityLinkTargetType> = {
  Sample: "Sample",
  TestOrder: "TestOrder",
  Equipment: "Equipment",
  Media: "MediaLot",
  DocumentMaster: "DocumentMaster",
  Item: "Item"
};

const MIN_QUERY_LENGTH = 2;
const SEARCH_LIMIT = 10;
const SEARCH_DEBOUNCE_MS = 250;

interface Candidate {
  key: string;
  targetType: QualityLinkTargetType;
  targetId: number;
  title: string;
  subtitle: string;
}

type SearchState = "idle" | "loading" | "done" | "error";

interface LinksPanelProps {
  recordId: number;
  recordVersion: number;
  // null when the links could not be loaded (the page shows the partial-outage banner).
  links: QualityLinkDto[] | null;
  // Quality.Raise: add and remove links. Without it the panel only lists them.
  canEdit: boolean;
  onChanged: () => Promise<void>;
}

export function LinksPanel({ recordId, recordVersion, links, canEdit, onChanged }: LinksPanelProps) {
  const { t } = useTranslation("quality");
  const [searchOpen, setSearchOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [candidates, setCandidates] = useState<Candidate[]>([]);
  const [searchState, setSearchState] = useState<SearchState>("idle");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const text = query.trim();
    if (!searchOpen || text.length < MIN_QUERY_LENGTH) {
      setCandidates([]);
      setSearchState("idle");
      return undefined;
    }

    const controller = new AbortController();
    setSearchState("loading");
    const handle = window.setTimeout(() => {
      globalSearchApi
        .search(text, undefined, SEARCH_LIMIT, controller.signal)
        .then((result) => {
          if (controller.signal.aborted) return;
          const found: Candidate[] = [];
          for (const category of result.categories) {
            for (const item of category.items) {
              const targetType = TARGET_BY_ENTITY_TYPE[item.entityType];
              if (targetType) {
                found.push({
                  key: `${item.entityType}-${item.id}`,
                  targetType,
                  targetId: item.id,
                  title: item.title,
                  subtitle: item.subtitle
                });
              }
            }
          }
          setCandidates(found);
          setSearchState("done");
        })
        .catch(() => {
          if (controller.signal.aborted) return;
          setCandidates([]);
          setSearchState("error");
        });
    }, SEARCH_DEBOUNCE_MS);

    return () => {
      window.clearTimeout(handle);
      controller.abort();
    };
  }, [query, searchOpen]);

  const closeSearch = () => {
    setSearchOpen(false);
    setQuery("");
    setCandidates([]);
    setSearchState("idle");
  };

  const addLink = async (candidate: Candidate) => {
    setBusy(true);
    setError(null);
    try {
      await qualityService.addLink(recordId, { targetType: candidate.targetType, targetId: candidate.targetId }, recordVersion);
      closeSearch();
      await onChanged();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("links.changeFailed"));
    } finally {
      setBusy(false);
    }
  };

  const removeLink = async (link: QualityLinkDto) => {
    setBusy(true);
    setError(null);
    try {
      await qualityService.removeLink(recordId, link.id, recordVersion);
      await onChanged();
    } catch (e: unknown) {
      setError(serverMessage(e) ?? t("links.changeFailed"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <PanelFrame
      title={t("links.title")}
      action={
        canEdit && !searchOpen ? (
          <Button size="small" variant="outlined" onClick={() => setSearchOpen(true)}>
            {t("links.add")}
          </Button>
        ) : undefined
      }
    >
      <Stack spacing={1.5}>
        {error && (
          <Alert severity="error" role="alert">
            {error}
          </Alert>
        )}

        {canEdit && searchOpen && (
          <Box>
            <Stack direction="row" spacing={1} sx={{ alignItems: "flex-start" }}>
              <TextField
                size="small"
                fullWidth
                autoFocus
                label={t("links.searchLabel")}
                helperText={t("links.searchHint")}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                disabled={busy}
              />
              <IconButton aria-label={t("links.close")} onClick={closeSearch}>
                <CloseIcon />
              </IconButton>
            </Stack>
            <Typography role="status" variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
              {searchState === "loading" && t("links.searching")}
              {searchState === "done" && candidates.length === 0 && t("links.noResults")}
              {searchState === "done" && candidates.length > 0 && t("links.resultsCount", { count: candidates.length })}
            </Typography>
            {searchState === "error" && (
              <Alert severity="error" role="alert" sx={{ mt: 1 }}>
                {t("links.searchError")}
              </Alert>
            )}
            {candidates.length > 0 && (
              <List dense disablePadding sx={{ mt: 1, border: 1, borderColor: "divider", borderRadius: 1 }}>
                {candidates.map((candidate) => (
                  <ListItem key={candidate.key} disablePadding>
                    <ListItemButton
                      disabled={busy}
                      aria-label={t("links.linkThis", { title: candidate.title })}
                      onClick={() => void addLink(candidate)}
                    >
                      <ListItemText primary={candidate.title} secondary={candidate.subtitle} />
                      <MetaBadge label={t(`links.targetTypes.${candidate.targetType}`)} />
                    </ListItemButton>
                  </ListItem>
                ))}
              </List>
            )}
          </Box>
        )}

        {links === null ? (
          <Typography variant="body2" color="text.secondary">
            {t("links.unavailable")}
          </Typography>
        ) : links.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("links.empty")}
          </Typography>
        ) : (
          <List disablePadding>
            {links.map((link) => {
              const number = link.number ?? String(link.id);
              return (
                <ListItem
                  key={link.id}
                  divider
                  secondaryAction={
                    canEdit ? (
                      <IconButton
                        edge="end"
                        disabled={busy}
                        aria-label={t("links.remove", { number })}
                        onClick={() => void removeLink(link)}
                      >
                        <DeleteOutlinedIcon />
                      </IconButton>
                    ) : undefined
                  }
                >
                  <ListItemText
                    disableTypography
                    primary={
                      <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                        {link.isRestricted || !link.route ? (
                          <Typography component="span" variant="body2" sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}>
                            {number}
                          </Typography>
                        ) : (
                          <Typography
                            component={RouterLink}
                            to={link.route}
                            variant="body2"
                            sx={{ fontFamily: monospaceFontFamily, fontWeight: 700 }}
                          >
                            {number}
                          </Typography>
                        )}
                        {link.isRestricted && <ToneBadge tone="pending" label={t("links.restricted")} />}
                        {!link.isRestricted && link.label && (
                          <Typography component="span" variant="body2">
                            {link.label}
                          </Typography>
                        )}
                      </Stack>
                    }
                    secondary={
                      link.isRestricted ? (
                        <Typography variant="caption" color="text.secondary">
                          {t("links.restrictedHelp")}
                        </Typography>
                      ) : undefined
                    }
                  />
                </ListItem>
              );
            })}
          </List>
        )}
      </Stack>
    </PanelFrame>
  );
}
