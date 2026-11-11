import { Box, Typography, Link as MuiLink, useTheme } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";

interface SectionTitleProps {
  children: string;
  tabs?: { label: string; onClick?: () => void; to?: string }[];
}

/**
 * @component SectionTitle
 * @status Stable
 * @purpose `.section-title` from the design - purple heading with `3px` left accent bar and optional inline
 * blue text-links (e.g. "Refresh" or route links like "View all").
 * @tokens Uses `theme.palette.primary.main` (`-0.01em` optical tracking).
 * @a11y Renders a sequential `<h2>` section heading; inline actions render as keyboard-operable `<button type="button">` or `<Link>`.
 */
export function SectionTitle({ children, tabs }: SectionTitleProps) {
  const theme = useTheme();
  return (
    <Box sx={{ display: "flex", alignItems: "center", flexWrap: "wrap", gap: 1.25, mt: 3, mb: 1.25 }}>
      <Typography
        component="h2"
        variant="h6"
        sx={{
          color: theme.palette.primary.main,
          letterSpacing: "-0.01em",
          borderLeft: `3px solid ${theme.palette.primary.main}`,
          pl: 1.25,
          lineHeight: 1.25
        }}
      >
        {children}
      </Typography>
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
