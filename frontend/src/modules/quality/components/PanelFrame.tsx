import { useId } from "react";
import type { ReactNode } from "react";
import { Box, Paper } from "@mui/material";
import { SectionTitle } from "../../../components/SectionTitle";

interface PanelFrameProps {
  title: string;
  // A control that belongs to the whole panel (for example "Add link"), shown beside the title.
  action?: ReactNode;
  children: ReactNode;
}

// The frame every panel of the quality record page shares: a labelled section with a heading.
export function PanelFrame({ title, action, children }: PanelFrameProps) {
  const headingId = useId();
  return (
    <Paper variant="outlined" component="section" aria-labelledby={headingId} sx={{ p: 2.5, borderRadius: 1.5 }}>
      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 1, mb: 1.5 }}>
        <Box id={headingId} sx={{ "& > div": { my: 0 } }}>
          <SectionTitle>{title}</SectionTitle>
        </Box>
        {action}
      </Box>
      {children}
    </Paper>
  );
}
