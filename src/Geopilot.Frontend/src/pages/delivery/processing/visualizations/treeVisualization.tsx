import { SyntheticEvent, useMemo, useState } from "react";
import { Box, Typography } from "@mui/material";
import { SimpleTreeView } from "@mui/x-tree-view";
import { useTranslation } from "react-i18next";
import {
  collectItemIds,
  collectMetadataAttributes,
  filterNodes,
  indexNodes,
  MetadataFilters,
  TreeNode,
  TreeVisualizationConfig,
} from "./treeNode";
import { renderTreeItems } from "./renderTreeItems";
import { MetadataPanel } from "./metadataPanel";
import { FilterBar } from "./filterBar";

interface TreeVisualizationProps {
  config: TreeVisualizationConfig;
}

export const TreeVisualization = ({ config }: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const nodes = config.nodes;
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [messageQuery, setMessageQuery] = useState("");
  const [metadataFilters, setMetadataFilters] = useState<MetadataFilters>({});
  const [expandedItems, setExpandedItems] = useState<string[]>([]);

  const attributes = useMemo(() => collectMetadataAttributes(nodes), [nodes]);

  const hasActiveFilters =
    messageQuery.trim().length > 0 || Object.values(metadataFilters).some(values => values.length > 0);

  const filteredNodes = useMemo(() => {
    if (!hasActiveFilters) return nodes;
    return filterNodes(nodes, messageQuery.trim().toLowerCase(), metadataFilters);
  }, [nodes, hasActiveFilters, messageQuery, metadataFilters]);

  const items = useMemo(() => renderTreeItems(filteredNodes), [filteredNodes]);

  const nodesById = useMemo(() => {
    const map = new Map<string, TreeNode>();
    indexNodes(filteredNodes, map);
    return map;
  }, [filteredNodes]);

  // While filters are active, every match should be visible without manual expansion.
  const expandedFilteredItems = useMemo(() => {
    if (!hasActiveFilters) return null;
    const ids: string[] = [];
    collectItemIds(filteredNodes, ids);
    return ids;
  }, [hasActiveFilters, filteredNodes]);

  const handleMetadataFilterChange = (key: string, selected: string[]) => {
    setMetadataFilters(current => ({ ...current, [key]: selected }));
  };

  const selectedNode = selectedId ? (nodesById.get(selectedId) ?? null) : null;

  if (nodes.length === 0) return null;

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 2, width: "100%" }}>
      <FilterBar
        attributes={attributes}
        messageQuery={messageQuery}
        onMessageQueryChange={setMessageQuery}
        metadataFilters={metadataFilters}
        onMetadataFilterChange={handleMetadataFilterChange}
      />
      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, alignItems: "flex-start" }}>
        {filteredNodes.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("treeVisualizationNoResults")}
          </Typography>
        ) : (
          <SimpleTreeView
            sx={{ flex: "1 1 auto", minWidth: 0 }}
            selectedItems={selectedId}
            onSelectedItemsChange={(_: SyntheticEvent, itemId: string | null) => setSelectedId(itemId)}
            expandedItems={expandedFilteredItems ?? expandedItems}
            onExpandedItemsChange={(_: SyntheticEvent, itemIds: string[]) => setExpandedItems(itemIds)}>
            {items}
          </SimpleTreeView>
        )}
        <MetadataPanel node={selectedNode} />
      </Box>
    </Box>
  );
};
