import { useState } from "react";
import { TextField, Button, Stack } from "@mui/material";
import { FloatingDialog } from "../../components/FloatingDialog";
import { ReviewService } from "./services/ReviewService";

export function ReviewDialog({ open, testOrderId, onClose }: { open: boolean; testOrderId: number | null; onClose: () => void }) {
  const [comment, setComment] = useState("");

  const submit = async () => {
    if (testOrderId) await ReviewService.submitReview(testOrderId, comment);
    onClose();
  };

  return (
    <FloatingDialog open={open} title="Review" onClose={onClose} actions={<Button variant="contained" onClick={submit}>Submit Review</Button>}>
      <Stack spacing={2}>
        <TextField label="Comment" multiline rows={3} value={comment} onChange={(e) => setComment(e.target.value)} />
      </Stack>
    </FloatingDialog>
  );
}
