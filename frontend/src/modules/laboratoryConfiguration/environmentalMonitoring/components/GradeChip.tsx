import { Box } from "@mui/material";

// Cleanroom grade badge - darker for the more critical grades.
export function GradeChip({ grade }: { grade: string }) {
  const strong = grade === "A" || grade === "B";
  return (
    <Box
      component="span"
      sx={{
        display: "inline-flex",
        px: 1,
        py: "2px",
        borderRadius: 1,
        fontSize: 12,
        fontWeight: 700,
        whiteSpace: "nowrap",
        color: strong ? "primary.contrastText" : "primary.main",
        bgcolor: grade === "A" ? "primary.dark" : grade === "B" ? "primary.main" : "action.selected",
        border: "1px solid",
        borderColor: strong ? "transparent" : "primary.light"
      }}
    >
      Grade {grade || "?"}
    </Box>
  );
}
