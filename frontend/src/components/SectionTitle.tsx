import { Box, Typography, Link as MuiLink, useTheme } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";

interface SectionTitleProps {
  children: string;
  tabs?: { label: string; onClick?: () => void; to?: string }[];
}

// .section-title from the design - purple heading with optional inline
// blue text-links (e.g. "Refresh" or route links like "View all").
export function SectionTitle({ children, tabs }: SectionTitleProps) {
  const theme = useTheme();
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1.25, mt: 3.5, mb: 1.25 }}>
      <Typography sx={{ fontSize: 18, color: theme.palette.primary.main, fontWeight: 600 }}>{children}</Typography>
      {tabs?.map((t) =>
        t.to ? (
          <MuiLink
            key={t.label}
            component={RouterLink}
            to={t.to}
            sx={{ fontSize: 13, ml: 0.75 }}
            underline="hover"
          >
            {t.label}
          </MuiLink>
        ) : (
          <MuiLink
            key={t.label}
            component="button"
            type="button"
            onClick={t.onClick}
            sx={{ fontSize: 13, ml: 0.75 }}
            underline="hover"
          >
            {t.label}
          </MuiLink>
        )
      )}
    </Box>
  );
}
