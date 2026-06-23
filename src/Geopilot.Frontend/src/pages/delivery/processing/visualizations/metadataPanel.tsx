import { Table, TableBody, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { GeopilotBox } from "../../../../components/styledComponents";
import { TreeNode } from "./treeNode";
import { MetadataRow } from "./metadataRow";

interface MetadataPanelProps {
  node: TreeNode | null;
}

export const MetadataPanel = ({ node }: MetadataPanelProps) => {
  const { t } = useTranslation();
  const entries = node?.metadata ? Object.entries(node.metadata) : [];

  return (
    <GeopilotBox sx={{ width: 380, flexShrink: 0, gap: 1 }}>
      <Typography variant="subtitle2">{t("treeVisualizationMetadataTitle")}</Typography>
      {entries.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          {t("treeVisualizationMetadataEmpty")}
        </Typography>
      ) : (
        <Table size="small" sx={{ tableLayout: "fixed" }}>
          <TableBody>
            {entries.map(([key, value]) => (
              <MetadataRow key={key} label={key} value={value} />
            ))}
          </TableBody>
        </Table>
      )}
    </GeopilotBox>
  );
};
