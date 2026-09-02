import { useEffect, useRef, useState, KeyboardEvent } from "react";
import {
  Box,
  Typography,
  Avatar,
  IconButton,
  TextField,
  Divider,
  CircularProgress,
  Tooltip
} from "@mui/material";
import SendIcon from "@mui/icons-material/Send";
import GroupIcon from "@mui/icons-material/Group";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { ConversationSummary, DirectMessage } from "../types/messageTypes";
import { messageService } from "../services/messageService";
import { brandColors } from "../../../theme";

interface Props {
  conversation: ConversationSummary;
  currentUserId?: number;
  onBack?: () => void;
  onMessageSent: () => void;
}

export function ChatWindow({ conversation, currentUserId, onBack, onMessageSent }: Props) {
  const [messages, setMessages] = useState<DirectMessage[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [text, setText] = useState<string>("");
  const [sending, setSending] = useState<boolean>(false);

  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  const loadMessages = async () => {
    try {
      const list = await messageService.getMessages(conversation.id);
      setMessages(list);
      // Mark as read
      await messageService.markAsRead(conversation.id);
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setLoading(true);
    loadMessages();

    // Poll for new messages every 6 seconds while chatting
    const interval = setInterval(loadMessages, 6_000);
    return () => clearInterval(interval);
  }, [conversation.id]);

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const handleSend = async () => {
    if (!text.trim() || sending) return;
    const content = text.trim();
    setText("");
    setSending(true);

    try {
      const msg = await messageService.sendMessage(conversation.id, content);
      setMessages((prev) => [...prev, msg]);
      onMessageSent();
    } catch (err: any) {
      alert("Failed to send message.");
    } finally {
      setSending(false);
    }
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const otherParticipant = conversation.participants.find((p) => p.userId !== currentUserId);
  const displayTitle = conversation.title || otherParticipant?.fullName || "Conversation";
  const initial = displayTitle.charAt(0).toUpperCase();

  const participantsTooltip = conversation.participants
    .map((p) => `${p.fullName} (${p.roleName})`)
    .join(", ");

  return (
    <Box sx={{ display: "flex", flexDirection: "column", height: "100%", width: "100%" }}>
      {/* Header */}
      <Box
        sx={{
          p: 1.5,
          px: 2,
          display: "flex",
          alignItems: "center",
          gap: 1.5,
          borderBottom: "1px solid",
          borderColor: "divider",
          flexShrink: 0
        }}
      >
        {onBack && (
          <IconButton onClick={onBack} size="small" sx={{ mr: 0.5, display: { md: "none" } }}>
            <ArrowBackIcon fontSize="small" />
          </IconButton>
        )}

        <Avatar
          sx={{
            width: 38,
            height: 38,
            bgcolor: conversation.isGroup ? "primary.dark" : brandColors.sectionTitle,
            fontWeight: 700,
            fontSize: 14
          }}
        >
          {conversation.isGroup ? <GroupIcon fontSize="small" /> : initial}
        </Avatar>

        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography sx={{ fontWeight: 700, fontSize: 14, color: "text.primary" }} noWrap>
            {displayTitle}
          </Typography>
          <Tooltip title={participantsTooltip}>
            <Typography sx={{ fontSize: 11.5, color: "text.secondary", cursor: "pointer" }} noWrap>
              {conversation.isGroup
                ? `${conversation.participants.length} members: ${conversation.participants.map((p) => p.fullName).join(", ")}`
                : otherParticipant?.jobTitle
                ? `${otherParticipant.jobTitle} • ${otherParticipant.roleName}`
                : otherParticipant?.roleName || "Colleague"}
            </Typography>
          </Tooltip>
        </Box>
      </Box>

      {/* Messages Thread */}
      <Box
        sx={{
          flex: 1,
          overflowY: "auto",
          p: 2,
          display: "flex",
          flexDirection: "column",
          gap: 1.5,
          bgcolor: (theme) => (theme.palette.mode === "dark" ? "rgba(0,0,0,0.15)" : "rgba(0,0,0,0.02)")
        }}
      >
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100%" }}>
            <CircularProgress size={30} />
          </Box>
        ) : messages.length === 0 ? (
          <Box sx={{ textAlign: "center", my: "auto", py: 6, color: "text.secondary" }}>
            <Typography variant="body2">No messages in this conversation yet.</Typography>
            <Typography variant="caption">Say hello to start the conversation!</Typography>
          </Box>
        ) : (
          messages.map((m) => {
            const isMine = m.senderUserId === currentUserId;
            const time = new Date(m.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

            return (
              <Box
                key={m.id}
                sx={{
                  display: "flex",
                  flexDirection: "column",
                  alignItems: isMine ? "flex-end" : "flex-start",
                  maxWidth: "75%",
                  alignSelf: isMine ? "flex-end" : "flex-start"
                }}
              >
                {!isMine && conversation.isGroup && (
                  <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", mb: 0.25, px: 1 }}>
                    {m.senderName}
                  </Typography>
                )}

                <Box
                  sx={{
                    p: 1.5,
                    px: 1.75,
                    borderRadius: 2.5,
                    borderTopRightRadius: isMine ? 0.5 : 2.5,
                    borderTopLeftRadius: !isMine ? 0.5 : 2.5,
                    bgcolor: isMine ? "primary.main" : "background.paper",
                    color: isMine ? "#fff" : "text.primary",
                    boxShadow: "0 1px 3px rgba(0,0,0,0.06)",
                    border: isMine ? "none" : "1px solid",
                    borderColor: "divider",
                    wordBreak: "break-word"
                  }}
                >
                  <Typography sx={{ fontSize: 13.5, whiteSpace: "pre-wrap", lineHeight: 1.45 }}>
                    {m.content}
                  </Typography>
                </Box>

                <Typography sx={{ fontSize: 10, color: "text.disabled", mt: 0.25, px: 0.75 }}>
                  {time}
                </Typography>
              </Box>
            );
          })
        )}
        <div ref={messagesEndRef} />
      </Box>

      {/* Message Input Footer */}
      <Divider />
      <Box sx={{ p: 1.5, display: "flex", alignItems: "flex-end", gap: 1 }}>
        <TextField
          placeholder="Write a message... (Press Enter to send)"
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={handleKeyDown}
          fullWidth
          multiline
          maxRows={4}
          size="small"
          disabled={sending}
          sx={{
            "& .MuiOutlinedInput-root": {
              borderRadius: 2
            }
          }}
        />
        <IconButton
          color="primary"
          onClick={handleSend}
          disabled={!text.trim() || sending}
          sx={{
            bgcolor: "primary.main",
            color: "#fff",
            "&:hover": { bgcolor: "primary.dark" },
            "&.Mui-disabled": { bgcolor: "action.disabledBackground", color: "action.disabled" },
            p: 1.25
          }}
        >
          <SendIcon fontSize="small" />
        </IconButton>
      </Box>
    </Box>
  );
}
