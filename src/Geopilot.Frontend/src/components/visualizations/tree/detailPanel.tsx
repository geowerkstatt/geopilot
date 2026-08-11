import { useTranslation } from "react-i18next";
import { Table, TableBody, Typography } from "@mui/material";
import { TreeItem } from "../../../api/apiInterfaces";
import { geopilotTheme } from "../../../appTheme";
import { useLocalized } from "../../../hooks/useLocalized";
import { GeopilotBox } from "../../styledComponents";
import { DetailRow } from "./detailRow";

interface DetailPanelProps {
  item: TreeItem;
}

/** Shows the fields of the selected error in a fixed order, skipping the ones the error does not carry. */
export const DetailPanel = ({ item }: DetailPanelProps) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();

  return (
    <GeopilotBox
      sx={{
        width: "100%",
        gap: 1,
        backgroundColor: geopilotTheme.palette.primary.states.selected,
      }}>
      <Typography variant="h6" sx={{ m: 0 }}>
        {t("treeVisualizationMetadataTitle")}
      </Typography>
      <Table size="small" sx={{ tableLayout: "fixed" }}>
        <TableBody>
          {item.errorType && <DetailRow label={t("treeFieldErrorType")} value={localized(item.errorType)} />}
          {item.tid && <DetailRow label={t("treeFieldTid")} value={item.tid} />}
          {item.model && <DetailRow label={t("treeFieldModel")} value={item.model} />}
          {item.topic && <DetailRow label={t("treeFieldTopic")} value={item.topic} />}
          {item.class && <DetailRow label={t("treeFieldClass")} value={item.class} />}
          <DetailRow label={t("treeFieldMessage")} value={item.message} />
          {item.line !== undefined && <DetailRow label={t("treeFieldLine")} value={String(item.line)} />}
          {item.coordinates && <DetailRow label={t("treeFieldCoordinates")} value={item.coordinates} />}
        </TableBody>
      </Table>
    </GeopilotBox>
  );
};
