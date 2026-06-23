import { SyntheticEvent, useEffect, useMemo, useState } from "react";
import { Box, Typography } from "@mui/material";
import { SimpleTreeView } from "@mui/x-tree-view";
import { useTranslation } from "react-i18next";
import useFetch from "../../../../hooks/useFetch";
import {
  collectItemIds,
  collectMetadataAttributes,
  filterNodes,
  indexNodes,
  MetadataFilters,
  TreeNode,
} from "./treeNode";
import { renderTreeItems } from "./renderTreeItems";
import { MetadataPanel } from "./metadataPanel";
import { FilterBar } from "./filterBar";

interface TreeVisualizationProps {
  url: string;
}

export const TreeVisualization = ({ url }: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const [nodes, setNodes] = useState<TreeNode[] | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [messageQuery, setMessageQuery] = useState("");
  const [metadataFilters, setMetadataFilters] = useState<MetadataFilters>({});
  const [expandedItems, setExpandedItems] = useState<string[]>([]);

  useEffect(() => {
    let cancelled = false;
    setNodes(null);
    setErrorMessage(null);
    setSelectedId(null);
    setMessageQuery("");
    setMetadataFilters({});

    (async () => {
      try {
        const data = await fetchApi<TreeNode[]>(url);
        if (!cancelled) setNodes(data);
      } catch {
        if (!cancelled) setErrorMessage(t("treeVisualizationLoadFailed"));
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [url, t, fetchApi]);

  const attributes = useMemo(() => (nodes ? collectMetadataAttributes(nodes) : []), [nodes]);

  const hasActiveFilters =
    messageQuery.trim().length > 0 || Object.values(metadataFilters).some(values => values.length > 0);

  const filteredNodes = useMemo(() => {
    if (!nodes) return null;
    if (!hasActiveFilters) return nodes;
    return filterNodes(nodes, messageQuery.trim().toLowerCase(), metadataFilters);
  }, [nodes, hasActiveFilters, messageQuery, metadataFilters]);

  const items = useMemo(() => (filteredNodes ? renderTreeItems(filteredNodes) : null), [filteredNodes]);

  const nodesById = useMemo(() => {
    const map = new Map<string, TreeNode>();
    if (filteredNodes) indexNodes(filteredNodes, map);
    return map;
  }, [filteredNodes]);

  // While filters are active, every match should be visible without manual expansion.
  const expandedFilteredItems = useMemo(() => {
    if (!hasActiveFilters || !filteredNodes) return null;
    const ids: string[] = [];
    collectItemIds(filteredNodes, ids);
    return ids;
  }, [hasActiveFilters, filteredNodes]);

  const handleMetadataFilterChange = (key: string, selected: string[]) => {
    setMetadataFilters(current => ({ ...current, [key]: selected }));
  };

  const selectedNode = selectedId ? (nodesById.get(selectedId) ?? null) : null;

  if (errorMessage) {
    return (
      <Typography variant="body2" color="error">
        {errorMessage}
      </Typography>
    );
  }

  if (!nodes) return null;
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
        {filteredNodes && filteredNodes.length === 0 ? (
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

export default TreeVisualization;
