import {
  Box,
  Typography,
  Stack,
  Button,
  Paper,
  Chip,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  useTheme,
  TableContainer
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import LocationOnOutlinedIcon from "@mui/icons-material/LocationOnOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import AssignmentTurnedInOutlinedIcon from "@mui/icons-material/AssignmentTurnedInOutlined";
import { PathogenTestingSessionDto } from "../types/pathogenSessionTypes";
import { brandColors } from "../../../theme";
import { Trans } from "react-i18next";
import { useTranslation } from "../../../i18n";
import { Ltr } from "../../../components/Ltr";

interface Props {
  session: PathogenTestingSessionDto;
  onStartWorkflow: () => void;
}

export function SessionOverviewPanel({ session, onStartWorkflow }: Props) {
  const { t } = useTranslation("testingWorkspace");
  const theme = useTheme();
  const tsbCount = session.assignedTests.filter((tItem) => tItem.requiresTsb).length;

  return (
    <Stack spacing={3}>
      {/* Top Session Cards */}
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: "1fr",
            sm: "repeat(2, 1fr)",
            md: "repeat(4, 1fr)"
          },
          gap: 2
        }}
      >
        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" spacing={1.5} sx={{
            alignItems: "center"
          }}>
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2,
                bgcolor: theme.custom.status.purple.bg,
                color: theme.custom.status.purple.text,
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <LocationOnOutlinedIcon />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: "text.primary" }}>
                <Ltr>{session.totalLocations}</Ltr>
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
                {t("pathogenSession.overview.samplingLocations")}
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" spacing={1.5} sx={{
            alignItems: "center"
          }}>
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2,
                bgcolor: theme.custom.status.info.bg,
                color: theme.custom.status.info.text,
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <ScienceOutlinedIcon />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: "text.primary" }}>
                <Ltr>{session.totalAssignedTests}</Ltr>
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
                {t("pathogenSession.overview.assignedTests")}
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" spacing={1.5} sx={{
            alignItems: "center"
          }}>
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2,
                bgcolor: theme.custom.status.inconclusive.bg,
                color: theme.custom.status.inconclusive.text,
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <FactCheckOutlinedIcon />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: "text.primary" }}>
                <Ltr>{session.requiredResultCount}</Ltr>
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
                <Trans
                  i18nKey="pathogenSession.overview.requiredResults"
                  ns="testingWorkspace"
                  values={{ locations: session.totalLocations, testCount: session.totalAssignedTests }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" spacing={1.5} sx={{
            alignItems: "center"
          }}>
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2,
                bgcolor: session.completedResultCount === session.requiredResultCount ? theme.custom.status.notDetected.bg : "background.default",
                color: session.completedResultCount === session.requiredResultCount ? theme.custom.status.notDetected.text : "text.secondary",
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <AssignmentTurnedInOutlinedIcon />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: "text.primary" }}>
                <Ltr>{session.completedResultCount}</Ltr>
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
                <Trans
                  i18nKey="pathogenSession.overview.completedPending"
                  ns="testingWorkspace"
                  values={{ count: session.pendingResultCount }}
                  components={{ ltr: <Ltr>{null}</Ltr> }}
                />
              </Typography>
            </Box>
          </Stack>
        </Paper>
      </Box>

      {/* Session Scope & Rules Callout */}
      <Paper sx={{ p: 2.5, borderRadius: 2, bgcolor: theme.custom.status.purple.bg, border: "1px solid", borderColor: theme.custom.status.purple.border }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{
            justifyContent: "space-between",
            alignItems: { sm: "center" }
          }}>
          <Box>
            <Stack
              direction="row"
              spacing={1}
              sx={{
                alignItems: "center",
                mb: 0.5
              }}>
              <Chip
                label={t("pathogenSession.overview.masterDataDriven")}
                size="small"
                sx={{ bgcolor: brandColors.sectionTitle, color: "common.white", fontWeight: 700, fontSize: 12 }}
              />
              <Typography sx={{ fontSize: 14, fontWeight: 700, color: "text.primary" }}>
                {t("pathogenSession.overview.workflowConfiguredTitle")}
              </Typography>
            </Stack>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              {t("pathogenSession.overview.workflowConfiguredDesc")}
            </Typography>
          </Box>

          <Button
            variant="contained"
            color="primary"
            startIcon={<PlayArrowIcon />}
            onClick={onStartWorkflow}
            sx={{ px: 3, py: 1, fontWeight: 700, flexShrink: 0 }}
          >
            {tsbCount > 0 ? t("pathogenSession.overview.openSharedTsbSetup") : t("pathogenSession.overview.openTestWorkflows")}
          </Button>
        </Stack>
      </Paper>

      {/* Assigned Tests & Locations Grid */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 3 }}>
        {/* Assigned Tests List */}
        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack
            direction="row"
            sx={{
              justifyContent: "space-between",
              alignItems: "center",
              mb: 2
            }}>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: "text.primary" }}>
              <Trans
                i18nKey="pathogenSession.overview.assignedTestsTitle"
                ns="testingWorkspace"
                values={{ count: session.assignedTests.length }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <Chip
              label={t("pathogenSession.overview.sourceTestMaster")}
              size="small"
              variant="outlined"
              sx={{ fontSize: 12, fontWeight: 600 }}
            />
          </Stack>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>{t("pathogenSession.overview.colTestCode")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>{t("pathogenSession.overview.colDisplayName")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>{t("pathogenSession.overview.colWorkflow")}</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>{t("pathogenSession.overview.colTsb")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {session.assignedTests.map((tItem) => (
                  <TableRow key={tItem.testCode} hover>
                    <TableCell sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
                      <Ltr>{tItem.testCode}</Ltr>
                    </TableCell>
                    <TableCell sx={{ fontSize: 13 }}>{tItem.displayName}</TableCell>
                    <TableCell>
                      <Chip
                        label={tItem.workflowType}
                        size="small"
                        sx={{ fontSize: 12, height: 20, bgcolor: "background.default" }}
                      />
                    </TableCell>
                    <TableCell>
                      {tItem.requiresTsb ? (
                        <Chip
                          label={t("pathogenSession.overview.required")}
                          size="small"
                          sx={{ fontSize: 12, height: 20, bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 700 }}
                        />
                      ) : (
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>—</Typography>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>

        {/* Sampling Locations List */}
        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack
            direction="row"
            sx={{
              justifyContent: "space-between",
              alignItems: "center",
              mb: 2
            }}>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: "text.primary" }}>
              <Trans
                i18nKey="pathogenSession.overview.samplingLocationsTitle"
                ns="testingWorkspace"
                values={{ count: session.locations.length }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
            <Chip
              label={t("pathogenSession.overview.programChip", { name: session.programName })}
              size="small"
              variant="outlined"
              sx={{ fontSize: 12, fontWeight: 600 }}
            />
          </Stack>
          <Box sx={{ maxHeight: 320, overflowY: "auto" }}>
            <TableContainer>
              <Table size="small" stickyHeader>
                <TableHead>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>{t("pathogenSession.overview.colIndex")}</TableCell>
                    <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>{t("pathogenSession.overview.colLocationName")}</TableCell>
                    <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>{t("pathogenSession.overview.colType")}</TableCell>
                    <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>{t("pathogenSession.overview.colGrade")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {session.locations.map((loc, idx) => (
                    <TableRow key={loc.id} hover>
                      <TableCell sx={{ color: "text.secondary", fontSize: 12 }}><Ltr>{idx + 1}</Ltr></TableCell>
                      <TableCell sx={{ fontWeight: 600, fontSize: 13 }}>{loc.locationName}</TableCell>
                      <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>{loc.locationType}</TableCell>
                      <TableCell sx={{ fontSize: 12 }}>
                        {loc.gradeClassification ? (
                          <Chip
                            label={t("pathogenSession.overview.gradeChip", { grade: loc.gradeClassification })}
                            size="small"
                            sx={{ fontSize: 12, height: 20, bgcolor: theme.custom.status.notDetected.bg, color: theme.custom.status.notDetected.text }}
                          />
                        ) : (
                          "—"
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </Box>
        </Paper>
      </Box>
    </Stack>
  );
}
