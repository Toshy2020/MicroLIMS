import { useState, MouseEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  Card,
  CardContent,
  Box,
  Typography,
  Avatar,
  IconButton,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText,
  Chip,
  Tooltip
} from "@mui/material";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import ChatBubbleOutlineIcon from "@mui/icons-material/ChatBubbleOutline";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import StarIcon from "@mui/icons-material/Star";
import StarBorderIcon from "@mui/icons-material/StarBorder";
import HistoryIcon from "@mui/icons-material/History";
import EditIcon from "@mui/icons-material/Edit";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { DiscussionPostSummary } from "../types/discussionTypes";
import { DiscussionCategoryBadge } from "./DiscussionCategoryBadge";
import { brandColors } from "../../../theme";

interface Props {
  post: DiscussionPostSummary;
  currentUserId?: number;
  canEditAny?: boolean;
  onToggleImportant: (id: number) => void;
  onDelete: (id: number) => void;
  onOpenHistory: (postId: number, postTitle: string) => void;
}

export function DiscussionCard({
  post,
  currentUserId,
  canEditAny,
  onToggleImportant,
  onDelete,
  onOpenHistory
}: Props) {
  const navigate = useNavigate();
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);

  const isAuthor = currentUserId === post.authorUserId;
  const canModify = isAuthor || canEditAny;

  const handleMenuOpen = (e: MouseEvent<HTMLElement>) => {
    e.stopPropagation();
    setMenuAnchor(e.currentTarget);
  };

  const handleMenuClose = () => {
    setMenuAnchor(null);
  };

  const handleToggleImportant = (e: MouseEvent) => {
    e.stopPropagation();
    handleMenuClose();
    onToggleImportant(post.id);
  };

  const handleDelete = (e: MouseEvent) => {
    e.stopPropagation();
    handleMenuClose();
    if (window.confirm("Are you sure you want to delete this discussion post?")) {
      onDelete(post.id);
    }
  };

  const authorInitial = (post.authorName || "U").charAt(0).toUpperCase();

  return (
    <Card
      variant="outlined"
      sx={{
        borderRadius: 2.5,
        transition: "transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease",
        borderColor: post.isImportant ? "warning.main" : "divider",
        bgcolor: (theme) =>
          post.isImportant
            ? theme.palette.mode === "dark"
              ? "rgba(234, 179, 8, 0.04)"
              : "rgba(254, 249, 195, 0.25)"
            : "background.paper",
        "&:hover": {
          boxShadow: "0 4px 12px rgba(0,0,0,0.06)",
          borderColor: post.isImportant ? "warning.dark" : "primary.main"
        }
      }}
    >
      <CardContent sx={{ p: 2.5, "&:last-child": { pb: 2.5 } }}>
        {/* Header Row */}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 1.5, mb: 1.5 }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
            <Avatar
              sx={{
                width: 38,
                height: 38,
                bgcolor: brandColors.sectionTitle,
                color: "#fff",
                fontWeight: 700,
                fontSize: 14
              }}
            >
              {authorInitial}
            </Avatar>
            <Box>
              <Typography sx={{ fontWeight: 700, fontSize: 13.5, color: "text.primary", lineHeight: 1.2 }}>
                {post.authorName}
              </Typography>
              <Typography sx={{ fontSize: 11.5, color: "text.secondary", lineHeight: 1.2 }}>
                {post.authorRole} • {new Date(post.createdAt).toLocaleDateString(undefined, {
                  month: "short",
                  day: "numeric",
                  year: "numeric"
                })}
              </Typography>
            </Box>
          </Box>

          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            {post.isImportant && (
              <Chip
                icon={<StarIcon sx={{ fontSize: "14px !important", color: "warning.main" }} />}
                label="Important"
                size="small"
                sx={{
                  fontWeight: 700,
                  fontSize: 11,
                  bgcolor: (theme) => (theme.palette.mode === "dark" ? "rgba(234, 179, 8, 0.15)" : "#FEF9C3"),
                  color: "warning.dark",
                  border: "1px solid",
                  borderColor: "warning.light"
                }}
              />
            )}

            <DiscussionCategoryBadge category={post.category} categoryName={post.categoryName} />

            {post.isEdited && (
              <Tooltip title={`Version ${post.currentVersion} (Edited). Click to view history.`}>
                <Chip
                  icon={<HistoryIcon sx={{ fontSize: "13px !important" }} />}
                  label={`v${post.currentVersion} Edited`}
                  size="small"
                  variant="outlined"
                  onClick={(e) => {
                    e.stopPropagation();
                    onOpenHistory(post.id, post.title);
                  }}
                  sx={{ fontSize: 11, cursor: "pointer" }}
                />
              </Tooltip>
            )}

            {canModify && (
              <>
                <IconButton size="small" onClick={handleMenuOpen}>
                  <MoreVertIcon fontSize="small" />
                </IconButton>
                <Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={handleMenuClose}>
                  <MenuItem
                    component={Link}
                    to={`/discussions/${post.id}`}
                    onClick={handleMenuClose}
                  >
                    <ListItemIcon>
                      <EditIcon fontSize="small" />
                    </ListItemIcon>
                    <ListItemText primary="View / Edit" />
                  </MenuItem>
                  <MenuItem onClick={handleToggleImportant}>
                    <ListItemIcon>
                      {post.isImportant ? <StarBorderIcon fontSize="small" /> : <StarIcon fontSize="small" />}
                    </ListItemIcon>
                    <ListItemText primary={post.isImportant ? "Unmark Important" : "Mark Important"} />
                  </MenuItem>
                  <MenuItem onClick={handleDelete} sx={{ color: "error.main" }}>
                    <ListItemIcon sx={{ color: "error.main" }}>
                      <DeleteOutlineIcon fontSize="small" />
                    </ListItemIcon>
                    <ListItemText primary="Delete Discussion" />
                  </MenuItem>
                </Menu>
              </>
            )}
          </Box>
        </Box>

        {/* Title */}
        <Typography
          component={Link}
          to={`/discussions/${post.id}`}
          sx={{
            fontWeight: 700,
            fontSize: 16,
            color: "text.primary",
            textDecoration: "none",
            display: "block",
            mb: 1,
            "&:hover": { color: "primary.main", textDecoration: "underline" }
          }}
        >
          {post.title}
        </Typography>

        {/* Content Preview */}
        <Typography
          variant="body2"
          sx={{
            color: "text.secondary",
            fontSize: 13.5,
            mb: 2,
            lineHeight: 1.5,
            display: "-webkit-box",
            WebkitLineClamp: 3,
            WebkitBoxOrient: "vertical",
            overflow: "hidden"
          }}
        >
          {post.contentPreview}
        </Typography>

        {/* Footer Meta */}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", pt: 1, borderTop: "1px solid", borderColor: "divider" }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
            <Box
              component={Link}
              to={`/discussions/${post.id}`}
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 0.6,
                color: "text.secondary",
                textDecoration: "none",
                fontSize: 12.5,
                fontWeight: 500,
                "&:hover": { color: "primary.main" }
              }}
            >
              <ChatBubbleOutlineIcon sx={{ fontSize: 16 }} />
              {post.commentCount} {post.commentCount === 1 ? "comment" : "comments"}
            </Box>

            {post.attachmentCount > 0 && (
              <Box
                component={Link}
                to={`/discussions/${post.id}`}
                sx={{
                  display: "flex",
                  alignItems: "center",
                  gap: 0.5,
                  color: "text.secondary",
                  textDecoration: "none",
                  fontSize: 12.5,
                  fontWeight: 500,
                  "&:hover": { color: "primary.main" }
                }}
              >
                <AttachFileIcon sx={{ fontSize: 16 }} />
                {post.attachmentCount} {post.attachmentCount === 1 ? "attachment" : "attachments"}
              </Box>
            )}
          </Box>

          <Typography
            component={Link}
            to={`/discussions/${post.id}`}
            sx={{
              fontSize: 12.5,
              fontWeight: 600,
              color: "primary.main",
              textDecoration: "none",
              "&:hover": { textDecoration: "underline" }
            }}
          >
            Open discussion →
          </Typography>
        </Box>
      </CardContent>
    </Card>
  );
}
