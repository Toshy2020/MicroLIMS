import { useState, useEffect } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  FormControlLabel,
  Checkbox,
  Box,
  Alert,
  CircularProgress
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import { DISCUSSION_CATEGORIES, DiscussionCategory, DiscussionPostDetail } from "../types/discussionTypes";
import { discussionService } from "../services/discussionService";

interface Props {
  open: boolean;
  post: DiscussionPostDetail | null;
  onClose: () => void;
  onSaved: (updated: DiscussionPostDetail) => void;
}

export function EditDiscussionDialog({ open, post, onClose, onSaved }: Props) {
  const [title, setTitle] = useState<string>("");
  const [content, setContent] = useState<string>("");
  const [category, setCategory] = useState<DiscussionCategory>(DiscussionCategory.Water);
  const [isImportant, setIsImportant] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (post) {
      setTitle(post.title);
      setContent(post.content);
      setCategory(post.category);
      setIsImportant(post.isImportant);
      setError(null);
    }
  }, [post, open]);

  const handleSubmit = async () => {
    if (!post) return;
    if (!title.trim()) {
      setError("Discussion title is required.");
      return;
    }
    if (!content.trim()) {
      setError("Discussion content is required.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const updated = await discussionService.updatePost(post.id, {
        title: title.trim(),
        content: content.trim(),
        category,
        isImportant
      });
      onSaved(updated);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to update discussion.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onClose={loading ? undefined : onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1, fontWeight: 700 }}>
        <EditIcon color="primary" />
        Edit Discussion (Version {post?.currentVersion})
      </DialogTitle>
      <DialogContent sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}

        <Alert severity="info" sx={{ fontSize: 12.5 }}>
          Editing will create a historical snapshot of this post and notify discussion participants.
        </Alert>

        <TextField
          label="Title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          fullWidth
          required
          disabled={loading}
          autoFocus
        />

        <Box sx={{ display: "flex", gap: 2, alignItems: "center", flexWrap: "wrap" }}>
          <FormControl sx={{ minWidth: 260 }}>
            <InputLabel id="edit-category-label">Category</InputLabel>
            <Select
              labelId="edit-category-label"
              value={category}
              label="Category"
              onChange={(e) => setCategory(Number(e.target.value) as DiscussionCategory)}
              disabled={loading}
            >
              {DISCUSSION_CATEGORIES.map((cat) => (
                <MenuItem key={cat.id} value={cat.id}>
                  {cat.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControlLabel
            control={
              <Checkbox
                checked={isImportant}
                onChange={(e) => setIsImportant(e.target.checked)}
                color="warning"
                disabled={loading}
              />
            }
            label="Mark as Important"
          />
        </Box>

        <TextField
          label="Content / Details"
          value={content}
          onChange={(e) => setContent(e.target.value)}
          fullWidth
          multiline
          minRows={6}
          maxRows={14}
          required
          disabled={loading}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={loading} color="inherit">
          Cancel
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          color="primary"
          disabled={loading || !title.trim() || !content.trim()}
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : <EditIcon />}
        >
          {loading ? "Saving Changes..." : "Save Changes"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
