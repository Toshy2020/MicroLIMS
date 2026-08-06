import { useState } from "react";
import { SignatureDialog } from "../../components/SignatureDialog";
import { ReviewService } from "./services/ReviewService";

export function ReviewDialog({ open, testOrderId, onClose }: { open: boolean; testOrderId: number | null; onClose: () => void }) {
  const [comment, setComment] = useState("");

  const confirm = async (password: string) => {
    if (!testOrderId) return;
    await ReviewService.submitReview(testOrderId, comment, password);
    setComment("");
    onClose();
  };

  return (
    <SignatureDialog
      open={open}
      meaningStatement="I am reviewing this result."
      showComment
      comment={comment}
      onCommentChange={setComment}
      onCancel={onClose}
      onConfirm={confirm}
    />
  );
}
