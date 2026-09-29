import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer,
  Paper,
  CircularProgress,
  Link,
  useTheme
} from "@mui/material";
import { tableHeadSx } from "../../../../theme";
import { SolutionPreparationService } from "../../../solutionPreparation/services/SolutionPreparationService";
import { PreparationStatusBadge } from "../../../solutionPreparation/components/PreparationStatusBadge";
import { formatLabDate } from "../../../../utils/formatDate";
import type { SolutionPreparationListItem } from "../../../solutionPreparation/types";

interface Props {
  materialId: number;
  onNavigate?: () => void;
}

export function LotPreparationsList({ materialId, onNavigate }: Props) {
  const navigate = useNavigate();
  const theme = useTheme();
  const [preparations, setPreparations] = useState<SolutionPreparationListItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    setLoading(true);
    SolutionPreparationService.getByLot(materialId)
      .then((data) => {
        if (!active) return;
        setPreparations(data);
      })
      .catch(() => {
        if (!active) return;
        setPreparations([]);
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [materialId]);

  const handleClick = (id: number) => {
    if (onNavigate) onNavigate();
    navigate(`/preparation/${id}`);
  };

  return (
    <Box sx={{ mb: 2 }}>
      <Typography sx={{ fontWeight: 600, fontSize: 13, mb: 1 }}>
        Consumed by Preparations
      </Typography>

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 1 }}>
          <CircularProgress size={14} />
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Checking preparations...
          </Typography>
        </Box>
      ) : preparations.length === 0 ? (
        <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic" }}>
          No preparations have consumed this lot.
        </Typography>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ maxHeight: 180 }}>
          <Table size="small" stickyHeader>
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 11, py: 0.5, fontWeight: 700 }}>Code</TableCell>
                <TableCell sx={{ fontSize: 11, py: 0.5, fontWeight: 700 }}>Solution</TableCell>
                <TableCell sx={{ fontSize: 11, py: 0.5, fontWeight: 700 }}>Prepared</TableCell>
                <TableCell sx={{ fontSize: 11, py: 0.5, fontWeight: 700 }}>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {preparations.map((p) => (
                <TableRow key={p.id} hover>
                  <TableCell sx={{ fontSize: 11, py: 0.5 }}>
                    <Link
                      component="button"
                      variant="body2"
                      onClick={() => handleClick(p.id)}
                      sx={{
                        fontFamily: "monospace",
                        fontWeight: 700,
                        fontSize: 11,
                        textAlign: "left",
                        cursor: "pointer"
                      }}
                    >
                      {p.code || `#${p.id} (In Progress)`}
                    </Link>
                  </TableCell>
                  <TableCell sx={{ fontSize: 11, py: 0.5 }}>
                    {p.solutionMasterName}
                  </TableCell>
                  <TableCell sx={{ fontSize: 11, py: 0.5 }}>
                    {p.preparedAt ? formatLabDate(p.preparedAt) : "—"}
                  </TableCell>
                  <TableCell sx={{ fontSize: 11, py: 0.5 }}>
                    <PreparationStatusBadge status={p.effectiveStatus} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
