import {
  Box,
  Typography,
  Grid,
  Card,
  Chip,
  Stack,
  useTheme
} from "@mui/material";
import type { HplcMethodOptionDto } from "../types";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import { MethodReadOnlyPanel } from "./MethodReadOnlyPanel";

export interface WizardMethodStepProps {
  methodOptions: HplcMethodOptionDto[];
  selectedMethodId: number | null;
  selectedMethod: HplcMethodResponse | null;
  onSelectMethod: (id: number) => void;
}

export function WizardMethodStep({
  methodOptions,
  selectedMethodId,
  selectedMethod,
  onSelectMethod
}: WizardMethodStepProps) {
  const theme = useTheme();

  return (
    <Stack spacing={2.5}>
      <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
        Select an Active HPLC Method:
      </Typography>
      <Grid container spacing={2}>
        {methodOptions.map((opt) => (
          <Grid size={{ xs: 12, sm: 6 }} key={opt.id}>
            <Card
              variant="outlined"
              onClick={() => onSelectMethod(opt.id)}
              sx={{
                p: 2,
                cursor: "pointer",
                borderRadius: 2,
                borderColor:
                  selectedMethodId === opt.id
                    ? theme.palette.primary.main
                    : theme.palette.divider,
                backgroundColor:
                  selectedMethodId === opt.id
                    ? theme.palette.action.selected
                    : theme.palette.background.paper,
                "&:hover": { borderColor: theme.palette.primary.light }
              }}
            >
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
                <Box>
                  <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 15 }}>
                    {opt.name}
                  </Typography>
                  <Stack direction="row" spacing={1} sx={{ alignItems: "center", mt: 0.5 }}>
                    <Typography variant="caption" sx={{ color: "primary.main", fontWeight: 700 }}>
                      {opt.abbreviation}
                    </Typography>
                    {opt.columnDesignation && (
                      <Chip
                        size="small"
                        label={`USP ${opt.columnDesignation}`}
                        variant="outlined"
                        sx={{ fontSize: 11, height: 20, fontWeight: 600 }}
                      />
                    )}
                  </Stack>
                </Box>
                <Chip
                  size="small"
                  label={`${opt.eligibleTestOrderCount} eligible tests`}
                  variant="outlined"
                  sx={{ fontWeight: 600 }}
                />
              </Box>
            </Card>
          </Grid>
        ))}
      </Grid>

      {selectedMethod && (
        <Box sx={{ mt: 2 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            Method Details Preview:
          </Typography>
          <MethodReadOnlyPanel method={selectedMethod} />
        </Box>
      )}
    </Stack>
  );
}
