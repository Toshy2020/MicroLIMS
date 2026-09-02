import { Box, useTheme } from "@mui/material";
import { DiscussionCategory } from "../types/discussionTypes";
import { StatusTone } from "../../../theme/statusTokens";

const categoryToneMap: Record<DiscussionCategory, StatusTone> = {
  [DiscussionCategory.Water]: "info",
  [DiscussionCategory.Equipment]: "action",
  [DiscussionCategory.EnvironmentalMonitoring]: "purple",
  [DiscussionCategory.Products]: "notDetected",
  [DiscussionCategory.MediaMaterials]: "pale",
  [DiscussionCategory.InternalDecisions]: "info",
  [DiscussionCategory.ManagementRequirements]: "purple",
  [DiscussionCategory.EdaRequirements]: "detected",
  [DiscussionCategory.Iso17025]: "notDetected",
  [DiscussionCategory.GmpRegulatory]: "action",
  [DiscussionCategory.Other]: "pending"
};

interface Props {
  category: DiscussionCategory;
  categoryName: string;
}

export function DiscussionCategoryBadge({ category, categoryName }: Props) {
  const theme = useTheme();
  const tone = categoryToneMap[category] ?? "pending";
  const tokens = theme.custom.status[tone];

  return (
    <Box
      component="span"
      sx={{
        display: "inline-block",
        px: 1.25,
        py: 0.35,
        borderRadius: 5,
        fontSize: 11.5,
        fontWeight: 700,
        bgcolor: tokens.bg,
        color: tokens.text,
        border: `1px solid ${tokens.border}`,
        lineHeight: 1.2,
        whiteSpace: "nowrap"
      }}
    >
      {categoryName}
    </Box>
  );
}
