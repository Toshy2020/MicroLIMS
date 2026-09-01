import { useEffect, useState } from "react";
import {
  Box,
  Grid,
  Paper,
  Typography,
  Button,
  Chip,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  useTheme
} from "@mui/material";
import AdminPanelSettingsOutlinedIcon from "@mui/icons-material/AdminPanelSettingsOutlined";
import PeopleAltOutlinedIcon from "@mui/icons-material/PeopleAltOutlined";
import SecurityOutlinedIcon from "@mui/icons-material/SecurityOutlined";
import SearchOutlinedIcon from "@mui/icons-material/SearchOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import ArrowForwardOutlinedIcon from "@mui/icons-material/ArrowForwardOutlined";
import { Link } from "react-router-dom";
import { useAuth } from "../../contexts/AuthContext";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { DashboardService } from "./services/DashboardService";
import { DashboardSummary, KpiDeltas } from "./types/dashboard";
import { brandColors } from "../../theme";

export function AdminDashboardPage() {
  const theme = useTheme();
  const { username, fullName } = useAuth();
  const displayName = fullName ?? username ?? "System Administrator";

  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [kpis, setKpis] = useState<KpiDeltas | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      DashboardService.getSummary().catch(() => null),
      DashboardService.getKpiDeltas().catch(() => null)
    ]).then(([sumData, kpiData]) => {
      setSummary(sumData);
      setKpis(kpiData);
      setLoading(false);
    });
  }, []);

  if (loading || !summary) return <LoadingSpinner />;

  return (
    <>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
        <PageHeader
          title={`Administrator Command Center — ${displayName}`}
          subtitle="System administration, access control, audit compliance, and laboratory operations."
        />
        <Box sx={{ display: "flex", gap: 1.5, flexWrap: "wrap" }}>
          <Button
            component={Link}
            to="/audit-search"
            variant="outlined"
            startIcon={<SearchOutlinedIcon />}
            sx={{ textTransform: "none", fontWeight: 600, borderRadius: 2 }}
          >
            Audit Search
          </Button>
          <Button
            component={Link}
            to="/users"
            variant="contained"
            startIcon={<PeopleAltOutlinedIcon />}
            sx={{ textTransform: "none", fontWeight: 600, borderRadius: 2 }}
          >
            Manage Users
          </Button>
        </Box>
      </Box>

      {/* Tier 1: Administrative Control Pillars */}
      <Grid container spacing={2} sx={{ mb: 2.5 }}>
        <Grid item xs={12} sm={6} md={3}>
          <Paper
            component={Link}
            to="/users"
            sx={{
              p: 2,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${theme.palette.primary.main}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                User Accounts
              </Typography>
              <PeopleAltOutlinedIcon sx={{ color: theme.palette.primary.main, fontSize: 22 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: theme.palette.primary.main, my: 0.5 }}>
              Manage Users
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              Create, lock/unlock, password resets
            </Typography>
          </Paper>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Paper
            component={Link}
            to="/roles"
            sx={{
              p: 2,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${brandColors.info}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Roles & Permissions
              </Typography>
              <SecurityOutlinedIcon sx={{ color: brandColors.info, fontSize: 22 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.info, my: 0.5 }}>
              Access Control
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              RBAC and segregation of duties
            </Typography>
          </Paper>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Paper
            component={Link}
            to="/audit-search"
            sx={{
              p: 2,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${brandColors.warn}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Audit Search
              </Typography>
              <SearchOutlinedIcon sx={{ color: brandColors.warn, fontSize: 22 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.warn, my: 0.5 }}>
              21 CFR Part 11
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              ALCOA+ traceability logs
            </Typography>
          </Paper>
        </Grid>

        <Grid item xs={12} sm={6} md={3}>
          <Paper
            component={Link}
            to="/reports"
            sx={{
              p: 2,
              cursor: "pointer",
              display: "block",
              textDecoration: "none",
              color: "inherit",
              borderLeft: `4px solid ${brandColors.ok}`,
              transition: "transform 0.15s, box-shadow 0.15s",
              "&:hover": { transform: "translateY(-2px)", boxShadow: 3 }
            }}
          >
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography sx={{ fontSize: 12, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
                Reports & KPIs
              </Typography>
              <DescriptionOutlinedIcon sx={{ color: brandColors.ok, fontSize: 22 }} />
            </Box>
            <Typography sx={{ fontSize: 24, fontWeight: 800, color: brandColors.ok, my: 0.5 }}>
              Laboratory KPIs
            </Typography>
            <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
              Export workbench & analytics
            </Typography>
          </Paper>
        </Grid>
      </Grid>

      {/* Tier 2: Laboratory Operational Oversight */}
      <Paper sx={{ p: 2.5, mb: 2.5 }}>
        <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
          Laboratory Operational Oversight
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={6} sm={4} md={2}>
            <Paper variant="outlined" sx={{ p: 1.5, textAlign: "center" }}>
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 700 }}>Total Samples</Typography>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: theme.palette.primary.main }}>
                {kpis?.totalSamples ?? "—"}
              </Typography>
            </Paper>
          </Grid>
          <Grid item xs={6} sm={4} md={2}>
            <Paper variant="outlined" sx={{ p: 1.5, textAlign: "center" }}>
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 700 }}>Total Tests</Typography>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: theme.palette.primary.main }}>
                {kpis?.totalTests ?? "—"}
              </Typography>
            </Paper>
          </Grid>
          <Grid item xs={6} sm={4} md={2}>
            <Paper
              component={Link}
              to="/testing-workspace?status=Active"
              variant="outlined"
              sx={{
                p: 1.5,
                textAlign: "center",
                cursor: "pointer",
                display: "block",
                textDecoration: "none",
                color: "inherit",
                "&:hover": { bgcolor: "action.hover" }
              }}
            >
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 700 }}>Pending Tests</Typography>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: brandColors.info }}>
                {summary.pendingTests}
              </Typography>
            </Paper>
          </Grid>
          <Grid item xs={6} sm={4} md={2}>
            <Paper
              component={Link}
              to="/testing-workspace?testStatus=ResultEntered"
              variant="outlined"
              sx={{
                p: 1.5,
                textAlign: "center",
                cursor: "pointer",
                display: "block",
                textDecoration: "none",
                color: "inherit",
                "&:hover": { bgcolor: "action.hover" }
              }}
            >
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 700 }}>Review Queue</Typography>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: brandColors.warn }}>
                {summary.reviewerQueue}
              </Typography>
            </Paper>
          </Grid>
          <Grid item xs={6} sm={4} md={2}>
            <Paper
              component={Link}
              to="/testing-workspace?testStatus=Reviewed"
              variant="outlined"
              sx={{
                p: 1.5,
                textAlign: "center",
                cursor: "pointer",
                display: "block",
                textDecoration: "none",
                color: "inherit",
                "&:hover": { bgcolor: "action.hover" }
              }}
            >
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 700 }}>Approval Queue</Typography>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: brandColors.ok }}>
                {summary.approvalQueue}
              </Typography>
            </Paper>
          </Grid>
          {summary.pendingPreparationConfigApproval > 0 && (
            <Grid item xs={6} sm={4} md={2}>
              <Paper
                component={Link}
                to="/laboratory-configuration/items"
                variant="outlined"
                sx={{
                  p: 1.5,
                  textAlign: "center",
                  cursor: "pointer",
                  display: "block",
                  textDecoration: "none",
                  color: "inherit",
                  "&:hover": { bgcolor: "action.hover" }
                }}
              >
                <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 700 }}>Prep Configs Pending</Typography>
                <Typography sx={{ fontSize: 22, fontWeight: 800, color: brandColors.warn }}>
                  {summary.pendingPreparationConfigApproval}
                </Typography>
              </Paper>
            </Grid>
          )}
          <Grid item xs={6} sm={4} md={2}>
            <Paper
              component={Link}
              to="/testing-workspace?urgency=overdue"
              variant="outlined"
              sx={{
                p: 1.5,
                textAlign: "center",
                cursor: "pointer",
                display: "block",
                textDecoration: "none",
                color: "inherit",
                "&:hover": { bgcolor: "action.hover" }
              }}
            >
              <Typography sx={{ fontSize: 11, color: "text.secondary", fontWeight: 700 }}>Overdue (&gt;24h)</Typography>
              <Typography sx={{ fontSize: 22, fontWeight: 800, color: brandColors.err }}>
                {summary.delayedTests}
              </Typography>
            </Paper>
          </Grid>
        </Grid>
      </Paper>

      {/* Tier 3: Master Data & Laboratory Configuration Quick Actions */}
      <Paper sx={{ p: 2.5 }}>
        <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main, mb: 1.5 }}>
          Master Data & System Configuration
        </Typography>
        <Grid container spacing={1.5}>
          {[
            { label: "Test Master", path: "/laboratory-configuration/test-master" },
            { label: "Specifications", path: "/laboratory-configuration/specifications" },
            { label: "Media Configurations", path: "/laboratory-configuration/media-configurations" },
            { label: "Organisms", path: "/laboratory-configuration/organisms" },
            { label: "Items & Materials", path: "/laboratory-configuration/items" },
            { label: "Equipment Inventory", path: "/inventory/equipment" }
          ].map((item, idx) => (
            <Grid item xs={12} sm={6} md={4} key={idx}>
              <Paper
                component={Link}
                to={item.path}
                variant="outlined"
                sx={{
                  p: 1.5,
                  cursor: "pointer",
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  textDecoration: "none",
                  color: "inherit",
                  "&:hover": { bgcolor: "action.hover", borderColor: theme.palette.primary.main }
                }}
              >
                <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{item.label}</Typography>
                <ArrowForwardOutlinedIcon sx={{ fontSize: 18, color: "text.secondary" }} />
              </Paper>
            </Grid>
          ))}
        </Grid>
      </Paper>
    </>
  );
}
