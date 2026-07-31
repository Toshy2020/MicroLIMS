import { useEffect, useState } from "react";
import { DataTable, Column } from "../../../components/DataTable";
import { MediaService } from "./services/MediaService";

interface MediaRow { id: number; name: string; lotNumber: string; expiryDate: string; status: string; }

export function MediaTable() {
  const [rows, setRows] = useState<MediaRow[]>([]);
  useEffect(() => { MediaService.getAll().then(setRows); }, []);
  const columns: Column<MediaRow>[] = [
    { key: "name", label: "Media" },
    { key: "lotNumber", label: "Lot Number" },
    { key: "expiryDate", label: "Expiry" },
    { key: "status", label: "Status" }
  ];
  return <DataTable columns={columns} rows={rows} getRowId={(r) => r.id} />;
}
