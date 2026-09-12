import { Paper, Typography, useTheme } from "@mui/material";
import { PieChart, Pie, Cell, Tooltip, Legend, ResponsiveContainer } from "recharts";
import { DistributionSlice } from "../types/dashboard";
import { statusTone } from "../../../theme/statusTokens";

export function TestOrderStatusDonut({ statusDist }: { statusDist: DistributionSlice[] | null }) {
  const theme = useTheme();

  // These slices are statuses - Approved / Pending / Rejected - which is the
  // one kind of data that has a reserved palette. They used to be painted
  // from the categorical chart palette by index, so the three of them came
  // out as three shades of the same purple and, worse, Rejected landed on
  // whichever shade its position happened to give it - usually the palest,
  // leaving the slice that matters most as the least prominent one.
  //
  // statusTone() is the map the theme already keeps for exactly this (and the
  // one statusTokens.ts asks callers to extend rather than hardcoding), so
  // Approved reads green, Rejected red, Pending neutral, in both modes.
  const fillFor = (status?: string) => theme.custom.status[statusTone(status ?? "")].text;

  return (
    <Paper sx={{ p: 2.5, height: 320 }}>
      <Typography sx={{ fontWeight: 600, mb: 1 }}>Test Order Status</Typography>
      {statusDist && statusDist.length > 0 ? (
        <ResponsiveContainer width="100%" height="85%">
          <PieChart>
            <Pie data={statusDist} dataKey="count" nameKey="status" innerRadius={55} outerRadius={85} paddingAngle={2}>
              {statusDist.map((slice) => (
                <Cell
                  key={slice.status}
                  fill={fillFor(slice.status)}
                  // A 2px surface-coloured ring keeps adjacent slices from
                  // bleeding into one another.
                  stroke={theme.palette.background.paper}
                  strokeWidth={2}
                />
              ))}
            </Pie>
            <Tooltip
              formatter={(value: unknown, name: unknown) => [`${value}`, String(name)]}
              contentStyle={{ background: theme.palette.background.paper, border: `1px solid ${theme.palette.divider}`, color: theme.palette.text.primary }}
            />
            <Legend />
          </PieChart>
        </ResponsiveContainer>
      ) : (
        <Typography
          sx={{
            color: "text.secondary",
            mt: 4
          }}>No data yet.</Typography>
      )}
    </Paper>
  );
}
