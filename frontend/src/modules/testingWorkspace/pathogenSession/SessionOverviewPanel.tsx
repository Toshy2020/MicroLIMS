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
  Divider
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
        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb" }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2,
                bgcolor: "#ede9fe",
                color: "#6d28d9",
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <LocationOnOutlinedIcon />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: "#1f2937" }}>
                {session.totalLocations}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "#6b7280", fontWeight: 600 }}>
                Sampling Locations
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb" }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2,
                bgcolor: "#dbeafe",
                color: "#1d4ed8",
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <ScienceOutlinedIcon />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: "#1f2937" }}>
                {session.totalAssignedTests}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "#6b7280", fontWeight: 600 }}>
                Assigned Tests
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb" }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2,
                bgcolor: "#fef3c7",
                color: "#b45309",
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <FactCheckOutlinedIcon />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: "#1f2937" }}>
                {session.requiredResultCount}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "#6b7280", fontWeight: 600 }}>
                Required Results ({session.totalLocations} × {session.totalAssignedTests})
              </Typography>
            </Box>
          </Stack>
        </Paper>

        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb" }}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <Box
              sx={{
                width: 40,
                height: 40,
                borderRadius: 2,
                bgcolor: session.completedResultCount === session.requiredResultCount ? "#d1fae5" : "#f1f5f9",
                color: session.completedResultCount === session.requiredResultCount ? "#047857" : "#475569",
                display: "flex",
                alignItems: "center",
                justifyContent: "center"
              }}
            >
              <AssignmentTurnedInOutlinedIcon />
            </Box>
            <Box>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: "#1f2937" }}>
                {session.completedResultCount}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "#6b7280", fontWeight: 600 }}>
                Completed ({session.pendingResultCount} pending)
              </Typography>
            </Box>
          </Stack>
        </Paper>
      </Box>

      {/* Session Scope & Rules Callout */}
      <Paper sx={{ p: 2.5, borderRadius: 2, bgcolor: "#faf5ff", border: "1px solid #e9d5ff" }}>
        <Stack direction={{ xs: "column", sm: "row" }} justifyContent="space-between" alignItems={{ sm: "center" }} spacing={2}>
          <Box>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 0.5 }}>
              <Chip
                label="Master Data Driven"
                size="small"
                sx={{ bgcolor: brandColors.sectionTitle, color: "#ffffff", fontWeight: 700, fontSize: 11 }}
              />
              <Typography sx={{ fontSize: 14, fontWeight: 700, color: "#1f2937" }}>
                Workflow Configured from Test Master
              </Typography>
            </Stack>
            <Typography sx={{ fontSize: 13, color: "#4b5563" }}>
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
        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb" }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: "#1f2937" }}>
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
                <TableCell sx={{ fontWeight: 700, color: "#4b5563" }}>Test Code</TableCell>
                <TableCell sx={{ fontWeight: 700, color: "#4b5563" }}>Display Name</TableCell>
                <TableCell sx={{ fontWeight: 700, color: "#4b5563" }}>Workflow</TableCell>
                <TableCell sx={{ fontWeight: 700, color: "#4b5563" }}>TSB</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {session.assignedTests.map((t) => (
                <TableRow key={t.testCode} hover>
                  <TableCell sx={{ fontWeight: 700, color: brandColors.sectionTitle }}>
                    {t.testCode}
                  </TableCell>
                  <TableCell sx={{ fontSize: 13 }}>{t.displayName}</TableCell>
                  <TableCell>
                    <Chip
                      label={t.workflowType}
                      size="small"
                      sx={{ fontSize: 11, height: 20, bgcolor: "#f3f4f6" }}
                    />
                  </TableCell>
                  <TableCell>
                    {t.requiresTsb ? (
                      <Chip
                        label="Required"
                        size="small"
                        sx={{ fontSize: 11, height: 20, bgcolor: "#ede9fe", color: "#6d28d9", fontWeight: 700 }}
                      />
                    ) : (
                      <Typography sx={{ fontSize: 12, color: "#9ca3af" }}>—</Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>

        {/* Sampling Locations List */}
        <Paper sx={{ p: 2.5, borderRadius: 2, border: "1px solid #e5e7eb" }}>
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
            <Typography sx={{ fontSize: 15, fontWeight: 700, color: "#1f2937" }}>
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
                  <TableCell sx={{ fontWeight: 700, color: "#4b5563" }}>#</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "#4b5563" }}>Location Name</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "#4b5563" }}>Type</TableCell>
                  <TableCell sx={{ fontWeight: 700, color: "#4b5563" }}>Grade</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {session.locations.map((loc, idx) => (
                  <TableRow key={loc.id} hover>
                    <TableCell sx={{ color: "#6b7280", fontSize: 12 }}>{idx + 1}</TableCell>
                    <TableCell sx={{ fontWeight: 600, fontSize: 13 }}>{loc.locationName}</TableCell>
                    <TableCell sx={{ fontSize: 12, color: "#6b7280" }}>{loc.locationType}</TableCell>
                    <TableCell sx={{ fontSize: 12 }}>
                      {loc.gradeClassification ? (
                        <Chip
                          label={`Grade ${loc.gradeClassification}`}
                          size="small"
                          sx={{ fontSize: 10, height: 18, bgcolor: "#ecfdf5", color: "#065f46" }}
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
