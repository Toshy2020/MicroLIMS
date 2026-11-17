import { cloneElement, useId, type ReactElement, type MouseEvent } from "react";
import { Box, Typography, useTheme, type ButtonProps } from "@mui/material";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import { useTranslation } from "../i18n";
import type { ActionAvailability } from "../hooks/useActionAvailability";

export interface GatedActionProps {
  action: string;
  availability: ActionAvailability;
  // The real button. While availability is unknown it is rendered unchanged and the server decides on click.
  children: ReactElement<ButtonProps>;
  reasonPlacement?: "below" | "inline";
  // The one "Reasons are unavailable right now" notice. A screen that gates several buttons from the same
  // availability turns it off on all but one.
  notice?: boolean;
}

/** The notice shown when the reasons could not be loaded; the buttons stay usable. */
export function AvailabilityFailedNotice({ availability }: { availability: ActionAvailability }) {
  const { t } = useTranslation();
  const theme = useTheme();
  if (!availability.failed) return null;
  return (
    <Typography
      role="status"
      variant="caption"
      sx={{ display: "flex", alignItems: "center", gap: 0.5, color: theme.custom.status.pending.text }}
    >
      <InfoOutlinedIcon sx={{ fontSize: 14 }} aria-hidden />
      {t("gated.unavailable")}
    </Typography>
  );
}

/**
 * Wraps one action button with the server's answer about it. A refused action keeps its button focusable
 * (aria-disabled, not disabled) so a keyboard or screen-reader user reaches the reason, which is shown as
 * visible text and tied to the button with aria-describedby. The reason is the server's sentence and is
 * rendered as plain text.
 */
export function GatedAction({
  action,
  availability,
  children,
  reasonPlacement = "below",
  notice = true
}: GatedActionProps) {
  const theme = useTheme();
  const reasonId = useId();
  const entry = availability.get(action);
  const refused = entry !== null && !entry.allowed;

  let button = children;
  if (refused) {
    const own = children.props["aria-describedby"];
    button = cloneElement(children, {
      "aria-disabled": true,
      "aria-describedby": own ? `${own} ${reasonId}` : reasonId,
      onClick: (event: MouseEvent<HTMLButtonElement>) => {
        event.preventDefault();
        event.stopPropagation();
      },
      sx: [
        ...(Array.isArray(children.props.sx) ? children.props.sx : children.props.sx ? [children.props.sx] : []),
        { opacity: 0.65, cursor: "not-allowed" }
      ]
    });
  }

  const inline = reasonPlacement === "inline";
  return (
    <Box
      sx={{
        display: children.props.fullWidth ? "flex" : "inline-flex",
        flexDirection: inline ? "row" : "column",
        alignItems: inline ? "center" : "flex-start",
        gap: 0.5,
        maxWidth: "100%"
      }}
    >
      {button}
      {refused && entry.reason && (
        <Typography
          id={reasonId}
          variant="caption"
          sx={{ display: "flex", alignItems: "flex-start", gap: 0.5, color: theme.custom.status.pending.text }}
        >
          <LockOutlinedIcon sx={{ fontSize: 14, mt: "2px" }} aria-hidden />
          <span>{entry.reason}</span>
        </Typography>
      )}
      {refused && !entry.reason && <span id={reasonId} hidden />}
      {notice && <AvailabilityFailedNotice availability={availability} />}
    </Box>
  );
}
