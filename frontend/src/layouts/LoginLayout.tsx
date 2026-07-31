import { Outlet } from "react-router-dom";
import { Box, Paper } from "@mui/material";

export function LoginLayout() {
  return (
    <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", bgcolor: "background.default" }}>
      <Paper elevation={3} sx={{ p: 4, width: 360 }}>
        <Outlet />
      </Paper>
    </Box>
  );
}
