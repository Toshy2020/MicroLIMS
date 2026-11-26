import { useCallback, useEffect, useState } from "react";
import {
  qualityService,
  type QualityChecklistAnswerValue,
  type QualityChecklistItemDto
} from "../../../services/qualityService";
import type { LookupStatus } from "../steps/lookups";

export interface ChecklistAnswerState {
  answer: QualityChecklistAnswerValue | "";
  comment: string;
}

const PAGE_SIZE = 100;

// The impact checklist of a change control: the active items of the section's template and the answers already
// recorded on the record (the latest answer of each item wins).
export function useChecklist(recordId: number, sectionId: number | null) {
  const [items, setItems] = useState<QualityChecklistItemDto[]>([]);
  const [answers, setAnswers] = useState<Record<string, ChecklistAnswerState>>({});
  const [status, setStatus] = useState<LookupStatus>("loading");

  useEffect(() => {
    const controller = new AbortController();
    setStatus("loading");
    Promise.all([
      qualityService.getChecklistTemplate("ChangeControl", sectionId, controller.signal),
      qualityService.listChecklistAnswers(recordId, 1, PAGE_SIZE, controller.signal)
    ])
      .then(([template, existing]) => {
        if (controller.signal.aborted) return;
        const active = (template?.items ?? []).filter((item) => item.isActive).sort((a, b) => a.order - b.order);
        const latest: Record<string, ChecklistAnswerState> = {};
        for (const answer of [...existing].sort((a, b) => a.id - b.id)) {
          latest[answer.itemKey] = { answer: answer.answer, comment: answer.comment ?? "" };
        }
        setItems(active);
        setAnswers(latest);
        setStatus("ready");
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus("failed");
      });
    return () => controller.abort();
  }, [recordId, sectionId]);

  const setAnswer = useCallback((key: string, patch: Partial<ChecklistAnswerState>) => {
    setAnswers((current) => ({ ...current, [key]: { answer: "", comment: "", ...current[key], ...patch } }));
  }, []);

  return { items, answers, status, setAnswer };
}
