import { useId, type ReactNode } from "react";
import { ListItemIcon, ListItemText, MenuItem, useTheme } from "@mui/material";
import type { ActionAvailability } from "../hooks/useActionAvailability";

export interface GatedMenuItemProps {
  action: string;
  availability: ActionAvailability;
  onSelect: () => void;
  label: string;
  icon?: ReactNode;
  // Typography of the label, so the item keeps the look it had before it was gated.
  labelSx?: object;
}

/**
 * A menu item gated by the server's answer. A refused item stays focusable (aria-disabled, not disabled),
 * ignores selection and shows the server's sentence as its secondary text, tied to it with aria-describedby.
 * While the answer is unknown the item works as before and the server decides on selection.
 */
export function GatedMenuItem({ action, availability, onSelect, label, icon, labelSx }: GatedMenuItemProps) {
  const theme = useTheme();
  const reasonId = useId();
  const entry = availability.get(action);
  const refused = entry !== null && !entry.allowed;

  return (
    <MenuItem
      aria-disabled={refused ? true : undefined}
      aria-describedby={refused && entry.reason ? reasonId : undefined}
      onClick={() => {
        if (!refused) onSelect();
      }}
      sx={refused ? { cursor: "not-allowed", alignItems: "flex-start", whiteSpace: "normal" } : undefined}
    >
      {icon && <ListItemIcon>{icon}</ListItemIcon>}
      <ListItemText
        primary={label}
        secondary={refused ? entry.reason : undefined}
        slotProps={{
          primary: { sx: { ...labelSx, ...(refused ? { opacity: 0.65 } : {}) } },
          secondary: { id: reasonId, sx: { color: theme.custom.status.pending.text, fontSize: 12 } }
        }}
      />
    </MenuItem>
  );
}
