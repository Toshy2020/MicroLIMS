import { Box, Paper, Typography, Checkbox, FormControlLabel, Tooltip, Stack } from "@mui/material";
import { PermissionRecord, SodRules } from "../services/RoleService";
import { MetaBadge, ToneBadge } from "../../../components/StatusBadge";

// "TestWorkflow.Execute" -> module "TestWorkflow", tier "Execute". Module
// grouping per the spec: everything before the first ".".
function moduleOf(code: string): string {
  return code.split(".")[0];
}

// "TestWorkflow" -> "Test Workflow", "MasterData" -> "Master Data" -
// same PascalCase-to-spaced convention used elsewhere in the app
// (SampleReportPage.humanize) for turning enum/identifier names into
// readable labels.
function humanizeModule(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
}

const MODULE_ORDER = [
  "Users", "Roles", "Audit", "Reporting", "Samples", "Signatures",
  "TestWorkflow", "Cryovials", "Materials", "Equipment", "Items", "MasterData"
];

interface PermissionMatrixProps {
  permissions: PermissionRecord[];
  checkedCodes: Set<string>;
  onToggle: (code: string, checked: boolean) => void;
  // When given, a box that would put administration and GxP codes in one
  // role is disabled. The server enforces the same rule on save.
  sodRules?: SodRules | null;
}

// The checked code that `code` may not be combined with, if any.
function conflictingCode(code: string, checkedCodes: Set<string>, rules: SodRules | null | undefined): string | undefined {
  if (!rules) return undefined;
  const opposite = rules.administrationCodes.includes(code) ? rules.gxpExecutionAndApprovalCodes
    : rules.gxpExecutionAndApprovalCodes.includes(code) ? rules.administrationCodes
    : [];
  return opposite.find((c) => checkedCodes.has(c));
}

export function PermissionMatrix({ permissions, checkedCodes, onToggle, sodRules }: PermissionMatrixProps) {
  const groups = new Map<string, PermissionRecord[]>();
  for (const p of permissions) {
    const mod = moduleOf(p.code);
    if (!groups.has(mod)) groups.set(mod, []);
    groups.get(mod)!.push(p);
  }

  const orderedModules = [
    ...MODULE_ORDER.filter((m) => groups.has(m)),
    ...Array.from(groups.keys()).filter((m) => !MODULE_ORDER.includes(m))
  ];

  return (
    <Stack spacing={2}>
      {orderedModules.map((mod) => (
        <Paper key={mod} variant="outlined" sx={{ p: 2 }}>
          <Typography sx={{ fontSize: 13, fontWeight: 700, textTransform: "uppercase", letterSpacing: 0.4, color: "text.secondary", mb: 1 }}>
            {humanizeModule(mod)}
          </Typography>
          <Stack spacing={0.5}>
            {groups.get(mod)!.map((p) => {
              // A checked box stays enabled so a role that already conflicts can be fixed.
              const blockedBy = checkedCodes.has(p.code) ? undefined : conflictingCode(p.code, checkedCodes, sodRules);
              return (
              <Box key={p.code} sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.5, py: 0.25 }}>
                <Tooltip title={blockedBy ? `Cannot be combined with ${blockedBy}: a role may not hold both system administration and GxP execution or approval.` : ""}>
                  {/* Span wrapper: a disabled control does not fire the hover Tooltip listens for. */}
                  <Box component="span" sx={{ flex: 1 }}>
                    <FormControlLabel
                      sx={{ m: 0, width: "100%" }}
                      disabled={!!blockedBy}
                      control={
                        <Checkbox
                          size="small"
                          checked={checkedCodes.has(p.code)}
                          onChange={(e) => onToggle(p.code, e.target.checked)}
                        />
                      }
                      label={
                        <Box>
                          <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{p.code}</Typography>
                          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{p.description}</Typography>
                        </Box>
                      }
                    />
                  </Box>
                </Tooltip>
                <Tooltip
                  title={
                    p.isEnforced
                      ? "This permission alone controls access - the endpoints it covers check it directly."
                      : "Access on screens covered by this permission still follows the role's underlying base type, not this checkbox, until that screen is migrated to the new permission system."
                  }
                >
                  {/* Span wrapper: Tooltip needs a child that holds a ref. */}
                  <Box component="span" sx={{ flexShrink: 0 }}>
                    {p.isEnforced ? <MetaBadge label="Enforced" /> : <ToneBadge tone="action" label="Legacy-only" />}
                  </Box>
                </Tooltip>
              </Box>
              );
            })}
          </Stack>
        </Paper>
      ))}
    </Stack>
  );
}
