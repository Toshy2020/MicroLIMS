import { useId } from "react";
import { Box, Button, Chip, Paper, Stack, Typography } from "@mui/material";
import { Link } from "react-router-dom";
import { Trans } from "react-i18next";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { MyWorkGroup as MyWorkGroupDto, MyWorkItem } from "../myWorkTypes";
import { routeForGroup } from "../targetRoutes";
import { MyWorkRow } from "./MyWorkRow";

export interface MyWorkGroupProps {
  group: MyWorkGroupDto;
  timeZoneId: string;
  onTake: (item: MyWorkItem) => void;
  onRelease: (item: MyWorkItem) => void;
  busyKey?: string | null;
}

export function MyWorkGroup({ group, timeZoneId, onTake, onRelease, busyKey }: MyWorkGroupProps) {
  const { t } = useTranslation("myWork");
  const headingId = useId();

  if (group.items.length === 0) return null;

  const firstLab = group.items[0]?.lab;
  const showAllRoute = routeForGroup(group.key, firstLab);

  return (
    <Paper component="section" aria-labelledby={headingId} sx={{ p: 2 }}>
      <Box
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          gap: 1.5,
          mb: 1.5,
          flexWrap: "wrap"
        }}
      >
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <Typography id={headingId} component="h2" variant="h6" sx={{ fontSize: 16, fontWeight: 700 }}>
            {t(`group.titles.${group.key}`, { defaultValue: group.title })}
          </Typography>
          <Chip
            size="small"
            label={group.count}
            sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}
          />
        </Stack>

        {group.hasMore && (
          <Button
            component={Link}
            to={showAllRoute}
            size="small"
            variant="text"
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            <Trans i18nKey="group.showAll" ns="myWork" values={{ count: group.count }} components={{ ltr: <Ltr>{null}</Ltr> }} />
          </Button>
        )}
      </Box>

      <Stack spacing={1}>
        {group.items.map((item) => (
          <MyWorkRow
            key={item.key}
            item={item}
            timeZoneId={timeZoneId}
            onTake={onTake}
            onRelease={onRelease}
            busyKey={busyKey}
          />
        ))}
      </Stack>
    </Paper>
  );
}
