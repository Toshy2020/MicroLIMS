import { Paper, Typography, useTheme } from "@mui/material";
import { PieChart, Pie, Cell, Tooltip, Legend, ResponsiveContainer } from "recharts";
import { DistributionSlice } from "../types/dashboard";

export function TestOrderStatusDonut({ statusDist }: { statusDist: DistributionSlice[] | null }) {
  const theme = useTheme();
  const chartPalette = theme.custom.chartPalette;
  return (
    <Paper sx={{ p: 2.5, height: 320 }}>
      <Typography sx={{ fontWeight: 600, mb: 1 }}>Test Order Status</Typography>
      {statusDist && statusDist.length > 0 ? (
        <ResponsiveContainer width="100%" height="85%">
          <PieChart>
            <Pie data={statusDist} dataKey="count" nameKey="status" innerRadius={55} outerRadius={85} paddingAngle={2}>
              {statusDist.map((_, i) => <Cell key={i} fill={chartPalette[i % chartPalette.length]} />)}
            </Pie>
            <Tooltip
              formatter={(value: number, name: string) => [`${value}`, name]}
              contentStyle={{ background: theme.palette.background.paper, border: `1px solid ${theme.palette.divider}`, color: theme.palette.text.primary }}
            />
            <Legend />
          </PieChart>
        </ResponsiveContainer>
      ) : (
        <Typography color="text.secondary" sx={{ mt: 4 }}>No data yet.</Typography>
      )}
    </Paper>
  );
}
