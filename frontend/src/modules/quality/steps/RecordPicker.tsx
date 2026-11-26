import { useEffect, useState } from "react";
import { Alert, List, ListItem, ListItemButton, ListItemText, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { globalSearchApi } from "../../../api/globalSearchApi";

const MIN_QUERY_LENGTH = 2;
const SEARCH_LIMIT = 10;
const SEARCH_DEBOUNCE_MS = 250;

export interface PickedRecord {
  // The global search's entity type (Item, Equipment, DocumentMaster, Sample, ...).
  entityType: string;
  id: number;
  title: string;
  subtitle: string;
}

interface RecordPickerProps {
  label: string;
  // Only hits of these entity types are offered.
  entityTypes: readonly string[];
  disabled?: boolean;
  onPick: (record: PickedRecord) => void;
}

type SearchState = "idle" | "loading" | "done" | "error";

// Chooses a laboratory record with the system's search: never typed in as free text, so the link always points at a
// record that exists and that the person may read.
export function RecordPicker({ label, entityTypes, disabled = false, onPick }: RecordPickerProps) {
  const { t } = useTranslation("quality");
  const [query, setQuery] = useState("");
  const [hits, setHits] = useState<PickedRecord[]>([]);
  const [state, setState] = useState<SearchState>("idle");
  const allowed = entityTypes.join("|");

  useEffect(() => {
    const text = query.trim();
    if (text.length < MIN_QUERY_LENGTH) {
      setHits([]);
      setState("idle");
      return undefined;
    }
    const controller = new AbortController();
    setState("loading");
    const handle = window.setTimeout(() => {
      globalSearchApi
        .search(text, undefined, SEARCH_LIMIT, controller.signal)
        .then((result) => {
          if (controller.signal.aborted) return;
          const found: PickedRecord[] = [];
          for (const category of result.categories) {
            for (const item of category.items) {
              if (allowed.split("|").includes(item.entityType)) {
                found.push({ entityType: item.entityType, id: item.id, title: item.title, subtitle: item.subtitle });
              }
            }
          }
          setHits(found);
          setState("done");
        })
        .catch(() => {
          if (controller.signal.aborted) return;
          setHits([]);
          setState("error");
        });
    }, SEARCH_DEBOUNCE_MS);
    return () => {
      window.clearTimeout(handle);
      controller.abort();
    };
  }, [query, allowed]);

  return (
    <div>
      <TextField
        size="small"
        fullWidth
        label={label}
        helperText={t("steps.picker.hint")}
        value={query}
        disabled={disabled}
        onChange={(e) => setQuery(e.target.value)}
      />
      <Typography role="status" variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
        {state === "loading" && t("steps.picker.searching")}
        {state === "done" && hits.length === 0 && t("steps.picker.noResults")}
        {state === "done" && hits.length > 0 && t("steps.picker.resultsCount", { count: hits.length })}
      </Typography>
      {state === "error" && (
        <Alert severity="error" role="alert" sx={{ mt: 1 }}>
          {t("steps.picker.error")}
        </Alert>
      )}
      {hits.length > 0 && (
        <List dense disablePadding sx={{ mt: 1, border: 1, borderColor: "divider", borderRadius: 1 }}>
          {hits.map((hit) => (
            <ListItem key={`${hit.entityType}-${hit.id}`} disablePadding>
              <ListItemButton
                disabled={disabled}
                aria-label={t("steps.picker.choose", { title: hit.title })}
                onClick={() => {
                  setQuery("");
                  onPick(hit);
                }}
              >
                <ListItemText primary={hit.title} secondary={hit.subtitle} />
              </ListItemButton>
            </ListItem>
          ))}
        </List>
      )}
    </div>
  );
}
