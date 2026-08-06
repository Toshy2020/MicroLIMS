import { Paper, Box, Typography, Select, MenuItem } from "@mui/material";
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from "recharts";
import { MonthlyTrendPoint } from "../types/dashboard";
import { chartPalette } from "../../../theme";

export function SamplesTrendChart({ trend, months, onMonthsChange }: {
  trend: MonthlyTrendPoint[] | null; months: number; onMonthsChange: (months: number) => void;
}) {
  return (
    <Paper sx={{ p: 2.5, height: 320 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography sx={{ fontWeight: 600 }}>Samples Received Trend</Typography>
        <Select size="small" value={months} onChange={(e) => onMonthsChange(Number(e.target.value))}>
          <MenuItem value={3}>Last 3 months</MenuItem>
          <MenuItem value={6}>Last 6 months</MenuItem>
          <MenuItem value={12}>Last 12 months</MenuItem>
        </Select>
      </Box>
      {trend && (
        <ResponsiveContainer width="100%" height="85%">
          <BarChart data={trend}>
            <CartesianGrid strokeDasharray="3 3" vertical={false} />
            <XAxis dataKey="month" fontSize={12} />
            <YAxis fontSize={12} />
            <Tooltip />
            <Legend />
            <Bar dataKey="samplesLodged" name="Samples" fill={chartPalette[1]} radius={[4, 4, 0, 0]} />
            <Bar dataKey="testsLodged" name="Test Requests" fill={chartPalette[0]} radius={[4, 4, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      )}
    </Paper>
  );
}
