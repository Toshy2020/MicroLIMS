import React, { useState } from "react";
import {
  Box,
  IconButton,
  Tooltip,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText
} from "@mui/material";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { SampleRecord } from "../types/receivingTypes";

interface Props {
  sample: SampleRecord;
  onViewSummary: (sample: SampleRecord) => void;
  onEdit: (sample: SampleRecord) => void;
  onViewReport: (sample: SampleRecord) => void;
  onViewAuditHistory: (sample: SampleRecord) => void;
  onPrepareSample: (sample: SampleRecord) => void;
}

export function SampleActionMenu({
  sample,
  onViewSummary,
  onEdit,
  onViewReport,
  onViewAuditHistory,
  onPrepareSample
}: Props) {
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const isMenuOpen = Boolean(anchorEl);

  const handleOpenMenu = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleCloseMenu = () => {
    setAnchorEl(null);
  };

  const isEditable = !sample.incubationStarted;
  const needsPreparation = sample.preparationStatus === "NeedsPreparation";

  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
      {/* View Sample Action */}
      <Tooltip title="View Sample Details & Workflow">
        <IconButton
          size="small"
          onClick={() => onViewSummary(sample)}
          sx={{
            color: "#4b5563",
            "&:hover": { color: "#7b2d8e", bgcolor: "#f3e8ff" }
          }}
        >
          <VisibilityOutlinedIcon sx={{ fontSize: 18 }} />
        </IconButton>
      </Tooltip>

      {/* Edit Sample Action (Batch / Control correction) */}
      <Tooltip
        title={
          isEditable
            ? "Edit Batch / Control Number"
            : "Locked: Incubation has already started for this sample"
        }
      >
        <span>
          <IconButton
            size="small"
            disabled={!isEditable}
            onClick={() => onEdit(sample)}
            sx={{
              color: isEditable ? "#4b5563" : "#d1d5db",
              "&:hover": isEditable ? { color: "#7b2d8e", bgcolor: "#f3e8ff" } : undefined
            }}
          >
            <EditOutlinedIcon sx={{ fontSize: 18 }} />
          </IconButton>
        </span>
      </Tooltip>

      {/* More Actions Menu */}
      <IconButton
        size="small"
        onClick={handleOpenMenu}
        sx={{
          color: "#4b5563",
          "&:hover": { color: "#7b2d8e", bgcolor: "#f3e8ff" }
        }}
      >
        <MoreVertIcon sx={{ fontSize: 18 }} />
      </IconButton>

      <Menu
        anchorEl={anchorEl}
        open={isMenuOpen}
        onClose={handleCloseMenu}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
        PaperProps={{
          sx: {
            minWidth: 180,
            borderRadius: 1.5,
            boxShadow: "0 4px 16px rgba(0,0,0,0.1)"
          }
        }}
      >
        {needsPreparation && (
          <MenuItem
            onClick={() => {
              handleCloseMenu();
              onPrepareSample(sample);
            }}
          >
            <ListItemIcon>
              <ScienceOutlinedIcon sx={{ fontSize: 18, color: "#d97706" }} />
            </ListItemIcon>
            <ListItemText primary="Prepare Sample" primaryTypographyProps={{ fontSize: 13, fontWeight: 600 }} />
          </MenuItem>
        )}

        <MenuItem
          onClick={() => {
            handleCloseMenu();
            onViewReport(sample);
          }}
        >
          <ListItemIcon>
            <PictureAsPdfOutlinedIcon sx={{ fontSize: 18, color: "#2563eb" }} />
          </ListItemIcon>
          <ListItemText primary="View Full Report" primaryTypographyProps={{ fontSize: 13 }} />
        </MenuItem>

        <MenuItem
          onClick={() => {
            handleCloseMenu();
            onViewAuditHistory(sample);
          }}
        >
          <ListItemIcon>
            <HistoryOutlinedIcon sx={{ fontSize: 18, color: "#6b7280" }} />
          </ListItemIcon>
          <ListItemText primary="Audit History" primaryTypographyProps={{ fontSize: 13 }} />
        </MenuItem>
      </Menu>
    </Box>
  );
}
