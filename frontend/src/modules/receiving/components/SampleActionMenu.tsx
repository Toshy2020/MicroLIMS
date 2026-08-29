import React, { useState } from "react";
import {
  Box,
  IconButton,
  Tooltip,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText,
  useTheme
} from "@mui/material";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import AssignmentIndOutlinedIcon from "@mui/icons-material/AssignmentIndOutlined";
import BlockOutlinedIcon from "@mui/icons-material/BlockOutlined";
import { Link } from "react-router-dom";
import { SampleRecord } from "../types/receivingTypes";
import { useAuth } from "../../../contexts/AuthContext";

interface Props {
  sample: SampleRecord;
  onViewSummary: (sample: SampleRecord) => void;
  onEdit: (sample: SampleRecord) => void;
  onViewReport: (sample: SampleRecord) => void;
  onViewAuditHistory: (sample: SampleRecord) => void;
  onPrepareSample: (sample: SampleRecord) => void;
  onAssignAnalyst?: (sample: SampleRecord) => void;
  onVoid?: (sample: SampleRecord) => void;
}

export function SampleActionMenu({
  sample,
  onViewSummary,
  onEdit,
  onViewReport,
  onViewAuditHistory,
  onPrepareSample,
  onAssignAnalyst,
  onVoid
}: Props) {
  const theme = useTheme();
  const { role } = useAuth();
  const isAuthorizedToAssign = role === "SectionHead" || role === "SystemAdministrator";
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const isMenuOpen = Boolean(anchorEl);

  const handleOpenMenu = (event: React.MouseEvent<HTMLElement>) => {
    event.stopPropagation();
    setAnchorEl(event.currentTarget);
  };

  const handleCloseMenu = (event?: any) => {
    event?.stopPropagation?.();
    setAnchorEl(null);
  };

  const isEditable = !sample.incubationStarted;
  const needsPreparation = sample.preparationStatus === "NeedsPreparation";

  return (
    <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }} onClick={(e) => e.stopPropagation()}>
      {/* View Sample Action */}
      <Tooltip title="View Sample Details & Workflow">
        <IconButton
          size="small"
          onClick={(e) => {
            e.stopPropagation();
            onViewSummary(sample);
          }}
          sx={{
            color: "text.secondary",
            "&:hover": { color: theme.custom.status.purple.text, bgcolor: theme.custom.status.purple.bg }
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
            onClick={(e) => {
              e.stopPropagation();
              onEdit(sample);
            }}
            sx={{
              color: isEditable ? "text.secondary" : "text.disabled",
              "&:hover": isEditable ? { color: theme.custom.status.purple.text, bgcolor: theme.custom.status.purple.bg } : undefined
            }}
          >
            <EditOutlinedIcon sx={{ fontSize: 18 }} />
          </IconButton>
        </span>
      </Tooltip>

      {/* Void Sample Action */}
      <Tooltip title="Void Sample">
        <IconButton
          size="small"
          onClick={(e) => {
            e.stopPropagation();
            if (onVoid) {
              onVoid(sample);
            } else {
              onViewSummary(sample);
            }
          }}
          sx={{
            color: "text.secondary",
            "&:hover": { color: theme.custom.status.detected.text, bgcolor: theme.custom.status.detected.bg }
          }}
        >
          <BlockOutlinedIcon sx={{ fontSize: 18 }} />
        </IconButton>
      </Tooltip>

      {/* More Actions Menu */}
      <IconButton
        size="small"
        onClick={handleOpenMenu}
        sx={{
          color: "text.secondary",
          "&:hover": { color: theme.custom.status.purple.text, bgcolor: theme.custom.status.purple.bg }
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
            borderRadius: 1.5
          }
        }}
      >
        {isAuthorizedToAssign && onAssignAnalyst && (
          <MenuItem
            onClick={() => {
              handleCloseMenu();
              onAssignAnalyst(sample);
            }}
          >
            <ListItemIcon>
              <AssignmentIndOutlinedIcon sx={{ fontSize: 18, color: theme.palette.primary.main }} />
            </ListItemIcon>
            <ListItemText primary="Assign Analyst" primaryTypographyProps={{ fontSize: 13, fontWeight: 600 }} />
          </MenuItem>
        )}

        {needsPreparation && (
          <MenuItem
            onClick={() => {
              handleCloseMenu();
              onPrepareSample(sample);
            }}
          >
            <ListItemIcon>
              <ScienceOutlinedIcon sx={{ fontSize: 18, color: theme.custom.status.action.text }} />
            </ListItemIcon>
            <ListItemText primary="Prepare Sample" primaryTypographyProps={{ fontSize: 13, fontWeight: 600 }} />
          </MenuItem>
        )}

        <MenuItem
          onClick={() => {
            handleCloseMenu();
            if (onVoid) {
              onVoid(sample);
            } else {
              onViewSummary(sample);
            }
          }}
        >
          <ListItemIcon>
            <BlockOutlinedIcon sx={{ fontSize: 18, color: theme.custom.status.detected.text }} />
          </ListItemIcon>
          <ListItemText
            primary="Void Sample"
            primaryTypographyProps={{ fontSize: 13, fontWeight: 600, color: theme.custom.status.detected.text }}
          />
        </MenuItem>

        <MenuItem
          component={Link}
          to={`/samples/${sample.sampleId}/report`}
          target="_blank"
          rel="noopener"
          onClick={handleCloseMenu}
        >
          <ListItemIcon>
            <PictureAsPdfOutlinedIcon sx={{ fontSize: 18, color: theme.custom.status.info.text }} />
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
            <HistoryOutlinedIcon sx={{ fontSize: 18, color: "text.secondary" }} />
          </ListItemIcon>
          <ListItemText primary="Audit History" primaryTypographyProps={{ fontSize: 13 }} />
        </MenuItem>
      </Menu>
    </Box>
  );
}
