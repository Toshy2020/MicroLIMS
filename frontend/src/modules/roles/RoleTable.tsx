import { useEffect, useState } from "react";
import { DataTable, Column } from "../../components/DataTable";
import { RoleService } from "./services/RoleService";

interface RoleRow { id: number; name: string; }

export function RoleTable() {
  const [rows, setRows] = useState<RoleRow[]>([]);
  useEffect(() => { RoleService.getAll().then(setRows); }, []);
  const columns: Column<RoleRow>[] = [{ key: "name", label: "Role" }];
  return <DataTable columns={columns} rows={rows} getRowId={(r) => r.id} />;
}
