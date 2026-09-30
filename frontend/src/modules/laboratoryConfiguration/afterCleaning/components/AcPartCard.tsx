import { Box, Button, IconButton, Paper, Stack, Tooltip, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { LimitPills, ToneChip } from "../../../../components/configHierarchy";
import { MachinePart, PartConfig, acTestTypeLabel, isPathogenConfig } from "../acConfigTypes";

interface Props {
  part: MachinePart;
  configs: PartConfig[];
  testName: (code: string) => string;
  onEditPart: () => void;
  onDeletePart: () => void;
  onAddCount: () => void;
  onEditCount: (c: PartConfig) => void;
  onDeleteCount: (c: PartConfig) => void;
  onEditPathogens: () => void;
}

const heading = { fontSize: 12, fontWeight: 600, letterSpacing: "0.04em", textTransform: "uppercase", color: "text.secondary" } as const;

// One machine part: its count tests (swab/rinse, with limits) and the
// set of pathogen tests run on it. A part with no tests is highlighted.
export function AcPartCard({ part, configs, testName, onEditPart, onDeletePart, onAddCount, onEditCount, onDeleteCount, onEditPathogens }: Props) {
  const counts = configs.filter((c) => !isPathogenConfig(c));
  const pathogens = configs.filter(isPathogenConfig);
  const empty = configs.length === 0;

  return (
    <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden", display: "flex", flexDirection: "column", borderColor: empty ? "warning.light" : "divider" }}>
      <Stack direction="row" sx={{ p: 2, alignItems: "flex-start", gap: 1 }}>
        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
          <Typography component="h3" sx={{ fontSize: 16, fontWeight: 600, overflowWrap: "anywhere" }}>{part.name}</Typography>
          <Box sx={{ mt: 0.75 }}>
            {empty ? (
              <ToneChip label="No tests configured" tone="inconclusive" />
            ) : (
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {counts.length} count {counts.length === 1 ? "test" : "tests"} · {pathogens.length} {pathogens.length === 1 ? "pathogen" : "pathogens"}
              </Typography>
            )}
          </Box>
        </Box>
        <Tooltip title="Rename part">
          <IconButton aria-label={`Edit ${part.name}`} onClick={onEditPart}>
            <EditIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Delete part">
          <IconButton aria-label={`Delete ${part.name}`} color="error" onClick={onDeletePart}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      <Stack spacing={1.5} sx={{ px: 2, py: 1.5, borderTop: "1px solid", borderColor: "divider", bgcolor: "background.default", flexGrow: 1 }}>
        <Stack spacing={1}>
          <Typography sx={heading}>Count tests</Typography>
          {counts.map((c) => (
            <Stack key={c.id} spacing={0.75} sx={{ p: 1.25, borderRadius: 1, bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
              <Stack direction="row" sx={{ alignItems: "center", gap: 1 }}>
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{acTestTypeLabel(c.testType)}</Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{c.testCode}</Typography>
                </Box>
                <Tooltip title="Edit test">
                  <IconButton size="small" aria-label={`Edit ${c.testType} ${c.testCode}`} onClick={() => onEditCount(c)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Remove test">
                  <IconButton size="small" color="error" aria-label={`Remove ${c.testType} ${c.testCode}`} onClick={() => onDeleteCount(c)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Stack>
              <LimitPills {...c} />
            </Stack>
          ))}
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon />}
            onClick={onAddCount}
            aria-label={`Add count test to ${part.name}`}
            sx={{ alignSelf: "flex-start", textTransform: "none", fontWeight: 600, borderStyle: "dashed" }}
          >
            Add swab / rinse test
          </Button>
        </Stack>

        <Stack spacing={1}>
          <Typography sx={heading}>Pathogen tests</Typography>
          {pathogens.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>None - presence/absence tests such as E. coli or Salmonella.</Typography>
          ) : (
            <Stack direction="row" sx={{ flexWrap: "wrap", gap: 0.75 }}>
              {pathogens.map((c) => (
                <ToneChip key={c.id} label={testName(c.testCode)} tone="purple" />
              ))}
            </Stack>
          )}
          <Button
            size="small"
            variant="outlined"
            startIcon={pathogens.length === 0 ? <AddIcon /> : <EditIcon />}
            onClick={onEditPathogens}
            aria-label={`${pathogens.length === 0 ? "Add" : "Edit"} pathogen tests for ${part.name}`}
            sx={{ alignSelf: "flex-start", textTransform: "none", fontWeight: 600, borderStyle: "dashed" }}
          >
            {pathogens.length === 0 ? "Add pathogen tests" : "Edit pathogen tests"}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
