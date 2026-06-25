import { useTranslation } from "react-i18next";
import { Table, TableBody, Typography } from "@mui/material";
import { geopilotTheme } from "../../../../../../appTheme";
import { GeopilotBox } from "../../../../../../components/styledComponents";
import { TreeNode } from "../treeNode";
import { MetadataRow } from "./metadataRow";

interface MetadataPanelProps {
  node: TreeNode | null;
  fullWidth?: boolean;
}

export const MetadataPanel = ({ node, fullWidth = false }: MetadataPanelProps) => {
  const { t } = useTranslation();
  const entries = node?.metadata ? Object.entries(node.metadata) : [];

  return (
    <GeopilotBox
      sx={{
        width: fullWidth ? "100%" : 380,
        maxWidth: "100%",
        flexShrink: 0,
        gap: 1,
        backgroundColor: geopilotTheme.palette.primary.active,
      }}>
      <Typography variant="subtitle2">{t("treeVisualizationMetadataTitle")}</Typography>
      <Table size="small" sx={{ tableLayout: "fixed" }}>
        <TableBody>
          {entries.map(([key, value]) => (
            <MetadataRow key={key} label={key} value={value} />
          ))}
        </TableBody>
      </Table>
    </GeopilotBox>
  );
};
