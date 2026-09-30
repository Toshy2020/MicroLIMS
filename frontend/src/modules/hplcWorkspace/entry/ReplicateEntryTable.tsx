import {
  Box,
  Paper,
  Typography,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  IconButton,
  Chip,
  Tooltip,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import { tableHeadSx } from "../../../theme";
import type { HplcMethodWeightDto, HplcReplicateDto } from "../types";

export interface ReplicateEntryTableProps {
  methodWeights: HplcMethodWeightDto[];
  replicates: HplcReplicateDto[];
  onChange: (replicates: HplcReplicateDto[]) => void;
  requiredReplicates?: number | null;
  disabled?: boolean;
}

export function ReplicateEntryTable({
  methodWeights,
  replicates,
  onChange,
  requiredReplicates,
  disabled = false
}: ReplicateEntryTableProps) {
  const theme = useTheme();

  const handleAddRow = () => {
    const nextNo = replicates.length + 1;
    const initialResponses = methodWeights.map((mw) => ({
      hplcMethodAnalyteId: mw.hplcMethodAnalyteId,
      response: 0
    }));

    const newReplicate: HplcReplicateDto = {
      replicateNo: nextNo,
      actualWeightMg: 0,
      responses: initialResponses
    };

    onChange([...replicates, newReplicate]);
  };

  const handleRemoveRow = (indexToRemove: number) => {
    const updated = replicates
      .filter((_, idx) => idx !== indexToRemove)
      .map((rep, idx) => ({
        ...rep,
        replicateNo: idx + 1
      }));
    onChange(updated);
  };

  const handleWeightChange = (index: number, rawVal: string) => {
    const parsed = rawVal === "" ? 0 : parseFloat(rawVal);
    const updated = replicates.map((rep, idx) => {
      if (idx !== index) return rep;
      return {
        ...rep,
        actualWeightMg: isNaN(parsed) ? 0 : parsed
      };
    });
    onChange(updated);
  };

  const handleResponseChange = (
    repIndex: number,
    analyteId: number,
    rawVal: string
  ) => {
    const parsed = rawVal === "" ? 0 : parseFloat(rawVal);
    const updated = replicates.map((rep, idx) => {
      if (idx !== repIndex) return rep;

      const existingResp = rep.responses.find((r) => r.hplcMethodAnalyteId === analyteId);
      const otherResponses = rep.responses.filter((r) => r.hplcMethodAnalyteId !== analyteId);

      const newResp = {
        hplcMethodAnalyteId: analyteId,
        response: isNaN(parsed) ? 0 : parsed
      };

      const finalResponses = existingResp
        ? rep.responses.map((r) => (r.hplcMethodAnalyteId === analyteId ? newResp : r))
        : [...otherResponses, newResp];

      return {
        ...rep,
        responses: finalResponses
      };
    });
    onChange(updated);
  };

  return (
    <Paper
      elevation={0}
      sx={{
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        overflow: "hidden"
      }}
    >
      <Box
        sx={{
          p: 2,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: 1.5,
          borderBottom: `1px solid ${theme.palette.divider}`
        }}
      >
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              Replicate Injections & Weigh-in
            </Typography>
            {requiredReplicates !== undefined && requiredReplicates !== null && (
              <Chip
                label={`Required: ${requiredReplicates}`}
                size="small"
                color={replicates.length >= requiredReplicates ? "success" : "default"}
                variant="outlined"
              />
            )}
            <Chip
              label={`${replicates.length} entered`}
              size="small"
              variant="outlined"
            />
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Record actual test weight (mg) and peak response per analyte for each replicate injection.
          </Typography>
        </Box>

        <Button
          variant="outlined"
          color="primary"
          size="small"
          startIcon={<AddIcon />}
          onClick={handleAddRow}
          disabled={disabled}
          sx={{ textTransform: "none" }}
        >
          Add Replicate
        </Button>
      </Box>

      <TableContainer>
        <Table size="small">
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell sx={{ width: 80 }}>Rep #</TableCell>
              <TableCell sx={{ minWidth: 160 }}>Actual Weight (mg) *</TableCell>
              {methodWeights.map((mw) => (
                <TableCell key={mw.hplcMethodAnalyteId} sx={{ minWidth: 180 }}>
                  {mw.analyteName} Response *
                </TableCell>
              ))}
              <TableCell align="right" sx={{ width: 60 }}>
                Action
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {replicates.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={3 + methodWeights.length}
                  sx={{ textAlign: "center", py: 4, color: "text.secondary" }}
                >
                  No replicates added. Click &ldquo;Add Replicate&rdquo; to start entering replicate test data.
                </TableCell>
              </TableRow>
            ) : (
              replicates.map((rep, repIdx) => (
                <TableRow key={rep.replicateNo} hover>
                  <TableCell sx={{ fontWeight: 700 }}>
                    #{rep.replicateNo}
                  </TableCell>
                  <TableCell>
                    <TextField
                      type="number"
                      size="small"
                      fullWidth
                      value={rep.actualWeightMg === 0 ? "" : rep.actualWeightMg}
                      onChange={(e) => handleWeightChange(repIdx, e.target.value)}
                      placeholder="0.00"
                      disabled={disabled}
                      slotProps={{ htmlInput: { min: 0, step: "any" } }}
                      sx={{ maxWidth: 160 }}
                    />
                  </TableCell>
                  {methodWeights.map((mw) => {
                    const respObj = rep.responses.find(
                      (r) => r.hplcMethodAnalyteId === mw.hplcMethodAnalyteId
                    );
                    const val = respObj ? respObj.response : 0;

                    return (
                      <TableCell key={mw.hplcMethodAnalyteId}>
                        <TextField
                          type="number"
                          size="small"
                          fullWidth
                          value={val === 0 ? "" : val}
                          onChange={(e) =>
                            handleResponseChange(
                              repIdx,
                              mw.hplcMethodAnalyteId,
                              e.target.value
                            )
                          }
                          placeholder="0.00"
                          disabled={disabled}
                          slotProps={{ htmlInput: { min: 0, step: "any" } }}
                          sx={{ maxWidth: 180 }}
                        />
                      </TableCell>
                    );
                  })}
                  <TableCell align="right">
                    <Tooltip title={replicates.length <= 1 ? "At least one replicate is required" : "Remove replicate"}>
                      <span>
                        <IconButton
                          size="small"
                          color="error"
                          disabled={disabled || replicates.length <= 1}
                          onClick={() => handleRemoveRow(repIdx)}
                        >
                          <DeleteOutlineIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}
