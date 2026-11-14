import { useRef, useState, useId } from "react";
import type { MouseEvent } from "react";
import { Box, Button, IconButton, Popover, Typography } from "@mui/material";
import HelpOutlineIcon from "@mui/icons-material/HelpOutlined";
import { HELP_GUIDANCE, useHelpPanel } from "../modules/help/HelpPanelContext";
import type { HelpTarget } from "../modules/help/HelpPanelContext";
import { useBenchMode } from "../theme/BenchModeContext";
import { BENCH_MIN_TARGET } from "../theme/benchTheme";

// A hint describes what a field means and what it does. It never states a
// limit, factor, count or time window: those come from the configured data on
// the screen. Hint text is taken from the matching guide chapter's wording.
export interface Hint {
  text: string;
  guide?: { chapterId: string; heading?: string };
}

const FIELD_HINT_TARGET = 24;

/**
 * The id of the element that describes a field's hint. A form passes
 * `aria-describedby={hintDescribedBy(id)}` on the same control that gets the
 * FieldHint's `id`. The hint text stays in the DOM (hidden) while the popover
 * is closed, so the reference is always valid.
 */
export function hintDescribedBy(id: string): string {
  return `${id}-hint`;
}

interface FieldHintProps {
  id: string;
  label: string;
  hint: Hint;
}

// Opens on click, Enter or Space (a native button). Hover does nothing.
export function FieldHint({ id, label, hint }: FieldHintProps) {
  const { benchMode } = useBenchMode();
  const { openPanel } = useHelpPanel();
  const buttonRef = useRef<HTMLButtonElement>(null);
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  // Focus goes back to the button when the popover closes, except when
  // "Read more" hands focus to the help panel.
  const [restoreFocus, setRestoreFocus] = useState(true);
  const titleId = useId();
  const size = benchMode ? BENCH_MIN_TARGET : FIELD_HINT_TARGET;
  const guide = hint.guide;

  const onOpen = (event: MouseEvent<HTMLButtonElement>) => {
    setRestoreFocus(true);
    setAnchor(event.currentTarget);
  };

  const onClose = () => {
    setAnchor(null);
    buttonRef.current?.focus();
  };

  const onReadMore = (target: HelpTarget) => {
    setRestoreFocus(false);
    setAnchor(null);
    openPanel(target);
  };

  return (
    <>
      <IconButton
        ref={buttonRef}
        aria-label={`About ${label}`}
        aria-haspopup="dialog"
        aria-expanded={Boolean(anchor)}
        onClick={onOpen}
        size="small"
        sx={{ minWidth: size, minHeight: size, flexShrink: 0 }}
      >
        <HelpOutlineIcon fontSize="small" />
      </IconButton>
      <Box component="span" id={hintDescribedBy(id)} hidden>
        {hint.text}
      </Box>
      <Popover
        open={Boolean(anchor)}
        anchorEl={anchor}
        onClose={onClose}
        disableRestoreFocus={!restoreFocus}
        anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
        transformOrigin={{ vertical: "top", horizontal: "left" }}
        slotProps={{
          paper: {
            role: "dialog",
            "aria-labelledby": titleId,
            sx: { maxWidth: 360, p: 2 }
          }
        }}
      >
        <Typography id={titleId} component="p" variant="subtitle2" sx={{ fontWeight: 600 }}>
          {label}
        </Typography>
        <Typography variant="body2" sx={{ mt: 1 }}>
          {hint.text}
        </Typography>
        <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
          {HELP_GUIDANCE}
        </Typography>
        {guide && (
          <Box sx={{ mt: 1 }}>
            <Button size="small" onClick={() => onReadMore(guide)} sx={{ minHeight: size }}>
              Read more
            </Button>
          </Box>
        )}
      </Popover>
    </>
  );
}
