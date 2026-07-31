import { Box, Button } from "@mui/material";
import { ReactNode } from "react";
import { brandColors } from "../theme";

interface ToolbarAction {
  label: string;
  icon?: string;
  onClick: () => void;
}

// .toolbar from the design: light-green rounded strip of text buttons.
export function Toolbar({ actions }: { actions: ToolbarAction[] | ReactNode }) {
  if (!Array.isArray(actions)) {
    return <Box sx={{ display: "flex", gap: 1, mb: 2 }}>{actions}</Box>;
  }
  return (
    <Box sx={{ background: brandColors.toolbarBg, borderRadius: 1.5, px: 2, py: 1.25, display: "flex", gap: 3, mb: 1.75 }}>
      {actions.map((a) => (
        <Button
          key={a.label}
          onClick={a.onClick}
          disableRipple
          sx={{ color: brandColors.toolbarText, fontSize: 13, fontWeight: 600, minWidth: 0, p: 0, "&:hover": { textDecoration: "underline", bgcolor: "transparent" } }}
        >
          {a.icon ? `${a.icon} ` : ""}{a.label}
        </Button>
      ))}
    </Box>
  );
}
