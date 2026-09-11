import { Typography, Box, useTheme } from "@mui/material";

// h1.page-title + p.subtitle from the design.
export function PageHeader({
  title,
  subtitle,
  children
}: {
  title: string;
  subtitle?: string;
  children?: React.ReactNode;
}) {
  const theme = useTheme();
  return (
    <Box sx={{ mb: 1, display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
      <Box>
        <Typography component="h1" variant="h5" sx={{ fontWeight: 700, color: theme.palette.primary.main, mb: 0.5 }}>{title}</Typography>
        {subtitle && <Typography sx={{ color: "text.secondary", mb: 1, fontSize: 14 }}>{subtitle}</Typography>}
      </Box>
      {children && <Box>{children}</Box>}
    </Box>
  );
}
