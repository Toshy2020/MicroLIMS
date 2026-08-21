import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Chip,
  CircularProgress,
  Button,
  useTheme
} from "@mui/material";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import { useNavigate } from "react-router-dom";
import { AuditSearchService } from "../services/AuditSearchService";
import type { AuditTraceabilityResult } from "../types/auditTypes";
import { resolveTraceabilityRoute } from "../../../routes/routes";

interface Props {
  auditLogId?: number;
  entityName?: string;
  entityId?: string;
  onOpenEntityHistory?: (name: string, id: number | string) => void;
}

export function AuditTraceabilityChain({
  auditLogId,
  entityName,
  entityId,
  onOpenEntityHistory
}: Props) {
  const navigate = useNavigate();
  const theme = useTheme();
  const [result, setResult] = useState<AuditTraceabilityResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    setError(null);

    const promise = auditLogId
      ? AuditSearchService.getTraceability(auditLogId)
      : entityName && entityId
      ? AuditSearchService.getTraceabilityForEntity(entityName, entityId)
      : Promise.resolve(null);

    promise
      .then((res) => {
        setResult(res);
      })
      .catch(() => {
        setError("Could not load record traceability chain.");
      })
      .finally(() => {
        setLoading(false);
      });
  }, [auditLogId, entityName, entityId]);

  if (loading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
        <CircularProgress size={24} />
      </Box>
    );
  }

  if (error) {
    return <Typography sx={{ fontSize: 12, color: "error.main" }}>{error}</Typography>;
  }

  if (!result || !result.nodes || result.nodes.length === 0) {
    return (
      <Typography sx={{ fontSize: 12, color: "text.secondary", fontStyle: "italic" }}>
        No relationship chain found for this record.
      </Typography>
    );
  }

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5, py: 1 }}>
      <Typography sx={{ fontSize: 11, fontWeight: 700, color: "text.secondary", textTransform: "uppercase" }}>
        Traceability Chain · {result.primaryCategory} ({result.rootIdentifier})
      </Typography>

      {result.nodes.map((node, idx) => {
        const isLast = idx === result.nodes.length - 1;

        return (
          <Box key={`${node.nodeType}-${node.identifier}-${idx}`}>
            <Box
              sx={{
                p: 1.5,
                borderRadius: 1.5,
                border: "1px solid",
                borderColor: "divider",
                bgcolor: "background.paper",
                transition: "all 0.15s ease",
                "&:hover": { borderColor: "primary.main", bgcolor: theme.custom.status.purple.bg }
              }}
            >
              <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 0.5 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                  <Chip
                    label={node.nodeType.toUpperCase()}
                    size="small"
                    sx={{
                      fontSize: 9,
                      fontWeight: 700,
                      height: 18,
                      bgcolor: theme.custom.status.purple.bg,
                      color: theme.custom.status.purple.text
                    }}
                  />
                  <Typography sx={{ fontSize: 13, fontWeight: 700, fontFamily: "monospace", color: "text.primary" }}>
                    {node.identifier}
                  </Typography>
                </Box>
                {node.status && (
                  <Chip
                    label={node.status}
                    size="small"
                    variant="outlined"
                    sx={{ fontSize: 10, height: 18 }}
                  />
                )}
              </Box>

              <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary", mb: 0.25 }}>
                {node.title}
              </Typography>

              {node.description && (
                <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                  {node.description}
                </Typography>
              )}

              {/* Action buttons */}
              <Box sx={{ display: "flex", justifyContent: "flex-end", gap: 1, mt: 1 }}>
                {onOpenEntityHistory && node.entityId && (
                  <Button
                    size="small"
                    variant="text"
                    onClick={() => onOpenEntityHistory(node.nodeType, node.entityId!)}
                    sx={{ fontSize: 10, textTransform: "none", py: 0.2 }}
                  >
                    View Audit History
                  </Button>
                )}

                {(() => {
                  const targetRoute = resolveTraceabilityRoute(node.navigationTarget, node.nodeType);
                  if (!targetRoute) return null;

                  return (
                    <Button
                      size="small"
                      variant="outlined"
                      endIcon={<OpenInNewIcon sx={{ fontSize: 12 }} />}
                      onClick={() => navigate(targetRoute)}
                      sx={{ fontSize: 10, textTransform: "none", py: 0.2 }}
                    >
                      Open {node.nodeType}
                    </Button>
                  );
                })()}
              </Box>
            </Box>

            {!isLast && (
              <Box sx={{ display: "flex", justifyContent: "center", py: 0.5 }}>
                <ArrowDownwardIcon sx={{ fontSize: 16, color: "primary.main" }} />
              </Box>
            )}
          </Box>
        );
      })}
    </Box>
  );
}
