import { apiClient } from "../../../services/apiClient";

export interface DatabaseTableSize {
  schema: string;
  name: string;
  totalSizeBytes: number;
  totalSizePretty: string;
  // Null until the table has been analyzed.
  estimatedRows: number | null;
}

export interface BackupRun {
  id: number;
  startedAtUtc: string;
  finishedAtUtc: string;
  succeeded: boolean;
  fileName: string;
  sizeBytes: number | null;
  error: string | null;
  host: string;
}

export interface DatabaseOverview {
  databaseSizeBytes: number;
  databaseSizePretty: string;
  topTables: DatabaseTableSize[];
  latestBackup: BackupRun | null;
}

export const DatabaseOverviewService = {
  async get(): Promise<DatabaseOverview> {
    return (await apiClient.get("/admin/database/overview")).data.data;
  }
};
