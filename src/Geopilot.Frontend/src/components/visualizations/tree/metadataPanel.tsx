import { useTranslation } from "react-i18next";
import { Table, TableBody, Typography } from "@mui/material";
import { geopilotTheme } from "../../../appTheme";
import { GeopilotBox } from "../../styledComponents";
import { MetadataRow } from "./metadataRow";
import { TreeNode } from "./treeNode";

interface MetadataPanelProps {
  node: TreeNode | null;
}

export const MetadataPanel = ({ node }: MetadataPanelProps) => {
  const { t } = useTranslation();
  const entries = node?.metadata ? Object.entries(node.metadata) : [];

  return (
    <GeopilotBox
      sx={{
        width: "100%",
        gap: 1,
        backgroundColor: geopilotTheme.palette.primary.states.selected,
      }}>
      <Typography variant="h6" m={0}>
        {t("treeVisualizationMetadataTitle")}
      </Typography>
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
