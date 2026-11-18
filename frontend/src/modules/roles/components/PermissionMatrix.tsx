import { Box, Paper, Typography, Checkbox, FormControlLabel, Tooltip, Stack } from "@mui/material";
import { PermissionRecord, SodRules } from "../services/RoleService";
import { MetaBadge, ToneBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";

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
  // Codes disallowed for a Quality Assurance role; shown disabled and
  // unchecked with the reason exposed to assistive tech.
  qaForbiddenCodes?: ReadonlySet<string>;
}

// The checked code that `code` may not be combined with, if any.
function conflictingCode(code: string, checkedCodes: Set<string>, rules: SodRules | null | undefined): string | undefined {
  if (!rules) return undefined;
  const opposite = rules.administrationCodes.includes(code) ? rules.gxpExecutionAndApprovalCodes
    : rules.gxpExecutionAndApprovalCodes.includes(code) ? rules.administrationCodes
    : [];
  return opposite.find((c) => checkedCodes.has(c));
}

export function PermissionMatrix({ permissions, checkedCodes, onToggle, sodRules, qaForbiddenCodes }: PermissionMatrixProps) {
  const { t } = useTranslation("roles");
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
              const isQaForbidden = Boolean(qaForbiddenCodes?.has(p.code));
              // A checked box stays enabled so a role that already conflicts can be fixed.
              const blockedBy = isQaForbidden || checkedCodes.has(p.code)
                ? undefined
                : conflictingCode(p.code, checkedCodes, sodRules);
              const reasonId = isQaForbidden ? `qa-forbidden-${p.code.replace(/[^a-zA-Z0-9_-]/g, "-")}` : undefined;
              return (
              <Box key={p.code} sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1.5, py: 0.25 }}>
                <Tooltip title={blockedBy ? t("matrix.cannotCombine", { code: blockedBy }) : ""}>
                  {/* Span wrapper: a disabled control does not fire the hover Tooltip listens for. */}
                  <Box component="span" sx={{ flex: 1 }}>
                    <FormControlLabel
                      sx={{ m: 0, width: "100%" }}
                      disabled={isQaForbidden || !!blockedBy}
                      control={
                        <Checkbox
                          size="small"
                          checked={!isQaForbidden && checkedCodes.has(p.code)}
                          onChange={(e) => {
                            if (isQaForbidden) return;
                            onToggle(p.code, e.target.checked);
                          }}
                          slotProps={reasonId ? { input: { "aria-describedby": reasonId } } : undefined}
                        />
                      }
                      label={
                        <Box>
                          <Typography sx={{ fontSize: 13, fontWeight: 600 }}><Ltr>{p.code}</Ltr></Typography>
                          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{p.description}</Typography>
                        </Box>
                      }
                    />
                    {isQaForbidden && (
                      <Typography id={reasonId} sx={{ fontSize: 12, color: "text.secondary", pl: 3.5 }}>
                        {t("matrix.qaForbidden")}
                      </Typography>
                    )}
                  </Box>
                </Tooltip>
                <Tooltip
                  title={
                    p.isEnforced
                      ? t("matrix.enforcedTip")
                      : t("matrix.legacyTip")
                  }
                >
                  {/* Span wrapper: Tooltip needs a child that holds a ref. */}
                  <Box component="span" sx={{ flexShrink: 0 }}>
                    {p.isEnforced ? <MetaBadge label={t("matrix.enforced")} /> : <ToneBadge tone="action" label={t("matrix.legacyOnly")} />}
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
