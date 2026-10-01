import { Button, Paper } from "@mui/material";
import SearchOffOutlinedIcon from "@mui/icons-material/SearchOffOutlined";
import { Link, useLocation } from "react-router-dom";
import { PageHeader } from "../components/PageHeader";
import { EmptyState } from "../components/lab/EmptyState";

// Shown for any address inside the app that matches no page - a mistyped or
// outdated link used to leave an empty content area with no explanation.
export function NotFoundPage() {
  const { pathname } = useLocation();
  return (
    <>
      <PageHeader title="Page not found" />
      <Paper variant="outlined">
        <EmptyState
          icon={<SearchOffOutlinedIcon />}
          title="There is no page at this address"
          description={`"${pathname}" does not match any MicroLIMS page. The link may be outdated, or the page may have moved. Use the navigation menu or return to your dashboard.`}
          action={<Button component={Link} to="/dashboard" variant="contained">Go to Dashboard</Button>}
        />
      </Paper>
    </>
  );
}
