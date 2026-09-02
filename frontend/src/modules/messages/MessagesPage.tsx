import { useEffect, useState, useCallback } from "react";
import {
  Box,
  Paper,
  Button,
  Typography,
  CircularProgress,
  useTheme,
  useMediaQuery,
  Alert
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import ChatBubbleOutlineIcon from "@mui/icons-material/ChatBubbleOutline";
import { PageHeader } from "../../components/PageHeader";
import { useAuth } from "../../contexts/AuthContext";
import { ConversationSummary } from "./types/messageTypes";
import { messageService } from "./services/messageService";
import { ConversationList } from "./components/ConversationList";
import { ChatWindow } from "./components/ChatWindow";
import { NewConversationDialog } from "./components/NewConversationDialog";

export function MessagesPage() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("md"));
  const { userId } = useAuth();
  const currentUserId = userId ?? undefined;

  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [newDialogOpen, setNewDialogOpen] = useState<boolean>(false);

  const loadConversations = useCallback(async () => {
    try {
      const data = await messageService.getConversations();
      setConversations(data);
      if (data.length > 0 && selectedId === null && !isMobile) {
        setSelectedId(data[0].id);
      }
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || "Failed to load conversations.");
    } finally {
      setLoading(false);
    }
  }, [selectedId, isMobile]);

  useEffect(() => {
    loadConversations();
    const interval = setInterval(loadConversations, 10_000);
    return () => clearInterval(interval);
  }, [loadConversations]);

  const selectedConversation = conversations.find((c) => c.id === selectedId) || null;

  const handleSelectConversation = (conv: ConversationSummary) => {
    setSelectedId(conv.id);
    // Mark as read in local state
    setConversations((prev) =>
      prev.map((c) => (c.id === conv.id ? { ...c, unreadCount: 0 } : c))
    );
  };

  const handleConversationCreated = (conv: ConversationSummary) => {
    setNewDialogOpen(false);
    setConversations((prev) => [conv, ...prev.filter((c) => c.id !== conv.id)]);
    setSelectedId(conv.id);
  };

  return (
    <Box sx={{ maxWidth: 1200, mx: "auto", pb: 4, height: "calc(100vh - 120px)", display: "flex", flexDirection: "column" }}>
      {/* Top Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5, flexShrink: 0 }}>
        <PageHeader
          title="Direct & Group Messages"
          subtitle="Real-time workplace discussions with laboratory analysts, reviewers, and section heads."
        />
        <Button
          variant="contained"
          color="primary"
          startIcon={<AddIcon />}
          onClick={() => setNewDialogOpen(true)}
          sx={{ fontWeight: 600, mt: 0.5 }}
        >
          New Message
        </Button>
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2, flexShrink: 0 }}>
          {error}
        </Alert>
      )}

      {/* Split-Pane Message Box */}
      <Paper
        variant="outlined"
        sx={{
          flex: 1,
          display: "flex",
          overflow: "hidden",
          borderRadius: 2.5,
          minHeight: 450
        }}
      >
        {loading && conversations.length === 0 ? (
          <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", width: "100%" }}>
            <CircularProgress size={36} />
          </Box>
        ) : (
          <>
            {/* Left Pane: Conversation List */}
            <Box
              sx={{
                width: { xs: selectedId ? "0%" : "100%", md: 340 },
                display: { xs: selectedId ? "none" : "flex", md: "flex" },
                borderRight: { md: "1px solid" },
                borderColor: { md: "divider" },
                flexShrink: 0,
                flexDirection: "column",
                height: "100%"
              }}
            >
              <ConversationList
                conversations={conversations}
                selectedId={selectedId}
                onSelect={handleSelectConversation}
                currentUserId={currentUserId}
              />
            </Box>

            {/* Right Pane: Chat Window or Empty State */}
            <Box
              sx={{
                flex: 1,
                display: { xs: selectedId ? "flex" : "none", md: "flex" },
                flexDirection: "column",
                height: "100%",
                minWidth: 0
              }}
            >
              {selectedConversation ? (
                <ChatWindow
                  conversation={selectedConversation}
                  currentUserId={currentUserId}
                  onBack={isMobile ? () => setSelectedId(null) : undefined}
                  onMessageSent={loadConversations}
                />
              ) : (
                <Box
                  sx={{
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                    height: "100%",
                    p: 4,
                    color: "text.secondary"
                  }}
                >
                  <ChatBubbleOutlineIcon sx={{ fontSize: 56, color: "text.disabled", mb: 1.5 }} />
                  <Typography variant="h6" sx={{ fontWeight: 600, mb: 0.5 }}>
                    Select a conversation
                  </Typography>
                  <Typography variant="body2" sx={{ mb: 2, textAlign: "center", maxWidth: 300 }}>
                    Choose a conversation from the list or start a new message.
                  </Typography>
                  <Button
                    variant="outlined"
                    color="primary"
                    startIcon={<AddIcon />}
                    onClick={() => setNewDialogOpen(true)}
                  >
                    New Message
                  </Button>
                </Box>
              )}
            </Box>
          </>
        )}
      </Paper>

      {/* New Conversation Dialog */}
      <NewConversationDialog
        open={newDialogOpen}
        currentUserId={currentUserId}
        onClose={() => setNewDialogOpen(false)}
        onCreated={handleConversationCreated}
      />
    </Box>
  );
}
