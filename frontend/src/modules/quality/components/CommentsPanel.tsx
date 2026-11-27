import { useState } from "react";
import { Alert, Box, Button, List, ListItem, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { ToneBadge } from "../../../components/StatusBadge";
import { getErrorMessage } from "../../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { qualityService, type QualityCommentDto } from "../../../services/qualityService";
import { PanelFrame } from "./PanelFrame";

const COMMENT_MAX = 2000;

interface CommentsPanelProps {
  recordId: number;
  recordVersion: number;
  // null when the comments could not be loaded (the page shows the partial-outage banner).
  comments: QualityCommentDto[] | null;
  currentUserId: number | null;
  // Quality.Raise: write a comment.
  canEdit: boolean;
  onChanged: () => Promise<void>;
}

export function CommentsPanel({ recordId, recordVersion, comments, currentUserId, canEdit, onChanged }: CommentsPanelProps) {
  const { t } = useTranslation("quality");
  const [text, setText] = useState("");
  const [posting, setPosting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const post = async () => {
    setPosting(true);
    setError(null);
    try {
      await qualityService.addComment(recordId, text.trim(), recordVersion);
      setText("");
      await onChanged();
    } catch (e: unknown) {
      setError(getErrorMessage(e, t("comments.postFailed")));
    } finally {
      setPosting(false);
    }
  };

  return (
    <PanelFrame title={t("comments.title")}>
      <Stack spacing={1.5}>
        {comments === null ? (
          <Typography variant="body2" color="text.secondary">
            {t("comments.unavailable")}
          </Typography>
        ) : comments.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("comments.empty")}
          </Typography>
        ) : (
          <List disablePadding>
            {comments.map((entry) => (
              <ListItem key={entry.id} divider sx={{ display: "block", px: 0 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                  <Typography variant="caption" color="text.secondary" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {t("comments.line", {
                      author: entry.authorUserId === currentUserId ? t("comments.you") : entry.authorName,
                      date: formatDateTimeWithTimeZone(entry.createdAtUtc)
                    })}
                  </Typography>
                  {entry.isAddendum && <ToneBadge tone="info" label={t("addendum.badge")} />}
                </Stack>
                <Typography variant="body2" sx={{ whiteSpace: "pre-wrap", wordBreak: "break-word" }}>
                  {entry.text}
                </Typography>
              </ListItem>
            ))}
          </List>
        )}

        {canEdit && (
          <Box>
            {error && (
              <Alert severity="error" role="alert" sx={{ mb: 1 }}>
                {error}
              </Alert>
            )}
            <TextField
              fullWidth
              multiline
              minRows={2}
              label={t("comments.label")}
              helperText={t("comments.helper", { count: text.length })}
              value={text}
              onChange={(e) => setText(e.target.value)}
              disabled={posting}
              slotProps={{ htmlInput: { maxLength: COMMENT_MAX } }}
            />
            <Button sx={{ mt: 1 }} variant="contained" disabled={!text.trim() || posting} onClick={() => void post()}>
              {posting ? t("comments.posting") : t("comments.post")}
            </Button>
          </Box>
        )}
      </Stack>
    </PanelFrame>
  );
}
