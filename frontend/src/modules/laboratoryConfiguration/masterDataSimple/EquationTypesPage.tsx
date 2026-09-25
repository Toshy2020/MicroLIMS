import { useEffect, useState } from "react";
import {
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Stack,
  Alert,
  Box,
  Typography,
  Chip,
  CircularProgress,
  Button
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import FunctionsIcon from "@mui/icons-material/Functions";
import { PageHeader } from "../../../components/PageHeader";
import { tableHeadSx } from "../../../theme";
import { masterDataOptions, EquationTypeDto } from "../../../services/masterDataOptions";

export function EquationTypesPage() {
  const [equationTypes, setEquationTypes] = useState<EquationTypeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadData = () => {
    setLoading(true);
    setError(null);
    masterDataOptions
      .getEquationTypes()
      .then((data) => {
        setEquationTypes(data);
        setLoading(false);
      })
      .catch((e: unknown) => {
        const errObj = e as { response?: { data?: { message?: string } }; message?: string };
        setError(errObj.response?.data?.message ?? errObj.message ?? "Could not load equation types.");
        setLoading(false);
      });
  };

  useEffect(() => {
    loadData();
  }, []);

  return (
    <>
      <PageHeader
        title="Equation Types"
        subtitle="Predefined mathematical formulas and required calculation parameters for analytical test methods and system suitability."
      >
        <Button variant="outlined" startIcon={<RefreshIcon />} onClick={loadData} disabled={loading}>
          Refresh
        </Button>
      </PageHeader>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Paper sx={{ p: 4, display: "flex", justifyContent: "center", alignItems: "center" }}>
          <CircularProgress size={32} />
        </Paper>
      ) : (
        <Paper sx={{ p: 2.5 }}>
          <Table size="small">
            <TableHead>
              <TableRow sx={tableHeadSx}>
                <TableCell sx={{ minWidth: 160 }}>Type / Name</TableCell>
                <TableCell sx={{ minWidth: 120 }}>Code</TableCell>
                <TableCell sx={{ minWidth: 320 }}>Formula Text</TableCell>
                <TableCell sx={{ minWidth: 240 }}>Required Inputs</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {equationTypes.map((type) => (
                <TableRow key={type.code}>
                  <TableCell>
                    <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                      <FunctionsIcon fontSize="small" color="primary" />
                      <Typography sx={{ fontWeight: 600, fontSize: "0.875rem" }}>{type.name}</Typography>
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Chip size="small" label={type.code} variant="outlined" />
                  </TableCell>
                  <TableCell>
                    {type.formulaText ? (
                      <Box
                        sx={{
                          fontFamily: "monospace",
                          fontSize: "0.8rem",
                          bgcolor: "action.hover",
                          p: 1,
                          borderRadius: 1,
                          wordBreak: "break-word",
                          lineHeight: 1.4
                        }}
                      >
                        {type.formulaText}
                      </Box>
                    ) : (
                      <Typography variant="body2" sx={{ color: "text.secondary", fontStyle: "italic" }}>
                        None
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    {type.requiredInputs && type.requiredInputs.length > 0 ? (
                      <Stack direction="row" spacing={0.75} sx={{ flexWrap: "wrap", rowGap: 0.5, alignItems: "center" }}>
                        {type.requiredInputs.map((input) => (
                          <Chip key={input} size="small" label={input} color="default" sx={{ fontSize: "0.75rem" }} />
                        ))}
                      </Stack>
                    ) : (
                      <Typography variant="body2" sx={{ color: "text.secondary", fontStyle: "italic" }}>
                        None
                      </Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {equationTypes.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} align="center" sx={{ py: 3, color: "text.secondary" }}>
                    No equation types found.
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Paper>
      )}
    </>
  );
}
