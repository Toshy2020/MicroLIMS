import { useTranslation } from "../../../i18n";
import { Paper, Box, Typography, Select, MenuItem, useTheme } from "@mui/material";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from "recharts";
import { MonthlyTrendPoint } from "../types/dashboard";

export function SamplesTrendChart({ trend, months, onMonthsChange }: {
  trend: MonthlyTrendPoint[] | null; months: number; onMonthsChange: (months: number) => void;
}) {
  const { t } = useTranslation("dashboard");
  const theme = useTheme();
  const chartPalette = theme.custom.chartPalette;

  // Two unrelated measures, so two different hues from the categorical
  // palette. They were previously adjacent steps of one purple ramp, which
  // measured dE 14.3 apart under normal vision - below the 15 floor - and
  // read on screen as "purple" and "almost black".
  const samplesColor = chartPalette[0];
  const testsColor = chartPalette[1];

  return (
    <Paper sx={{ p: 2.5, height: 320 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography sx={{ fontWeight: 600 }}>{t("trend.title")}</Typography>
        <Select
          size="small"
          value={months}
          onChange={(e) => onMonthsChange(Number(e.target.value))}
          inputProps={{ "aria-label": t("trend.period") }}
        >
          <MenuItem value={3}>{t("trend.last3")}</MenuItem>
          <MenuItem value={6}>{t("trend.last6")}</MenuItem>
          <MenuItem value={12}>{t("trend.last12")}</MenuItem>
        </Select>
      </Box>
      {trend && (
        <ResponsiveContainer width="100%" height="85%">
          <BarChart data={trend}>
            {/* Solid, not dashed: a dashed rule reads as a threshold line. */}
            <CartesianGrid vertical={false} stroke={theme.palette.divider} />
            <XAxis dataKey="month" fontSize={12} tick={{ fill: theme.palette.text.secondary }} />
            <YAxis fontSize={12} tick={{ fill: theme.palette.text.secondary }} />
            <Tooltip
              cursor={{ fill: theme.palette.action.hover }}
              contentStyle={{ background: theme.palette.background.paper, border: `1px solid ${theme.palette.divider}`, color: theme.palette.text.primary }}
            />
            <Legend />
            <Bar dataKey="samplesLodged" name={t("trend.samples")} fill={samplesColor} radius={[4, 4, 0, 0]} />
            <Bar dataKey="testsLodged" name={t("trend.testRequests")} fill={testsColor} radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      )}
    </Paper>
  );
}
