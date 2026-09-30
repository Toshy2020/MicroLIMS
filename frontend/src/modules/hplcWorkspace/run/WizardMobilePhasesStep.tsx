import { Link } from "react-router-dom";
import {
  Box,
  Typography,
  Button,
  Paper,
  Grid,
  Radio,
  RadioGroup,
  FormControlLabel,
  Card,
  Alert,
  Stack,
  useTheme
} from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import type { HplcMethodResponse } from "../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService";
import type { SolutionPreparationListItem } from "../../solutionPreparation/types";

export interface WizardMobilePhasesStepProps {
  method: HplcMethodResponse;
  availablePreparations: Record<string, SolutionPreparationListItem[]>;
  selectedMobilePhases: Record<string, number>;
  loadingPreps: boolean;
  onSelectPreparation: (channel: string, prepId: number) => void;
  onRefreshPreparations: () => void;
}

export function WizardMobilePhasesStep({
  method,
  availablePreparations,
  selectedMobilePhases,
  loadingPreps,
  onSelectPreparation,
  onRefreshPreparations
}: WizardMobilePhasesStepProps) {
  const theme = useTheme();

  return (
    <Stack spacing={3}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
          Assign Prepared Mobile Phases:
        </Typography>
        <Button
          size="small"
          startIcon={<RefreshIcon />}
          onClick={onRefreshPreparations}
          disabled={loadingPreps}
          sx={{ textTransform: "none" }}
        >
          Refresh Preparations
        </Button>
      </Box>

      {method.mobilePhases.map((mp) => {
        const preps = availablePreparations[mp.channel] || [];
        const selectedPrepId = selectedMobilePhases[mp.channel];

        return (
          <Paper
            key={mp.channel}
            elevation={0}
            sx={{
              p: 2.5,
              borderRadius: 2,
              border: `1px solid ${theme.palette.divider}`
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, fontSize: 15 }}>
                  Channel {mp.channel}: {mp.solutionMasterName}
                </Typography>
                {mp.ratioPercent != null && (
                  <Typography variant="caption" sx={{ color: "text.secondary" }}>
                    Ratio: {mp.ratioPercent}%
                  </Typography>
                )}
              </Box>

              <Button
                size="small"
                component={Link}
                to={`/preparation/new?type=MobilePhase&solutionId=${mp.solutionMasterId}&methodId=${method.id}`}
                target="_blank"
                rel="noopener noreferrer"
                endIcon={<OpenInNewIcon fontSize="small" />}
                sx={{ textTransform: "none" }}
              >
                Prepare New
              </Button>
            </Box>

            {preps.length === 0 ? (
              <Alert severity="warning" sx={{ py: 0.5, fontSize: 13 }}>
                No available preparation found for {mp.solutionMasterName}. Click &quot;Prepare New&quot; to prepare and sign a batch, then click &quot;Refresh Preparations&quot;.
              </Alert>
            ) : (
              <RadioGroup
                value={selectedPrepId ?? ""}
                onChange={(e) => onSelectPreparation(mp.channel, Number(e.target.value))}
              >
                <Grid container spacing={1.5}>
                  {preps.map((p) => (
                    <Grid size={{ xs: 12, sm: 6 }} key={p.id}>
                      <Card
                        variant="outlined"
                        onClick={() => onSelectPreparation(mp.channel, p.id)}
                        sx={{
                          p: 1.25,
                          cursor: "pointer",
                          borderColor:
                            selectedPrepId === p.id
                              ? theme.palette.primary.main
                              : theme.palette.divider,
                          backgroundColor:
                            selectedPrepId === p.id
                              ? theme.palette.action.selected
                              : theme.palette.background.paper
                        }}
                      >
                        <FormControlLabel
                          value={p.id}
                          control={<Radio size="small" />}
                          label={
                            <Box>
                              <Typography variant="body2" sx={{ fontWeight: 700 }}>
                                {p.code}
                              </Typography>
                              <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                                Prepared by {p.preparedByUserName} · Exp: {p.expiresAt ? new Date(p.expiresAt).toLocaleDateString() : "—"}
                              </Typography>
                            </Box>
                          }
                          sx={{ m: 0 }}
                        />
                      </Card>
                    </Grid>
                  ))}
                </Grid>
              </RadioGroup>
            )}
          </Paper>
        );
      })}
    </Stack>
  );
}
