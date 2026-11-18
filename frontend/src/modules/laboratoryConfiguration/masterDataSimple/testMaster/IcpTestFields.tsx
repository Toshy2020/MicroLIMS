import {
  Box,
  Button,
  FormHelperText,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "react-i18next";
import type { IcpElementRow, IcpTestForm, IcpTestMode, IcpSampleForm } from "./icpTestConfig";

export interface IcpTestFieldsProps {
  form: IcpTestForm;
  onChange: (form: IcpTestForm) => void;
}

// The ICP part of the Test Master dialog: assay (% of target) or impurity (ppm against the item's limit),
// the sample basis for the assay formula, and the elements the test reports.
export function IcpTestFields({ form, onChange }: IcpTestFieldsProps) {
  const { t } = useTranslation("masterData");
  const set = (patch: Partial<IcpTestForm>) => onChange({ ...form, ...patch });
  const assay = form.mode === "Assay";
  const liquid = form.sampleForm === "Liquid";
  const updateElement = (index: number, patch: Partial<IcpElementRow>) =>
    set({ elements: form.elements.map((e, i) => (i === index ? { ...e, ...patch } : e)) });

  return (
    <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
      <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>{t("testMaster.icp.sectionTitle")}</Typography>
      <Stack spacing={2}>
        <Box>
          <ToggleButtonGroup
            size="small"
            exclusive
            value={form.mode}
            onChange={(_, v: IcpTestMode | null) => v && set({ mode: v })}
            aria-label={t("testMaster.icp.testTypeAria")}
          >
            <ToggleButton value="Assay" sx={{ textTransform: "none", px: 2.5 }}>{t("testMaster.icp.modes.assay")}</ToggleButton>
            <ToggleButton value="Impurity" sx={{ textTransform: "none", px: 2.5 }}>{t("testMaster.icp.modes.impurity")}</ToggleButton>
          </ToggleButtonGroup>
          <FormHelperText>
            {assay
              ? t("testMaster.icp.modeHelp.assay")
              : t("testMaster.icp.modeHelp.impurity")}
          </FormHelperText>
        </Box>

        {assay && (
          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: { sm: "flex-start" } }}>
            <Box>
              <ToggleButtonGroup
                size="small"
                exclusive
                value={form.sampleForm}
                onChange={(_, v: IcpSampleForm | null) => v && set({ sampleForm: v })}
                aria-label={t("testMaster.icp.sampleFormAria")}
              >
                <ToggleButton value="Solid" sx={{ textTransform: "none", px: 2.5 }}>{t("testMaster.icp.sampleForms.solid")}</ToggleButton>
                <ToggleButton value="Liquid" sx={{ textTransform: "none", px: 2.5 }}>{t("testMaster.icp.sampleForms.liquid")}</ToggleButton>
              </ToggleButtonGroup>
            </Box>
            <TextField
              size="small"
              type="number"
              required
              label={liquid ? t("testMaster.icp.theoreticalVolume") : t("testMaster.icp.theoreticalWeight")}
              value={form.theoreticalAmount}
              onChange={(e) => set({ theoreticalAmount: e.target.value })}
              helperText={liquid ? t("testMaster.icp.thVol") : t("testMaster.icp.thWt")}
              slotProps={{ htmlInput: { min: 0, step: "any" } }}
              sx={{ width: 220 }}
            />
            {liquid && (
              <TextField
                size="small"
                type="number"
                required
                label={t("testMaster.icp.specificGravity")}
                value={form.specificGravity}
                onChange={(e) => set({ specificGravity: e.target.value })}
                helperText={t("testMaster.icp.spGr")}
                slotProps={{ htmlInput: { min: 0, step: "any" } }}
                sx={{ width: 180 }}
              />
            )}
          </Stack>
        )}

        <Box>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>{t("testMaster.icp.elementsHeading")}</Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell sx={{ width: 120 }}>{t("testMaster.icp.columns.symbol")}</TableCell>
                {assay && <TableCell>{t("testMaster.icp.columns.targetPpm")}</TableCell>}
                <TableCell>{t("testMaster.icp.columns.cleaningLimitPpm")}</TableCell>
                <TableCell sx={{ width: 56 }} />
              </TableRow>
            </TableHead>
            <TableBody>
              {form.elements.map((el, index) => (
                <TableRow key={el.id ?? `new-${index}`}>
                  <TableCell>
                    <TextField
                      size="small"
                      value={el.symbol}
                      onChange={(e) => updateElement(index, { symbol: e.target.value })}
                      placeholder={t("testMaster.icp.placeholders.symbol")}
                      slotProps={{ htmlInput: { maxLength: 3, "aria-label": t("testMaster.icp.aria.symbol", { index: index + 1 }) } }}
                      sx={{ width: 100 }}
                    />
                  </TableCell>
                  {assay && (
                    <TableCell>
                      <TextField
                        size="small"
                        type="number"
                        value={el.targetPpm}
                        onChange={(e) => updateElement(index, { targetPpm: e.target.value })}
                        placeholder={t("testMaster.icp.placeholders.targetPpm")}
                        slotProps={{ htmlInput: { min: 0, step: "any", "aria-label": t("testMaster.icp.aria.targetPpm", { index: index + 1 }) } }}
                        sx={{ width: 180 }}
                      />
                    </TableCell>
                  )}
                  <TableCell>
                    <TextField
                      size="small"
                      type="number"
                      value={el.cleaningLimitPpm}
                      onChange={(e) => updateElement(index, { cleaningLimitPpm: e.target.value })}
                      placeholder={t("testMaster.icp.placeholders.cleaningLimitPpm")}
                      slotProps={{ htmlInput: { min: 0, step: "any", "aria-label": t("testMaster.icp.aria.cleaningLimitPpm", { index: index + 1 }) } }}
                      sx={{ width: 180 }}
                    />
                  </TableCell>
                  <TableCell>
                    <IconButton
                      size="small"
                      aria-label={t("testMaster.icp.aria.removeElement", { index: index + 1 })}
                      disabled={form.elements.length === 1}
                      onClick={() => set({ elements: form.elements.filter((_, i) => i !== index) })}
                    >
                      <DeleteOutlinedIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <Button
            size="small"
            startIcon={<AddIcon />}
            onClick={() => set({ elements: [...form.elements, { symbol: "", targetPpm: "", cleaningLimitPpm: "" }] })}
            sx={{ mt: 1, textTransform: "none" }}
          >
            {t("testMaster.icp.addElement")}
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}
