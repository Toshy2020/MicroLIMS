import { useState } from "react";
import {
  Box,
  List,
  ListItemButton,
  ListItemAvatar,
  ListItemText,
  Avatar,
  Typography,
  Badge,
  TextField,
  InputAdornment,
  Divider
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import GroupIcon from "@mui/icons-material/Group";
import { ConversationSummary } from "../types/messageTypes";
import { brandColors } from "../../../theme";

interface Props {
  conversations: ConversationSummary[];
  selectedId: number | null;
  onSelect: (conv: ConversationSummary) => void;
  currentUserId?: number;
}

export function ConversationList({ conversations, selectedId, onSelect, currentUserId }: Props) {
  const [filter, setFilter] = useState<string>("");

  const filtered = conversations.filter((c) => {
    if (!filter.trim()) return true;
    const query = filter.toLowerCase();
    const titleMatch = c.title?.toLowerCase().includes(query);
    const participantMatch = c.participants.some(
      (p) => p.fullName.toLowerCase().includes(query) || p.username.toLowerCase().includes(query)
    );
    const lastMsgMatch = c.lastMessage?.content.toLowerCase().includes(query);
    return Boolean(titleMatch || participantMatch || lastMsgMatch);
  });

  const formatTimestamp = (dateStr: string) => {
    const d = new Date(dateStr);
    const now = new Date();
    const isToday =
      d.getDate() === now.getDate() &&
      d.getMonth() === now.getMonth() &&
      d.getFullYear() === now.getFullYear();

    if (isToday) {
      return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    }
    return d.toLocaleDateString([], { month: "short", day: "numeric" });
  };

  return (
    <Box sx={{ display: "flex", flexDirection: "column", height: "100%", width: "100%" }}>
      {/* Search Filter */}
      <Box sx={{ p: 1.5, pb: 1 }}>
        <TextField
          placeholder="Filter conversations..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          size="small"
          fullWidth
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
              </InputAdornment>
            )
          }}
        />
      </Box>
      <Divider />

      {/* List */}
      <List sx={{ flex: 1, overflowY: "auto", p: 0.5 }}>
        {filtered.length === 0 ? (
          <Typography sx={{ p: 3, textAlign: "center", color: "text.secondary", fontSize: 13 }}>
            {conversations.length === 0
              ? "No conversations yet. Start a new message!"
              : "No matching conversations found."}
          </Typography>
        ) : (
          filtered.map((c) => {
            const isSelected = c.id === selectedId;
            const otherParticipant = c.participants.find((p) => p.userId !== currentUserId);
            const displayTitle = c.title || otherParticipant?.fullName || "Conversation";
            const initial = displayTitle.charAt(0).toUpperCase();

            return (
              <ListItemButton
                key={c.id}
                selected={isSelected}
                onClick={() => onSelect(c)}
                sx={{
                  borderRadius: 2,
                  mb: 0.5,
                  alignItems: "flex-start",
                  py: 1.25,
                  px: 1.5,
                  "&.Mui-selected": {
                    bgcolor: (theme) =>
                      theme.palette.mode === "dark" ? "rgba(99, 102, 241, 0.15)" : "rgba(99, 102, 241, 0.08)",
                    borderLeft: "3px solid",
                    borderColor: "primary.main"
                  }
                }}
              >
                <ListItemAvatar sx={{ minWidth: 44 }}>
                  <Badge badgeContent={c.unreadCount} color="error" overlap="circular">
                    <Avatar
                      sx={{
                        width: 36,
                        height: 36,
                        bgcolor: c.isGroup ? "primary.dark" : brandColors.sectionTitle,
                        fontSize: 13,
                        fontWeight: 700
                      }}
                    >
                      {c.isGroup ? <GroupIcon fontSize="small" /> : initial}
                    </Avatar>
                  </Badge>
                </ListItemAvatar>
                <ListItemText
                  primary={
                    <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 0.25 }}>
                      <Typography
                        sx={{
                          fontWeight: c.unreadCount > 0 ? 800 : isSelected ? 700 : 600,
                          fontSize: 13,
                          color: "text.primary",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                          maxWidth: 150
                        }}
                      >
                        {displayTitle}
                      </Typography>
                      <Typography sx={{ fontSize: 10.5, color: "text.secondary", flexShrink: 0 }}>
                        {formatTimestamp(c.lastMessageAt)}
                      </Typography>
                    </Box>
                  }
                  secondary={
                    <Typography
                      variant="body2"
                      sx={{
                        fontSize: 12,
                        color: c.unreadCount > 0 ? "text.primary" : "text.secondary",
                        fontWeight: c.unreadCount > 0 ? 600 : 400,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap"
                      }}
                    >
                      {c.lastMessage
                        ? `${c.lastMessage.senderUserId === currentUserId ? "You: " : ""}${c.lastMessage.content}`
                        : "No messages yet"}
                    </Typography>
                  }
                />
              </ListItemButton>
            );
          })
        )}
      </List>
    </Box>
  );
}
