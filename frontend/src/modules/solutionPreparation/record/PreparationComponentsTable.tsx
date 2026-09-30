import {
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer,
  Paper,
  Typography,
  useTheme
} from "@mui/material";
import { tableHeadSx } from "../../../theme";
import type { SolutionPreparationComponentResponse } from "../types";

interface Props {
  components: SolutionPreparationComponentResponse[];
}

export function PreparationComponentsTable({ components }: Props) {
  const theme = useTheme();

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ width: 40, fontWeight: 700 }}>#</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Reagent / Material</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Target Quantity</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Consumed Stock Lot</TableCell>
            <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>Quantity Used</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {components.map((c) => (
            <TableRow key={c.id}>
              <TableCell>{c.order}</TableCell>
              <TableCell>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>{c.entryName}</Typography>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>{c.entryCode}</Typography>
              </TableCell>
              <TableCell>{c.recipeQuantity} {c.recipeUnit}</TableCell>
              <TableCell>
                {c.lotBatchNumber ? (
                  <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                    {c.lotBatchNumber}
                  </Typography>
                ) : (
                  <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic" }}>
                    (None)
                  </Typography>
                )}
              </TableCell>
              <TableCell sx={{ textAlign: "right" }}>
                {c.quantityUsed != null ? (
                  <Typography variant="body2" sx={{ fontWeight: 700 }}>
                    {c.quantityUsed} {c.lotUnit ?? ""}
                  </Typography>
                ) : "—"}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
