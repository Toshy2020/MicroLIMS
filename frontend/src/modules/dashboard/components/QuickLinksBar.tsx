import { Paper, Box, Typography, Stack } from "@mui/material";
import { useNavigate } from "react-router-dom";
import AddCircleOutlineIcon from "@mui/icons-material/AddCircleOutline";
import PlaylistAddCheckOutlinedIcon from "@mui/icons-material/PlaylistAddCheckOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import FactCheckOutlinedIcon from "@mui/icons-material/FactCheckOutlined";
import GppGoodOutlinedIcon from "@mui/icons-material/GppGoodOutlined";
import { SvgIconComponent } from "@mui/icons-material";
import { brandColors } from "../../../theme";

interface QuickLink { label: string; icon: SvgIconComponent; path: string; count?: number }

// There are no dedicated Preparation/Review/Approval Queue pages in this
// app - that work is reached by clicking a sample's lifecycle badge
// inside Testing Workspace (see menuConfig.ts). All queue links point
// there rather than to invented routes/filters that don't exist yet.
export function QuickLinksBar({ preparationQueue, reviewerQueue, approvalQueue }: {
  preparationQueue: number; reviewerQueue: number; approvalQueue: number;
}) {
  const navigate = useNavigate();

  const links: QuickLink[] = [
    { label: "Receive New Sample", icon: AddCircleOutlineIcon, path: "/receiving" },
    { label: "Preparation Queue", icon: PlaylistAddCheckOutlinedIcon, path: "/testing-workspace", count: preparationQueue },
    { label: "Testing Workspace", icon: ScienceOutlinedIcon, path: "/testing-workspace" },
    { label: "Review Queue", icon: FactCheckOutlinedIcon, path: "/testing-workspace", count: reviewerQueue },
    { label: "Approval Queue", icon: GppGoodOutlinedIcon, path: "/testing-workspace", count: approvalQueue }
  ];

  return (
    <Paper sx={{ p: 2, mb: 1 }}>
      <Stack direction="row" spacing={3} flexWrap="wrap" rowGap={1.5}>
        {links.map((l) => (
          <Box
            key={l.label}
            onClick={() => navigate(l.path)}
            sx={{ display: "flex", alignItems: "center", gap: 0.75, cursor: "pointer", "&:hover": { opacity: 0.75 } }}
          >
            <l.icon fontSize="small" sx={{ color: brandColors.sectionTitle }} />
            <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{l.label}</Typography>
            {l.count !== undefined && (
              <Box component="span" sx={{
                display: "inline-flex", alignItems: "center", justifyContent: "center", minWidth: 20, height: 20, px: 0.5,
                borderRadius: 10, fontSize: 11, fontWeight: 700, color: "#fff", bgcolor: brandColors.sectionTitle
              }}>
                {l.count}
              </Box>
            )}
          </Box>
        ))}
      </Stack>
    </Paper>
  );
}
