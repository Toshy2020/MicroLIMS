import { Box, Paper, Typography, Checkbox, FormControlLabel, Chip, Tooltip, Stack } from "@mui/material";
import { PermissionRecord } from "../services/RoleService";

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
}

export function PermissionMatrix({ permissions, checkedCodes, onToggle }: PermissionMatrixProps) {
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
            {groups.get(mod)!.map((p) => (
              <Box key={p.code} sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.5, py: 0.25 }}>
                <FormControlLabel
                  sx={{ flex: 1, m: 0 }}
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
                <Tooltip
                  title={
                    p.isEnforced
                      ? "This permission alone controls access - the endpoints it covers check it directly."
                      : "Access on screens covered by this permission still follows the role's underlying base type, not this checkbox, until that screen is migrated to the new permission system."
                  }
                >
                  <Chip
                    label={p.isEnforced ? "Enforced" : "Legacy-only"}
                    size="small"
                    sx={{
                      flexShrink: 0,
                      fontWeight: 700,
                      fontSize: 11,
                      bgcolor: p.isEnforced ? "success.main" : "warning.main",
                      color: p.isEnforced ? "success.contrastText" : "warning.contrastText"
                    }}
                  />
                </Tooltip>
              </Box>
            ))}
          </Stack>
        </Paper>
      ))}
    </Stack>
  );
}
