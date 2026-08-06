import { useApi } from "../../../hooks/useApi";
import { MyTask } from "../types/dashboard";

// Backend rejects this for non-Analyst roles (403) - only call it from
// Analyst-gated panels.
export function useMyTasks() {
  return useApi<MyTask[]>("/dashboard/my-tasks");
}
