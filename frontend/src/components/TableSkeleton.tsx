import { Table, TableBody, TableCell, TableHead, TableRow, Skeleton, Paper, TableContainer } from "@mui/material";
import { tableHeadSx } from "../theme";
import { useTheme } from "@mui/material/styles";

interface TableSkeletonProps {
  rows?: number;
  cols?: number;
  hasContainer?: boolean;
}

export function TableSkeleton({ rows = 5, cols = 5, hasContainer = true }: TableSkeletonProps) {
  const theme = useTheme();

  const content = (
    <Table size="small">
      <TableHead>
        <TableRow sx={tableHeadSx(theme)}>
          {Array.from({ length: cols }).map((_, colIdx) => (
            <TableCell key={`th-${colIdx}`}>
              <Skeleton variant="text" width={colIdx === 0 ? "70%" : "50%"} height={24} />
            </TableCell>
          ))}
        </TableRow>
      </TableHead>
      <TableBody>
        {Array.from({ length: rows }).map((_, rowIdx) => (
          <TableRow key={`tr-${rowIdx}`}>
            {Array.from({ length: cols }).map((_, colIdx) => (
              <TableCell key={`td-${rowIdx}-${colIdx}`} sx={{ py: 1.5 }}>
                <Skeleton
                  variant="rounded"
                  width={colIdx === 0 ? "85%" : colIdx === cols - 1 ? "40%" : "60%"}
                  height={20}
                />
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );

  if (hasContainer) {
    return (
      <TableContainer component={Paper} elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2 }}>
        {content}
      </TableContainer>
    );
  }

  return content;
}
