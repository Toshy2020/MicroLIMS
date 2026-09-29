import {
  Box,
  Typography,
  Alert,
  Stack,
  Paper,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell
} from "@mui/material";
import { formatLabDate, formatLabDateTime } from "../../../utils/formatDate";
import type { TitrantStandardizationResponse, RecipeSnapshot } from "../types";

interface Props {
  result: TitrantStandardizationResponse;
  recipe: RecipeSnapshot | null;
  blankRequired: boolean;
}

export function StandardizationResultView({ result, recipe, blankRequired }: Props) {
  return (
    <Stack spacing={2.5}>
      <Alert severity={result.passed ? "success" : "error"}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          {result.passed ? "Standardization Passed" : "Standardization Failed"}
        </Typography>
        <Typography variant="body2">
          {result.passed
            ? `Mean factor: ${result.meanFactor.toFixed(4)}. This is now the active factor.`
            : "This standardization failed acceptance criteria and will not become the active factor."}
        </Typography>
        {!result.passed && result.failureReasons && (
          <Typography variant="body2" sx={{ mt: 1, whiteSpace: "pre-line", fontWeight: 600 }}>
            {result.failureReasons}
          </Typography>
        )}
      </Alert>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr 1fr", sm: "repeat(4, 1fr)" }, gap: 2 }}>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Mean Factor
            </Typography>
            <Typography variant="h6" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
              {result.meanFactor.toFixed(4)}
            </Typography>
            {recipe?.factorMin != null && recipe?.factorMax != null && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Limits: {recipe.factorMin.toFixed(4)} – {recipe.factorMax.toFixed(4)}
              </Typography>
            )}
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              RSD (%)
            </Typography>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              {result.rsdPercent != null ? `${result.rsdPercent.toFixed(2)}%` : "N/A"}
            </Typography>
            {recipe?.maxRsdPercent != null && (
              <Typography variant="caption" sx={{ color: "text.secondary" }}>
                Max: {recipe.maxRsdPercent.toFixed(2)}%
              </Typography>
            )}
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Standardized At
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {formatLabDateTime(result.standardizedAt)}
            </Typography>
          </Box>
          <Box>
            <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
              Valid Until
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {result.validUntil ? formatLabDate(result.validUntil) : (result.passed ? "Before each use" : "—")}
            </Typography>
          </Box>
        </Box>
      </Paper>

      <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
        Replicates Evaluated
      </Typography>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell sx={{ fontWeight: 700 }}>Rep #</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Titrant Vol (mL)</TableCell>
            {blankRequired && <TableCell sx={{ fontWeight: 700 }}>Blank (mL)</TableCell>}
            <TableCell sx={{ fontWeight: 700 }}>Calculated Factor</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {result.replicates.map((r) => (
            <TableRow key={r.id || r.replicateNo}>
              <TableCell sx={{ fontWeight: 600 }}>{r.replicateNo}</TableCell>
              <TableCell>{r.titrantVolumeMl}</TableCell>
              {blankRequired && <TableCell>{r.blankMl != null ? r.blankMl : "0"}</TableCell>}
              <TableCell sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                {r.factor.toFixed(4)}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Stack>
  );
}
