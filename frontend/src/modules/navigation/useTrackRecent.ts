import { useEffect, useRef } from "react";
import { notifyRecentTouched } from "./useRecentRecords";
import { recentRecordsService, type RecentRecordType } from "./services/recentRecordsService";

// Records that the user opened a record. Pass the id only once the record has
// loaded successfully (null/undefined until then): a failed load is not an
// opening. Posts once per mount for a given record. Failures are silent: this is
// a convenience list, and the server stores nothing for records the user may not read.
export function useTrackRecent(type: RecentRecordType, id: number | null | undefined): void {
  const posted = useRef<string | null>(null);
  useEffect(() => {
    if (id == null || !Number.isFinite(id) || id <= 0) return;
    const key = `${type}:${id}`;
    if (posted.current === key) return;
    posted.current = key;
    recentRecordsService
      .touch(type, id)
      .then(notifyRecentTouched)
      .catch(() => {
        // Not worth interrupting laboratory work; the record simply is not listed.
      });
  }, [type, id]);
}
