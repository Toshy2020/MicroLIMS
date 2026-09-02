import { useState, ChangeEvent } from "react";
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
  Typography,
  Chip,
  Alert,
  CircularProgress
} from "@mui/material";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import AddCommentIcon from "@mui/icons-material/AddComment";
import { DISCUSSION_CATEGORIES, DiscussionCategory } from "../types/discussionTypes";
import { discussionService } from "../services/discussionService";

interface Props {
  open: boolean;
  onClose: () => void;
  onCreated: (createdId: number) => void;
}

export function NewDiscussionDialog({ open, onClose, onCreated }: Props) {
  const [title, setTitle] = useState<string>("");
  const [content, setContent] = useState<string>("");
  const [category, setCategory] = useState<DiscussionCategory>(DiscussionCategory.Water);
  const [isImportant, setIsImportant] = useState<boolean>(false);
  const [files, setFiles] = useState<File[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const handleFileChange = (e: ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const selected = Array.from(e.target.files);
      setFiles((prev) => [...prev, ...selected]);
    }
  };

  const handleRemoveFile = (index: number) => {
    setFiles((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = async () => {
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
      const formData = new FormData();
      formData.append("title", title.trim());
      formData.append("content", content.trim());
      formData.append("category", category.toString());
      formData.append("isImportant", isImportant.toString());

      files.forEach((file) => {
        formData.append("files", file);
      });

      const res = await discussionService.createPost(formData);
      handleReset();
      onCreated(res.id);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to create discussion.");
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setTitle("");
    setContent("");
    setCategory(DiscussionCategory.Water);
    setIsImportant(false);
    setFiles([]);
    setError(null);
  };

  return (
    <Dialog open={open} onClose={loading ? undefined : onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1, fontWeight: 700 }}>
        <AddCommentIcon color="primary" />
        New Discussion
      </DialogTitle>
      <DialogContent sx={{ display: "flex", flexDirection: "column", gap: 2, pt: 1 }}>
        {error && <Alert severity="error">{error}</Alert>}

        <TextField
          label="Title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="What is this discussion about?"
          fullWidth
          required
          disabled={loading}
          autoFocus
        />

        <Box sx={{ display: "flex", gap: 2, alignItems: "center", flexWrap: "wrap" }}>
          <FormControl sx={{ minWidth: 260 }}>
            <InputLabel id="category-label">Category</InputLabel>
            <Select
              labelId="category-label"
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
            label="Mark as Important (Pinned at top)"
          />
        </Box>

        <TextField
          label="Content / Details"
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder="Provide context, observations, questions, or regulatory references..."
          fullWidth
          multiline
          minRows={5}
          maxRows={12}
          required
          disabled={loading}
        />

        {/* Attachments Picker */}
        <Box sx={{ border: "1px dashed", borderColor: "divider", p: 2, borderRadius: 2 }}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
              Attachments (Documents, Images, Worksheets)
            </Typography>
            <Button
              variant="outlined"
              component="label"
              size="small"
              startIcon={<AttachFileIcon />}
              disabled={loading}
            >
              Choose Files
              <input type="file" multiple hidden onChange={handleFileChange} />
            </Button>
          </Box>

          {files.length === 0 ? (
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              No files attached yet. Supported: PDF, Word, Excel, images, text files.
            </Typography>
          ) : (
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1, mt: 1 }}>
              {files.map((file, idx) => (
                <Chip
                  key={idx}
                  label={`${file.name} (${(file.size / 1024).toFixed(1)} KB)`}
                  onDelete={loading ? undefined : () => handleRemoveFile(idx)}
                  variant="outlined"
                  size="small"
                />
              ))}
            </Box>
          )}
        </Box>
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
          startIcon={loading ? <CircularProgress size={16} color="inherit" /> : <AddCommentIcon />}
        >
          {loading ? "Publishing..." : "Publish Discussion"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
