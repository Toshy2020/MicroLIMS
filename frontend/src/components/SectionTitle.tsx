import { Box, Typography, Link, useTheme } from "@mui/material";

interface SectionTitleProps {
  children: string;
  tabs?: { label: string; onClick: () => void }[];
}

// .section-title from the design - purple heading with optional inline
// blue text-links (e.g. "Refresh").
export function SectionTitle({ children, tabs }: SectionTitleProps) {
  const theme = useTheme();
  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1.25, mt: 3.5, mb: 1.25 }}>
      <Typography sx={{ fontSize: 18, color: theme.palette.primary.main, fontWeight: 600 }}>{children}</Typography>
      {tabs?.map((t) => (
        <Link key={t.label} component="button" onClick={t.onClick} sx={{ fontSize: 13, ml: 0.75 }} underline="hover">
          {t.label}
        </Link>
      ))}
    </Box>
  );
}
