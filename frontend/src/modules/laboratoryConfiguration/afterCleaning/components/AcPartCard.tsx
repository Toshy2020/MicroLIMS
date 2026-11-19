import { Box, Button, IconButton, Paper, Stack, Tooltip, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { useTranslation } from "../../../../i18n";
import { LimitPills, ToneChip } from "../../../../components/configHierarchy";
import { useTestDefinitions } from "../../../../hooks/useTestDefinitions";
import { useEditableTestCodes } from "../../../../hooks/useEditableTestCodes";
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
  editableCodes?: Set<string>;
}

const heading = { fontSize: 12, fontWeight: 600, letterSpacing: "0.04em", textTransform: "uppercase", color: "text.secondary" } as const;

// One machine part: its count tests (swab/rinse, with limits) and the
// set of pathogen tests run on it. A part with no tests is highlighted.
export function AcPartCard({ part, configs, testName, onEditPart, onDeletePart, onAddCount, onEditCount, onDeleteCount, onEditPathogens, editableCodes }: Props) {
  const { t } = useTranslation("labConfig");
  const { options } = useTestDefinitions();
  const { editableCodes: hookCodes } = useEditableTestCodes();
  const effectiveEditable = editableCodes !== undefined ? editableCodes : hookCodes;

  const counts = configs.filter((c) => !isPathogenConfig(c));
  const pathogens = configs.filter(isPathogenConfig);
  const empty = configs.length === 0;

  const labName = (code: string) => options.find((o) => o.code === code)?.section?.name || t("afterCleaning.card.otherLab");

  return (
    <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden", display: "flex", flexDirection: "column", borderColor: empty ? "warning.light" : "divider" }}>
      <Stack direction="row" sx={{ p: 2, alignItems: "flex-start", gap: 1 }}>
        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
          <Typography component="h3" sx={{ fontSize: 16, fontWeight: 600, overflowWrap: "anywhere" }}>{part.name}</Typography>
          <Box sx={{ mt: 0.75 }}>
            {empty ? (
              <ToneChip label={t("afterCleaning.card.noTests")} tone="inconclusive" />
            ) : (
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {t("afterCleaning.card.summary", {
                  counts: t("afterCleaning.card.countTests", { count: counts.length }),
                  pathogens: t("afterCleaning.card.pathogenCount", { count: pathogens.length })
                })}
              </Typography>
            )}
          </Box>
        </Box>
        <Tooltip title={t("afterCleaning.card.renamePart")}>
          <IconButton aria-label={t("afterCleaning.card.editAria", { name: part.name })} onClick={onEditPart}>
            <EditIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title={t("afterCleaning.card.deletePart")}>
          <IconButton aria-label={t("afterCleaning.card.deleteAria", { name: part.name })} color="error" onClick={onDeletePart}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      <Stack spacing={1.5} sx={{ px: 2, py: 1.5, borderTop: "1px solid", borderColor: "divider", bgcolor: "background.default", flexGrow: 1 }}>
        <Stack spacing={1}>
          <Typography sx={heading}>{t("afterCleaning.card.countHeading")}</Typography>
          {counts.map((c) => {
            const isEditable = effectiveEditable !== undefined && effectiveEditable.has(c.testCode);
            return (
              <Stack key={c.id} spacing={0.75} sx={{ p: 1.25, borderRadius: 1, bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
                <Stack direction="row" sx={{ alignItems: "center", gap: 1 }}>
                  <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                    <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{acTestTypeLabel(c.testType)}</Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{c.testCode}</Typography>
                  </Box>
                  {isEditable ? (
                    <>
                      <Tooltip title={t("afterCleaning.card.editTest")}>
                        <IconButton size="small" aria-label={t("afterCleaning.card.editTestAria", { type: c.testType, code: c.testCode })} onClick={() => onEditCount(c)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t("afterCleaning.card.removeTest")}>
                        <IconButton size="small" color="error" aria-label={t("afterCleaning.card.removeTestAria", { type: c.testType, code: c.testCode })} onClick={() => onDeleteCount(c)}>
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </>
                  ) : (
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      {labName(c.testCode)}
                    </Typography>
                  )}
                </Stack>
                <LimitPills {...c} />
              </Stack>
            );
          })}
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon />}
            onClick={onAddCount}
            aria-label={t("afterCleaning.card.addCountAria", { name: part.name })}
            sx={{ alignSelf: "flex-start", textTransform: "none", fontWeight: 600, borderStyle: "dashed" }}
          >
            {t("afterCleaning.card.addCount")}
          </Button>
        </Stack>

        <Stack spacing={1}>
          <Typography sx={heading}>{t("afterCleaning.card.pathogenHeading")}</Typography>
          {pathogens.length === 0 ? (
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("afterCleaning.card.noPathogens")}</Typography>
          ) : (
            <Stack direction="row" sx={{ flexWrap: "wrap", gap: 0.75 }}>
              {pathogens.map((c) => {
                const isLocked = effectiveEditable !== undefined && !effectiveEditable.has(c.testCode);
                return (
                  <Tooltip key={c.id} title={isLocked ? labName(c.testCode) : ""}>
                    <span>
                      <ToneChip label={testName(c.testCode)} tone={isLocked ? "pending" : "purple"} />
                    </span>
                  </Tooltip>
                );
              })}
            </Stack>
          )}
          <Button
            size="small"
            variant="outlined"
            startIcon={pathogens.length === 0 ? <AddIcon /> : <EditIcon />}
            onClick={onEditPathogens}
            aria-label={t(pathogens.length === 0 ? "afterCleaning.card.pathogenAria_add" : "afterCleaning.card.pathogenAria_edit", { name: part.name })}
            sx={{ alignSelf: "flex-start", textTransform: "none", fontWeight: 600, borderStyle: "dashed" }}
          >
            {pathogens.length === 0 ? t("afterCleaning.card.addPathogens") : t("afterCleaning.card.editPathogens")}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}
