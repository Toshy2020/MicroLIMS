import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";
import { useAuth } from "../contexts/AuthContext";
import { clientSettingsService } from "../services/clientSettingsService";
import { draftStore, type Draft } from "../services/drafts/draftStore";

// Used when the server cannot be asked; the server's own default is the same.
const DEFAULT_RETENTION_HOURS = 24;

export interface DraftOverview {
  // The signed-in user's own drafts, newest first.
  drafts: Draft<unknown>[];
  // How many drafts other users left on this tablet. A number only.
  otherUsersCount: number;
  discard(id: string): Promise<void>;
}

// What the header shows about local drafts. Removes drafts older than the
// server's retention once per sign-in and tells the user how many went.
export function useDraftOverview(): DraftOverview {
  const { userId } = useAuth();
  const [drafts, setDrafts] = useState<Draft<unknown>[]>([]);
  const [otherUsersCount, setOtherUsersCount] = useState(0);

  const refresh = useCallback(async () => {
    if (userId == null) {
      setDrafts([]);
      setOtherUsersCount(0);
      return;
    }
    try {
      const [mine, others] = await Promise.all([draftStore.listForUser(userId), draftStore.countOtherUsers(userId)]);
      setDrafts(mine);
      setOtherUsersCount(others);
    } catch {
      // No usable storage on this device: there is nothing to list.
      setDrafts([]);
      setOtherUsersCount(0);
    }
  }, [userId]);

  useEffect(() => {
    if (userId == null) return;
    const controller = new AbortController();
    void (async () => {
      const settings = await clientSettingsService.get(controller.signal).catch(() => null);
      if (controller.signal.aborted) return;
      const hours = settings?.benchDraftRetentionHours ?? DEFAULT_RETENTION_HOURS;
      const removed = await draftStore.purgeOlderThan(hours, new Date()).catch(() => 0);
      if (controller.signal.aborted) return;
      if (removed > 0) {
        toast.info(
          `${removed} unsaved ${removed === 1 ? "entry" : "entries"} older than ${hours} ${hours === 1 ? "hour" : "hours"} ${removed === 1 ? "was" : "were"} removed from this tablet.`
        );
      }
      await refresh();
    })();
    return () => controller.abort();
  }, [userId, refresh]);

  useEffect(() => {
    void refresh();
    return draftStore.subscribe(() => void refresh());
  }, [refresh]);

  const discard = useCallback(async (id: string) => {
    await draftStore.remove(id);
  }, []);

  return { drafts, otherUsersCount, discard };
}
