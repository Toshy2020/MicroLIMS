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
  Divider,
  useTheme
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import LocationOnOutlinedIcon from "@mui/icons-material/LocationOnOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import AssignmentTurnedInOutlinedIcon from "@mui/icons-material/AssignmentTurnedInOutlined";
import { PathogenTestingSessionDto } from "../types/pathogenSessionTypes";
import { brandColors } from "../../../theme";

interface Props {
  session: PathogenTestingSessionDto;
  onStartWorkflow: () => void;
}

export function SessionOverviewPanel({ session, onStartWorkflow }: Props) {
  const theme = useTheme();
  const tsbCount = session.assignedTests.filter((t) => t.requiresTsb).length;

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
          <Stack direction="row" spacing={1.5} alignItems="center">
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
                {session.totalLocations}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
                Sampling Locations
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
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
                {session.totalAssignedTests}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
                Assigned Tests
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
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
                {session.requiredResultCount}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
                Required Results ({session.totalLocations} × {session.totalAssignedTests})
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
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
                {session.completedResultCount}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary", fontWeight: 600 }}>
                Completed ({session.pendingResultCount} pending)
              </Typography>
            </Box>
          </Stack>
        </Paper>
      </Box>

      {/* Session Scope & Rules Callout */}
      <Paper sx={{ p: 2.5, borderRadius: 2, bgcolor: theme.custom.status.purple.bg, border: "1px solid", borderColor: theme.custom.status.purple.border }}>
        <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" alignItems={{ sm: "center" }} spacing={2}>
          <Box>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
              <Chip
                label="Master Data Driven"
                size="small"
                sx={{ bgcolor: brandColors.sectionTitle, color: "#ffffff", fontWeight: 700, fontSize: 11 }}
              />
              <Typography sx={{ fontSize: 14, fontWeight: 700, color: "text.primary" }}>
                Workflow Configured from Test Master
              </Typography>
            </Stack>
            <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
              Assigned tests are loaded automatically from the Sample Test Profile. Procedural steps (TSB enrichment & selective incubation) are executed once per session, while final analytical results are entered per location.
            </Typography>
          </Box>

          <Button
            variant="contained"
            color="primary"
            startIcon={<PlayArrowIcon />}
            onClick={onStartWorkflow}
            sx={{ px: 3, py: 1, fontWeight: 700, flexShrink: 0 }}
          >
            {tsbCount > 0 ? "Open Shared TSB Setup" : "Open Test Workflows"}
          </Button>
        </Stack>
      </Paper>

      {/* Assigned Tests & Locations Grid */}
      <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 3 }}>
        {/* Assigned Tests List */}
        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: "text.primary" }}>
              Assigned Tests ({session.assignedTests.length})
            </Typography>
            <Chip
              label="Source: Test Master"
              size="small"
              variant="outlined"
              sx={{ fontSize: 11, fontWeight: 600 }}
            />
          </Stack>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>Test Code</TableCell>
                <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>Display Name</TableCell>
                <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>Workflow</TableCell>
                <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>TSB</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {session.assignedTests.map((t) => (
                <TableRow key={t.testCode} hover>
                  <TableCell sx={{ fontWeight: 700, color: theme.palette.primary.main }}>
                    {t.testCode}
                  </TableCell>
                  <TableCell sx={{ fontSize: 13 }}>{t.displayName}</TableCell>
                  <TableCell>
                    <Chip
                      label={t.workflowType}
                      size="small"
                      sx={{ fontSize: 11, height: 20, bgcolor: "background.default" }}
                    />
                  </TableCell>
                  <TableCell>
                    {t.requiresTsb ? (
                      <Chip
                        label="Required"
                        size="small"
                        sx={{ fontSize: 11, height: 20, bgcolor: theme.custom.status.purple.bg, color: theme.custom.status.purple.text, fontWeight: 700 }}
                      />
                    ) : (
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>—</Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>

        {/* Sampling Locations List */}
        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid", borderColor: "divider" }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: "text.primary" }}>
              Sampling Locations ({session.locations.length})
            </Typography>
            <Chip
              label={`Program: ${session.programName}`}
              size="small"
              variant="outlined"
              sx={{ fontSize: 11, fontWeight: 600 }}
            />
          </Stack>
          <Box sx={{ maxHeight: 320, overflowY: "auto" }}>
            <Table size="small" stickyHeader>
              <TableHead>
                <TableRow>
                  <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>#</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>Location Name</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>Type</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "text.secondary" }}>Grade</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {session.locations.map((loc, idx) => (
                  <TableRow key={loc.id} hover>
                    <TableCell sx={{ color: "text.secondary", fontSize: 12 }}>{idx + 1}</TableCell>
                    <TableCell sx={{ fontWeight: 600, fontSize: 13 }}>{loc.locationName}</TableCell>
                    <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>{loc.locationType}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {loc.gradeClassification ? (
                        <Chip
                          label={`Grade ${loc.gradeClassification}`}
                          size="small"
                          sx={{ fontSize: 10, height: 18, bgcolor: theme.custom.status.notDetected.bg, color: theme.custom.status.notDetected.text }}
                        />
                      ) : (
                        "—"
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        </Paper>
      </Box>
    </Stack>
  );
}
