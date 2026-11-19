import { useEffect, useState } from "react";
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  FormControlLabel,
  RadioGroup,
  Radio,
  FormControl,
  FormLabel,
  Autocomplete,
  Chip,
  Box,
  Typography,
  CircularProgress,
  Alert,
  Avatar
} from "@mui/material";
import AddCommentIcon from "@mui/icons-material/AddComment";
import GroupIcon from "@mui/icons-material/Group";
import PersonIcon from "@mui/icons-material/Person";
import { messageService } from "../services/messageService";
import { UserDirectoryItem, ConversationSummary } from "../types/messageTypes";
import { brandColors } from "../../../theme";
import { useTranslation } from "../../../i18n";

interface Props {
  open: boolean;
  currentUserId?: number;
  onClose: () => void;
  onCreated: (conversation: ConversationSummary) => void;
}

export function NewConversationDialog({ open, currentUserId, onClose, onCreated }: Props) {
  const { t } = useTranslation("messages");
  const [isGroup, setIsGroup] = useState<boolean>(false);
  const [title, setTitle] = useState<string>("");
  const [selectedUsers, setSelectedUsers] = useState<UserDirectoryItem[]>([]);
  const [initialMessage, setInitialMessage] = useState<string>("");

  const [directory, setDirectory] = useState<UserDirectoryItem[]>([]);
  const [loadingDirectory, setLoadingDirectory] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [directoryLoadFailed, setDirectoryLoadFailed] = useState(false);

  useEffect(() => {
    if (open) {
      setLoadingDirectory(true);
      setDirectoryLoadFailed(false);
      messageService
        .getUserDirectory()
        .then((users) => {
          // Exclude self from recipient list
          setDirectory(users.filter((u) => u.id !== currentUserId && u.isActive));
        })
        .catch(() => {
          setDirectory([]);
          setDirectoryLoadFailed(true);
        })
        .finally(() => setLoadingDirectory(false));
    }
  }, [open, currentUserId]);

  const handleSubmit = async () => {
    if (selectedUsers.length === 0) {
      setError(t("dialog.noRecipient"));
      return;
    }
    if (isGroup && !title.trim()) {
      setError(t("dialog.groupTitleRequired"));
      return;
    }
    if (!initialMessage.trim()) {
      setError(t("dialog.initialEmpty"));
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const conv = await messageService.createConversation({
        title: isGroup ? title.trim() : null,
        isGroup,
        participantUserIds: selectedUsers.map((u) => u.id),
        initialMessage: initialMessage.trim()
      });
      handleReset();
      onCreated(conv);
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { message?: string } }; message?: string };
      setError(apiErr.response?.data?.message || apiErr.message || t("dialog.createFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  const handleReset = () => {
    setIsGroup(false);
    setTitle("");
    setSelectedUsers([]);
    setInitialMessage("");
    setError(null);
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: "flex", alignItems: "center", gap: 1, fontWeight: 700 }}>
        <AddCommentIcon color="primary" />
        {t("page.newMessage")}
      </DialogTitle>
      <DialogContent sx={{ display: "flex", flexDirection: "column", gap: 2.5, pt: 1 }}>
        {directoryLoadFailed && (
          <Alert severity="error">{t("dialog.directoryFailed")}</Alert>
        )}
        {error && <Alert severity="error">{error}</Alert>}

        <FormControl component="fieldset">
          <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 600 }}>
            {t("dialog.conversationType")}
          </FormLabel>
          <RadioGroup
            row
            value={isGroup ? "group" : "direct"}
            onChange={(e) => {
              const group = e.target.value === "group";
              setIsGroup(group);
              if (!group && selectedUsers.length > 1) {
                setSelectedUsers([selectedUsers[0]]);
              }
            }}
          >
            <FormControlLabel
              value="direct"
              control={<Radio size="small" />}
              label={
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                  <PersonIcon fontSize="small" sx={{ color: "text.secondary" }} />
                  <Typography variant="body2">{t("dialog.direct")}</Typography>
                </Box>
              }
            />
            <FormControlLabel
              value="group"
              control={<Radio size="small" />}
              label={
                <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                  <GroupIcon fontSize="small" sx={{ color: "text.secondary" }} />
                  <Typography variant="body2">{t("dialog.group")}</Typography>
                </Box>
              }
            />
          </RadioGroup>
        </FormControl>

        {isGroup && (
          <TextField
            label={t("dialog.groupTitle")}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder={t("dialog.groupPlaceholder")}
            fullWidth
            required
            size="small"
            disabled={submitting}
          />
        )}

        <Autocomplete
          multiple={isGroup}
          options={directory}
          loading={loadingDirectory}
          getOptionLabel={(option) => `${option.fullName} (${option.roleName})`}
          value={isGroup ? selectedUsers : selectedUsers[0] || null}
          onChange={(_, newValue) => {
            if (isGroup) {
              setSelectedUsers((newValue as UserDirectoryItem[]) || []);
            } else {
              setSelectedUsers(newValue ? [newValue as UserDirectoryItem] : []);
            }
          }}
          renderInput={(params) => (
            <TextField
              {...params}
              label={isGroup ? t("dialog.selectParticipants") : t("dialog.selectRecipient")}
              placeholder={t("dialog.searchPlaceholder")}
              size="small"
              required
              slotProps={{
                ...params.slotProps,

                input: {
                  ...params.slotProps.input,
                  endAdornment: (
                    <>
                      {loadingDirectory ? <CircularProgress color="inherit" size={18} /> : null}
                      {params.slotProps.input.endAdornment}
                    </>
                  )
                }
              }}
            />
          )}
          renderOption={(props, option) => (
            <Box component="li" {...props} key={option.id} sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 1 }}>
              <Avatar sx={{ width: 28, height: 28, fontSize: 12, bgcolor: brandColors.sectionTitle }}>
                {option.fullName.charAt(0).toUpperCase()}
              </Avatar>
              <Box>
                <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{option.fullName}</Typography>
                <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                  {t("dialog.roleJob", {
                    role: option.roleName,
                    job: option.jobTitle ? t("dialog.jobTitle", { jobTitle: option.jobTitle }) : ""
                  })}
                </Typography>
              </Box>
            </Box>
          )}
          renderValue={
            // renderTags (v5) only ran in multiple mode. renderValue also
            // renders a single selection, so it is passed for groups only -
            // a one-to-one recipient keeps showing as plain input text.
            isGroup
              ? (value, getItemProps) =>
                  (value as UserDirectoryItem[]).map((option, index) => (
                    <Chip
                      {...getItemProps({ index })}
                      key={option.id}
                      label={option.fullName}
                      size="small"
                      avatar={
                        <Avatar sx={{ width: 20, height: 20, fontSize: 12, bgcolor: brandColors.sectionTitle }}>
                          {option.fullName.charAt(0).toUpperCase()}
                        </Avatar>
                      }
                    />
                  ))
              : undefined
          }
        />

        <TextField
          label={t("dialog.initialMessage")}
          value={initialMessage}
          onChange={(e) => setInitialMessage(e.target.value)}
          placeholder={t("dialog.initialPlaceholder")}
          fullWidth
          multiline
          minRows={3}
          maxRows={6}
          required
          disabled={submitting}
        />
      </DialogContent>
      <DialogActions sx={{ px: 3, py: 2 }}>
        <Button onClick={onClose} disabled={submitting} color="inherit">
          {t("dialog.cancel")}
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          color="primary"
          disabled={submitting || selectedUsers.length === 0 || !initialMessage.trim()}
          startIcon={submitting ? <CircularProgress size={16} color="inherit" /> : <AddCommentIcon />}
        >
          {submitting ? t("dialog.sending") : t("dialog.send")}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
