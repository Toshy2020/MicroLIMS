import { useEffect, useState, useCallback } from "react";
import {
  Box,
  Typography,
  Button,
  TextField,
  InputAdornment,
  Chip,
  FormControlLabel,
  Checkbox,
  CircularProgress,
  Pagination,
  Paper,
  Alert
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import AddCommentIcon from "@mui/icons-material/AddComment";
import ForumOutlinedIcon from "@mui/icons-material/ForumOutlined";
import StarIcon from "@mui/icons-material/Star";
import { PageHeader } from "../../components/PageHeader";
import { useAuth } from "../../contexts/AuthContext";
import { DISCUSSION_CATEGORIES, DiscussionCategory, DiscussionPostSummary } from "./types/discussionTypes";
import { discussionService } from "./services/discussionService";
import { DiscussionCard } from "./components/DiscussionCard";
import { NewDiscussionDialog } from "./components/NewDiscussionDialog";
import { DiscussionHistoryDialog } from "./components/DiscussionHistoryDialog";

export function DiscussionsFeedPage() {
  const { userId, role } = useAuth();
  const currentUserId = userId ?? undefined;
  const canEditAny = role === "SystemAdministrator" || role === "SectionHead";

  const [posts, setPosts] = useState<DiscussionPostSummary[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [totalPages, setTotalPages] = useState<number>(1);
  const [page, setPage] = useState<number>(1);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [selectedCategory, setSelectedCategory] = useState<DiscussionCategory | undefined>(undefined);
  const [search, setSearch] = useState<string>("");
  const [importantOnly, setImportantOnly] = useState<boolean>(false);

  // Dialogs
  const [newDialogOpen, setNewDialogOpen] = useState<boolean>(false);
  const [historyTarget, setHistoryTarget] = useState<{ id: number; title: string } | null>(null);

  const loadFeed = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await discussionService.getFeed(selectedCategory, search, importantOnly, page, 15);
      setPosts(res.items);
      setTotalCount(res.totalCount);
      setTotalPages(res.totalPages || 1);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to load discussions.");
    } finally {
      setLoading(false);
    }
  }, [selectedCategory, search, importantOnly, page]);

  useEffect(() => {
    loadFeed();
  }, [loadFeed]);

  const handleToggleImportant = async (id: number) => {
    try {
      const isImportant = await discussionService.toggleImportant(id);
      setPosts((prev) =>
        prev.map((p) => (p.id === id ? { ...p, isImportant } : p))
      );
    } catch (err: any) {
      alert("Failed to toggle important status.");
    }
  };

  const handleDeletePost = async (id: number) => {
    try {
      await discussionService.deletePost(id);
      setPosts((prev) => prev.filter((p) => p.id !== id));
      setTotalCount((c) => Math.max(0, c - 1));
    } catch (err: any) {
      alert("Failed to delete discussion post.");
    }
  };

  return (
    <Box sx={{ maxWidth: 1000, mx: "auto", pb: 6 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 2 }}>
        <PageHeader
          title="Discussions & Knowledge Sharing"
          subtitle="Collaborative laboratory discussions, technical queries, compliance alerts, and knowledge exchange."
        />
        <Button
          variant="contained"
          color="primary"
          startIcon={<AddCommentIcon />}
          onClick={() => setNewDialogOpen(true)}
          sx={{ fontWeight: 600, mt: 0.5 }}
        >
          New Discussion
        </Button>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      {/* Filter and Search Bar */}
      <Paper variant="outlined" sx={{ p: 2, mb: 3, borderRadius: 2.5 }}>
        <Box sx={{ display: "flex", gap: 2, alignItems: "center", flexWrap: "wrap", mb: 2 }}>
          <TextField
            placeholder="Search discussions by title or content..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            size="small"
            sx={{ flex: 1, minWidth: 260 }}
            InputProps={{
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
                </InputAdornment>
              )
            }}
          />

          <FormControlLabel
            control={
              <Checkbox
                checked={importantOnly}
                onChange={(e) => {
                  setImportantOnly(e.target.checked);
                  setPage(1);
                }}
                color="warning"
                icon={<StarIcon sx={{ color: "text.disabled", fontSize: 20 }} />}
                checkedIcon={<StarIcon sx={{ color: "warning.main", fontSize: 20 }} />}
              />
            }
            label={<Typography sx={{ fontSize: 13, fontWeight: 600 }}>Important Only</Typography>}
          />
        </Box>

        {/* 11 Frozen Categories Filter Chips */}
        <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
          <Chip
            label="All Categories"
            onClick={() => {
              setSelectedCategory(undefined);
              setPage(1);
            }}
            color={selectedCategory === undefined ? "primary" : "default"}
            variant={selectedCategory === undefined ? "filled" : "outlined"}
            sx={{ fontWeight: 600, fontSize: 12 }}
          />
          {DISCUSSION_CATEGORIES.map((cat) => (
            <Chip
              key={cat.id}
              label={cat.name}
              onClick={() => {
                setSelectedCategory(cat.id === selectedCategory ? undefined : cat.id);
                setPage(1);
              }}
              color={selectedCategory === cat.id ? "primary" : "default"}
              variant={selectedCategory === cat.id ? "filled" : "outlined"}
              sx={{ fontWeight: selectedCategory === cat.id ? 700 : 500, fontSize: 12 }}
            />
          ))}
        </Box>
      </Paper>

      {/* Feed List */}
      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", py: 8 }}>
          <CircularProgress size={36} />
        </Box>
      ) : posts.length === 0 ? (
        <Paper
          variant="outlined"
          sx={{
            p: 6,
            textAlign: "center",
            borderRadius: 2.5,
            bgcolor: (theme) => (theme.palette.mode === "dark" ? "rgba(255,255,255,0.02)" : "rgba(0,0,0,0.01)")
          }}
        >
          <ForumOutlinedIcon sx={{ fontSize: 48, color: "text.disabled", mb: 1 }} />
          <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.5 }}>
            No discussions found
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mb: 2 }}>
            {search || selectedCategory || importantOnly
              ? "Try adjusting your search query or category filters."
              : "Be the first to start a conversation in this category."}
          </Typography>
          <Button
            variant="outlined"
            color="primary"
            startIcon={<AddCommentIcon />}
            onClick={() => setNewDialogOpen(true)}
          >
            Start a Discussion
          </Button>
        </Paper>
      ) : (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
          <Typography variant="body2" sx={{ color: "text.secondary", px: 0.5 }}>
            Showing {posts.length} of {totalCount} discussions
          </Typography>

          {posts.map((post) => (
            <DiscussionCard
              key={post.id}
              post={post}
              currentUserId={currentUserId}
              canEditAny={canEditAny}
              onToggleImportant={handleToggleImportant}
              onDelete={handleDeletePost}
              onOpenHistory={(id, title) => setHistoryTarget({ id, title })}
            />
          ))}

          {/* Pagination */}
          {totalPages > 1 && (
            <Box sx={{ display: "flex", justifyContent: "center", mt: 3 }}>
              <Pagination
                count={totalPages}
                page={page}
                onChange={(_, val) => setPage(val)}
                color="primary"
                shape="rounded"
              />
            </Box>
          )}
        </Box>
      )}

      {/* New Discussion Modal */}
      <NewDiscussionDialog
        open={newDialogOpen}
        onClose={() => setNewDialogOpen(false)}
        onCreated={() => {
          setNewDialogOpen(false);
          setPage(1);
          loadFeed();
        }}
      />

      {/* History Dialog */}
      {historyTarget && (
        <DiscussionHistoryDialog
          open={Boolean(historyTarget)}
          postId={historyTarget.id}
          postTitle={historyTarget.title}
          onClose={() => setHistoryTarget(null)}
        />
      )}
    </Box>
  );
}
