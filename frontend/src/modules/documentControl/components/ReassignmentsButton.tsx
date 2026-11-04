import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Badge, Button } from "@mui/material";
import SwapHorizOutlinedIcon from "@mui/icons-material/SwapHorizOutlined";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { reassignmentService } from "../services/reassignmentService";

// "Needs reassignment" with the number of items waiting, for the people who can act on them. The server decides
// who that is (Documents.ConfigManage, or Documents.Reassign within their own department); this only hides the
// button from everyone else and shows nothing if the count cannot be loaded.
export function ReassignmentsButton() {
  const navigate = useNavigate();
  const mayReassign = useHasPermission([PERMISSIONS.DOCUMENTS_CONFIG_MANAGE, PERMISSIONS.DOCUMENTS_REASSIGN]);
  const [count, setCount] = useState<{ total: number; overdue: number } | null>(null);

  useEffect(() => {
    if (!mayReassign) return;
    let live = true;
    reassignmentService.getCount().then((c) => { if (live) setCount(c); }).catch(() => {});
    return () => { live = false; };
  }, [mayReassign]);

  if (!mayReassign) return null;
  return (
    <Badge badgeContent={count?.total ?? 0} color={count && count.overdue > 0 ? "error" : "warning"} showZero={false} overlap="rectangular">
      <Button variant="outlined" startIcon={<SwapHorizOutlinedIcon />} onClick={() => navigate("/document-control/reassignments")}>
        Needs Reassignment
      </Button>
    </Badge>
  );
}


