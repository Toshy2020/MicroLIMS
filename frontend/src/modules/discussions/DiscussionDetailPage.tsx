import { useEffect, useState, useCallback } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import {
  Box,
  Typography,
  Paper,
  Button,
  Avatar,
  Chip,
  Divider,
  TextField,
  IconButton,
  CircularProgress,
  Alert,
  Tooltip,
  Menu,
  MenuItem,
  ListItemIcon,
  ListItemText
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import EditIcon from "@mui/icons-material/Edit";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import HistoryIcon from "@mui/icons-material/History";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import DownloadIcon from "@mui/icons-material/Download";
import SendIcon from "@mui/icons-material/Send";
import StarIcon from "@mui/icons-material/Star";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import StarBorderIcon from "@mui/icons-material/StarBorder";
import CheckIcon from "@mui/icons-material/Check";
import CloseIcon from "@mui/icons-material/Close";
import { useAuth } from "../../contexts/AuthContext";
import { DiscussionPostDetail, DiscussionComment } from "./types/discussionTypes";
import { discussionService } from "./services/discussionService";
import { DiscussionCategoryBadge } from "./components/DiscussionCategoryBadge";
import { EditDiscussionDialog } from "./components/EditDiscussionDialog";
import { DiscussionHistoryDialog } from "./components/DiscussionHistoryDialog";
import { brandColors } from "../../theme";

export function DiscussionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const postId = Number(id);
  const navigate = useNavigate();
  const { userId, role } = useAuth();
  const currentUserId = userId ?? undefined;
  const canEditAny = role === "SystemAdministrator" || role === "SectionHead";

  const [post, setPost] = useState<DiscussionPostDetail | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // New comment state
  const [newCommentText, setNewCommentText] = useState<string>("");
  const [submittingComment, setSubmittingComment] = useState<boolean>(false);

  // Editing comment state
  const [editingCommentId, setEditingCommentId] = useState<number | null>(null);
  const [editingCommentText, setEditingCommentText] = useState<string>("");

  // Modals
  const [editDialogOpen, setEditDialogOpen] = useState<boolean>(false);
  const [historyOpen, setHistoryOpen] = useState<boolean>(false);
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);

  const loadPost = useCallback(async () => {
    if (isNaN(postId)) return;
    setLoading(true);
    setError(null);
    try {
      const res = await discussionService.getById(postId);
      setPost(res);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to load discussion.");
    } finally {
      setLoading(false);
    }
  }, [postId]);

  useEffect(() => {
    loadPost();
  }, [loadPost]);

  const handleToggleImportant = async () => {
    if (!post) return;
    try {
      const isImportant = await discussionService.toggleImportant(post.id);
      setPost((prev) => (prev ? { ...prev, isImportant } : null));
    } catch {
      alert("Failed to toggle important status.");
    }
  };

  const handleDeletePost = async () => {
    if (!post) return;
    if (window.confirm("Are you sure you want to delete this discussion post?")) {
      try {
        await discussionService.deletePost(post.id);
        navigate("/discussions");
      } catch {
        alert("Failed to delete discussion post.");
      }
    }
  };

  const handleAddComment = async () => {
    if (!post || !newCommentText.trim()) return;
    setSubmittingComment(true);
    try {
      const created = await discussionService.addComment(post.id, newCommentText.trim());
      setPost((prev) => (prev ? { ...prev, comments: [...prev.comments, created] } : null));
      setNewCommentText("");
    } catch (err: any) {
      alert(err.response?.data?.message || "Failed to post comment.");
    } finally {
      setSubmittingComment(false);
    }
  };

  const handleStartEditComment = (comment: DiscussionComment) => {
    setEditingCommentId(comment.id);
    setEditingCommentText(comment.content);
  };

  const handleSaveEditComment = async (commentId: number) => {
    if (!post || !editingCommentText.trim()) return;
    try {
      const updated = await discussionService.updateComment(post.id, commentId, editingCommentText.trim());
      setPost((prev) =>
        prev
          ? {
              ...prev,
              comments: prev.comments.map((c) => (c.id === commentId ? updated : c))
            }
          : null
      );
      setEditingCommentId(null);
    } catch (err: any) {
      alert(err.response?.data?.message || "Failed to save comment.");
    }
  };

  const handleDeleteComment = async (commentId: number) => {
    if (!post) return;
    if (window.confirm("Are you sure you want to delete this comment?")) {
      try {
        await discussionService.deleteComment(post.id, commentId);
        setPost((prev) =>
          prev
            ? {
                ...prev,
                comments: prev.comments.filter((c) => c.id !== commentId)
              }
            : null
        );
      } catch {
        alert("Failed to delete comment.");
      }
    }
  };

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 10 }}>
        <CircularProgress size={40} />
      </Box>
    );
  }

  if (error || !post) {
    return (
      <Box sx={{ maxWidth: 900, mx: "auto", py: 4 }}>
        <Button component={Link} to="/discussions" startIcon={<ArrowBackIcon />} sx={{ mb: 2 }}>
          Back to Discussions
        </Button>
        <Alert severity="error">{error || "Discussion not found."}</Alert>
      </Box>
    );
  }

  const isAuthor = currentUserId === post.authorUserId;
  const canModify = isAuthor || canEditAny;
  const authorInitial = (post.authorName || "U").charAt(0).toUpperCase();

  return (
    <Box sx={{ maxWidth: 960, mx: "auto", pb: 8 }}>
      {/* Navigation Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2.5 }}>
        <Button
          component={Link}
          to="/discussions"
          startIcon={<ArrowBackIcon />}
          sx={{ fontWeight: 600, color: "text.secondary" }}
        >
          Back to Discussions
        </Button>

        {canModify && (
          <Box sx={{ display: "flex", gap: 1 }}>
            <Button
              variant="outlined"
              size="small"
              startIcon={<EditIcon />}
              onClick={() => setEditDialogOpen(true)}
            >
              Edit Post
            </Button>
            <IconButton size="small" onClick={(e) => setMenuAnchor(e.currentTarget)}>
              <MoreVertIcon fontSize="small" />
            </IconButton>
            <Menu anchorEl={menuAnchor} open={Boolean(menuAnchor)} onClose={() => setMenuAnchor(null)}>
              <MenuItem
                onClick={() => {
                  setMenuAnchor(null);
                  handleToggleImportant();
                }}
              >
                <ListItemIcon>
                  {post.isImportant ? <StarBorderIcon fontSize="small" /> : <StarIcon fontSize="small" />}
                </ListItemIcon>
                <ListItemText primary={post.isImportant ? "Unmark Important" : "Mark Important"} />
              </MenuItem>
              <MenuItem
                onClick={() => {
                  setMenuAnchor(null);
                  handleDeletePost();
                }}
                sx={{ color: "error.main" }}
              >
                <ListItemIcon sx={{ color: "error.main" }}>
                  <DeleteOutlineIcon fontSize="small" />
                </ListItemIcon>
                <ListItemText primary="Delete Discussion" />
              </MenuItem>
            </Menu>
          </Box>
        )}
      </Box>

      {/* Main Discussion Card */}
      <Paper variant="outlined" sx={{ p: 3.5, borderRadius: 2.5, mb: 3 }}>
        {/* Header with Author and Badges */}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 2, mb: 2.5, flexWrap: "wrap" }}>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
            <Avatar
              sx={{
                width: 44,
                height: 44,
                bgcolor: brandColors.sectionTitle,
                color: "#fff",
                fontWeight: 700,
                fontSize: 16
              }}
            >
              {authorInitial}
            </Avatar>
            <Box>
              <Typography sx={{ fontWeight: 700, fontSize: 14.5 }}>{post.authorName}</Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {post.authorRole} • Posted {new Date(post.createdAt).toLocaleString()}
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
                  color: "warning.dark"
                }}
              />
            )}
            <DiscussionCategoryBadge category={post.category} categoryName={post.categoryName} />
            {post.isEdited && (
              <Tooltip title="Click to view full revision history">
                <Chip
                  icon={<HistoryIcon sx={{ fontSize: "13px !important" }} />}
                  label={`v${post.currentVersion} Edited`}
                  size="small"
                  variant="outlined"
                  onClick={() => setHistoryOpen(true)}
                  sx={{ cursor: "pointer", fontSize: 11.5 }}
                />
              </Tooltip>
            )}
          </Box>
        </Box>

        {/* Discussion Title */}
        <Typography variant="h5" sx={{ fontWeight: 800, mb: 2, color: "text.primary", lineHeight: 1.3 }}>
          {post.title}
        </Typography>

        {/* Content Body */}
        <Typography
          variant="body1"
          sx={{
            whiteSpace: "pre-wrap",
            lineHeight: 1.7,
            color: "text.primary",
            fontSize: 14.5,
            mb: 3
          }}
        >
          {post.content}
        </Typography>

        {/* Attachments Section */}
        {post.attachments && post.attachments.length > 0 && (
          <Box sx={{ mt: 3, pt: 2.5, borderTop: "1px solid", borderColor: "divider" }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, display: "flex", alignItems: "center", gap: 0.75 }}>
              <AttachFileIcon fontSize="small" />
              Attachments ({post.attachments.length})
            </Typography>
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1.5 }}>
              {post.attachments.map((att) => (
                <Paper
                  key={att.id}
                  variant="outlined"
                  sx={{
                    p: 1.25,
                    px: 1.75,
                    display: "flex",
                    alignItems: "center",
                    gap: 1.5,
                    borderRadius: 2,
                    bgcolor: (theme) => (theme.palette.mode === "dark" ? "rgba(255,255,255,0.03)" : "rgba(0,0,0,0.02)")
                  }}
                >
                  <Box>
                    <Typography sx={{ fontWeight: 600, fontSize: 13, color: "text.primary" }}>
                      {att.fileName}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                      {(att.fileSizeBytes / 1024).toFixed(1)} KB
                    </Typography>
                  </Box>
                  <IconButton
                    size="small"
                    color="primary"
                    onClick={() => discussionService.downloadAttachment(post.id, att.id, att.fileName)}
                    title="Download file"
                  >
                    <DownloadIcon fontSize="small" />
                  </IconButton>
                </Paper>
              ))}
            </Box>
          </Box>
        )}
      </Paper>

      {/* Comments Thread */}
      <Box sx={{ mt: 4 }}>
        <Typography variant="h6" sx={{ fontWeight: 700, mb: 2.5 }}>
          Comments ({post.comments.length})
        </Typography>

        {/* Add Comment Input */}
        <Paper variant="outlined" sx={{ p: 2, borderRadius: 2.5, mb: 3 }}>
          <TextField
            placeholder="Write a comment or response to this discussion..."
            value={newCommentText}
            onChange={(e) => setNewCommentText(e.target.value)}
            fullWidth
            multiline
            minRows={2}
            maxRows={8}
            disabled={submittingComment}
            sx={{ mb: 1.5 }}
          />
          <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              variant="contained"
              color="primary"
              size="small"
              startIcon={submittingComment ? <CircularProgress size={14} color="inherit" /> : <SendIcon />}
              onClick={handleAddComment}
              disabled={submittingComment || !newCommentText.trim()}
              sx={{ fontWeight: 600 }}
            >
              {submittingComment ? "Posting..." : "Post Comment"}
            </Button>
          </Box>
        </Paper>

        {/* Flat Comments List */}
        {post.comments.length === 0 ? (
          <Typography variant="body2" sx={{ color: "text.secondary", textAlign: "center", py: 4 }}>
            No comments yet. Be the first to share your feedback or observations!
          </Typography>
        ) : (
          <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
            {post.comments.map((comment) => {
              const isCommentAuthor = currentUserId === comment.authorUserId;
              const canEditThisComment = isCommentAuthor || canEditAny;
              const isInlineEditing = editingCommentId === comment.id;
              const commentAuthorInitial = (comment.authorName || "U").charAt(0).toUpperCase();

              return (
                <Paper
                  key={comment.id}
                  variant="outlined"
                  sx={{
                    p: 2,
                    borderRadius: 2,
                    bgcolor: (theme) => (theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "background.paper")
                  }}
                >
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 1.25 }}>
                      <Avatar sx={{ width: 30, height: 30, fontSize: 12, bgcolor: brandColors.sectionTitle, color: "#fff" }}>
                        {commentAuthorInitial}
                      </Avatar>
                      <Box>
                        <Typography sx={{ fontWeight: 700, fontSize: 13, color: "text.primary", lineHeight: 1.1 }}>
                          {comment.authorName}
                        </Typography>
                        <Typography sx={{ fontSize: 11, color: "text.secondary", lineHeight: 1.1 }}>
                          {comment.authorRole} • {new Date(comment.createdAt).toLocaleString()}
                        </Typography>
                      </Box>
                    </Box>

                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                      {comment.isEdited && (
                        <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic", mr: 0.5 }}>
                          (Edited)
                        </Typography>
                      )}
                      {canEditThisComment && !isInlineEditing && (
                        <>
                          <IconButton size="small" onClick={() => handleStartEditComment(comment)}>
                            <EditIcon sx={{ fontSize: 15 }} />
                          </IconButton>
                          <IconButton size="small" color="error" onClick={() => handleDeleteComment(comment.id)}>
                            <DeleteOutlineIcon sx={{ fontSize: 15 }} />
                          </IconButton>
                        </>
                      )}
                    </Box>
                  </Box>

                  {/* Comment Content or Inline Editor */}
                  {isInlineEditing ? (
                    <Box sx={{ mt: 1.5 }}>
                      <TextField
                        fullWidth
                        multiline
                        size="small"
                        value={editingCommentText}
                        onChange={(e) => setEditingCommentText(e.target.value)}
                        sx={{ mb: 1 }}
                      />
                      <Box sx={{ display: "flex", gap: 1, justifyContent: "flex-end" }}>
                        <IconButton size="small" onClick={() => setEditingCommentId(null)}>
                          <CloseIcon fontSize="small" />
                        </IconButton>
                        <IconButton
                          size="small"
                          color="primary"
                          onClick={() => handleSaveEditComment(comment.id)}
                          disabled={!editingCommentText.trim()}
                        >
                          <CheckIcon fontSize="small" />
                        </IconButton>
                      </Box>
                    </Box>
                  ) : (
                    <Typography
                      variant="body2"
                      sx={{
                        whiteSpace: "pre-wrap",
                        color: "text.primary",
                        fontSize: 13.5,
                        lineHeight: 1.5,
                        pl: 4.75
                      }}
                    >
                      {comment.content}
                    </Typography>
                  )}
                </Paper>
              );
            })}
          </Box>
        )}
      </Box>

      {/* Edit Post Dialog */}
      <EditDiscussionDialog
        open={editDialogOpen}
        post={post}
        onClose={() => setEditDialogOpen(false)}
        onSaved={(updated) => {
          setEditDialogOpen(false);
          setPost(updated);
        }}
      />

      {/* History Dialog */}
      <DiscussionHistoryDialog
        open={historyOpen}
        postId={post.id}
        postTitle={post.title}
        onClose={() => setHistoryOpen(false)}
      />
    </Box>
  );
}
