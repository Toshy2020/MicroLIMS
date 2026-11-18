import {
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer,
  Paper,
  Typography,
  useTheme
} from "@mui/material";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import type { SolutionPreparationComponentResponse } from "../types";

interface Props {
  components: SolutionPreparationComponentResponse[];
}

export function PreparationComponentsTable({ components }: Props) {
  const { t } = useTranslation("solutionPreparation");
  const theme = useTheme();

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ width: 40, fontWeight: 700 }}>{t("componentsTable.columns.order")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("componentsTable.columns.reagentMaterial")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("componentsTable.columns.targetQuantity")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("componentsTable.columns.consumedLot")}</TableCell>
            <TableCell sx={{ fontWeight: 700, textAlign: "right" }}>{t("componentsTable.columns.quantityUsed")}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {components.map((c) =>
            c.kind === "Text" ? (
              <TableRow key={c.id}>
                <TableCell><Ltr>{c.order}</Ltr></TableCell>
                <TableCell colSpan={4}>
                  <Typography variant="body2" sx={{ fontStyle: "italic" }}>{c.text}</Typography>
                </TableCell>
              </TableRow>
            ) : (
              <TableRow key={c.id}>
                <TableCell><Ltr>{c.order}</Ltr></TableCell>
                <TableCell>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>{c.entryName}</Typography>
                  <Typography variant="caption" sx={{ color: "text.secondary" }}>
                    {c.kind === "PreparedSolution" ? t("componentsTable.preparedSolution") : <Ltr>{c.entryCode}</Ltr>}
                  </Typography>
                </TableCell>
                <TableCell><Ltr>{c.recipeQuantity} {c.recipeUnit}</Ltr></TableCell>
                <TableCell>
                  {c.kind === "PreparedSolution" ? (
                    c.sourcePreparationCode ? (
                      <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                        <Ltr>{c.sourcePreparationCode}</Ltr>
                      </Typography>
                    ) : (
                      <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic" }}>
                        {t("componentsTable.none")}
                      </Typography>
                    )
                  ) : c.lotBatchNumber ? (
                    <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                      <Ltr>{c.lotBatchNumber}</Ltr>
                    </Typography>
                  ) : (
                    <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic" }}>
                      {t("componentsTable.none")}
                    </Typography>
                  )}
                </TableCell>
                <TableCell sx={{ textAlign: "right" }}>
                  {c.quantityUsed != null ? (
                    <Typography variant="body2" sx={{ fontWeight: 700 }}>
                      <Ltr>{c.quantityUsed} {c.kind === "PreparedSolution" ? c.recipeUnit ?? "" : c.lotUnit ?? ""}</Ltr>
                    </Typography>
                  ) : "—"}
                </TableCell>
              </TableRow>
            )
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
