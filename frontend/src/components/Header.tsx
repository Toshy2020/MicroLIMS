import { Box, Typography, Avatar } from "@mui/material";
import { useAuth } from "../contexts/AuthContext";
import { brandColors } from "../theme";

// Purple-gradient brand topbar, per the provided design.
export function Header() {
  const { username } = useAuth();
  const initial = (username ?? "U").charAt(0).toUpperCase();

  return (
    <Box
      className="no-print"
      sx={{
        background: brandColors.topbarGradient,
        color: "#fff",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        px: 3,
        py: 1.5
      }}
    >
      <Typography sx={{ fontSize: 22, fontWeight: 700, letterSpacing: 0.5 }}>
        Micro<Box component="span" sx={{ fontWeight: 300, opacity: 0.85 }}>LIMS</Box>
      </Typography>
      <Avatar sx={{ width: 34, height: 34, bgcolor: "#fff", color: brandColors.sectionTitle, fontWeight: 700, fontSize: 14 }}>
        {initial}
      </Avatar>
    </Box>
  );
}
