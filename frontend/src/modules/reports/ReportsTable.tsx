import { Stack, Button } from "@mui/material";
import { apiClient } from "../../services/apiClient";

export function ReportsTable() {
  const download = async (type: string) => {
    const res = await apiClient.get(`/reports/${type}`, { responseType: "blob" });
    const url = window.URL.createObjectURL(res.data);
    const a = document.createElement("a");
    a.href = url;
    a.download = `${type}.pdf`;
    a.click();
  };

  return (
    <Stack direction="row" spacing={2}>
      <Button onClick={() => download("product")}>Product</Button>
      <Button onClick={() => download("water")}>Water</Button>
      <Button onClick={() => download("em")}>EM</Button>
    </Stack>
  );
}
