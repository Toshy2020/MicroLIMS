import { Outlet } from "react-router-dom";
import { Box } from "@mui/material";
import { Header } from "../components/Header";
import { Sidebar } from "../components/Sidebar";

// Topbar + subnav stacked at the top, content below - matches the
// provided design's page structure for every route in the app.
export function MainLayout() {
  return (
    <Box sx={{ minHeight: "100vh", bgcolor: "background.default" }}>
      <Header />
      <Sidebar />
      <Box component="main" sx={{ px: 3, py: 2.75, maxWidth: 1400, mx: "auto" }}>
        <Outlet />
      </Box>
    </Box>
  );
}
