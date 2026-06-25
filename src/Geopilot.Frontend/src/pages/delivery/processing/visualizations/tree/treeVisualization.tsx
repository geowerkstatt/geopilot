import { SyntheticEvent, useCallback, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, Typography } from "@mui/material";
import { SimpleTreeView } from "@mui/x-tree-view";
import { FlexBox } from "../../../../../components/styledComponents";
import { FilterBar } from "./filter/filterBar";
import { MetadataPanel } from "./metadata/metadataPanel";
import { renderTreeItems } from "./renderTreeItems";
import {
  collectItemIds,
  collectMetadataAttributes,
  filterNodes,
  indexNodes,
  MetadataFilters,
  TreeNode,
  TreeVisualizationConfig,
} from "./treeNode";

interface TreeVisualizationProps {
  config: TreeVisualizationConfig;
}

// Once the tree can no longer keep its minimum width next to the detail box, the box is
// rendered inline, directly below the selected element instead.
const PANEL_WIDTH = 380;
const PANEL_GAP = 16;
const MIN_TREE_WIDTH = 400;
const SIDE_BY_SIDE_THRESHOLD = MIN_TREE_WIDTH + PANEL_GAP + PANEL_WIDTH;

export const TreeVisualization = ({ config }: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const nodes = config.nodes;
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [messageQuery, setMessageQuery] = useState("");
  const [metadataFilters, setMetadataFilters] = useState<MetadataFilters>({});
  const [expandedItems, setExpandedItems] = useState<string[]>([]);
  const [containerWidth, setContainerWidth] = useState(0);
  const [panelTop, setPanelTop] = useState(0);
  const resizeObserverRef = useRef<ResizeObserver | null>(null);
  const treeWrapperRef = useRef<HTMLDivElement>(null);

  const sideBySide = containerWidth === 0 || containerWidth >= SIDE_BY_SIDE_THRESHOLD;

  // Callback ref: the container only mounts once data is loaded, so attach the observer
  // when the element appears rather than on first render (when nothing is rendered yet).
  const measureContainer = useCallback((node: HTMLDivElement | null) => {
    resizeObserverRef.current?.disconnect();
    if (!node) return;
    const observer = new ResizeObserver(entries => {
      setContainerWidth(entries[0].contentRect.width);
    });
    observer.observe(node);
    resizeObserverRef.current = observer;
  }, []);

  const attributes = useMemo(() => collectMetadataAttributes(nodes), [nodes]);

  const hasActiveFilters =
    messageQuery.trim().length > 0 || Object.values(metadataFilters).some(values => values.length > 0);

  const filteredNodes = useMemo(() => {
    if (!hasActiveFilters) return nodes;
    return filterNodes(nodes, messageQuery.trim().toLowerCase(), metadataFilters);
  }, [nodes, hasActiveFilters, messageQuery, metadataFilters]);

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
  const hasMetadata = !!selectedNode?.metadata && Object.keys(selectedNode.metadata).length > 0;

  // In narrow layouts the box is woven into the tree right below the selected item.
  const items = useMemo(() => {
    if (!sideBySide && hasMetadata) {
      return renderTreeItems(filteredNodes, "n", {
        selectedId,
        inlinePanel: <MetadataPanel node={selectedNode} fullWidth />,
      });
    }
    return renderTreeItems(filteredNodes);
  }, [filteredNodes, sideBySide, hasMetadata, selectedId, selectedNode]);

  // Align the box's top with the selected row; recompute when layout-affecting state changes.
  useLayoutEffect(() => {
    if (!sideBySide || !selectedId) {
      setPanelTop(0);
      return;
    }
    const wrapper = treeWrapperRef.current;
    const selected = wrapper?.querySelector<HTMLElement>(".Mui-selected");
    if (!wrapper || !selected) {
      setPanelTop(0);
      return;
    }
    const offset = selected.getBoundingClientRect().top - wrapper.getBoundingClientRect().top;
    setPanelTop(Math.max(0, offset));
  }, [sideBySide, selectedId, expandedItems, expandedFilteredItems, items, containerWidth]);

  if (nodes.length === 0) return null;

  return (
    <FlexBox ref={measureContainer} sx={{ width: "100%" }}>
      <FilterBar
        attributes={attributes}
        messageQuery={messageQuery}
        onMessageQueryChange={setMessageQuery}
        metadataFilters={metadataFilters}
        onMetadataFilterChange={handleMetadataFilterChange}
      />
      <Stack direction="row" sx={{ gap: 2, alignItems: "flex-start" }}>
        {filteredNodes.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("treeVisualizationNoResults")}
          </Typography>
        ) : (
          <>
            <Box ref={treeWrapperRef} sx={{ flex: "1 1 auto", minWidth: 0 }}>
              <SimpleTreeView
                selectedItems={selectedId}
                onSelectedItemsChange={(_: SyntheticEvent, itemId: string | null) => setSelectedId(itemId)}
                expandedItems={expandedFilteredItems ?? expandedItems}
                onExpandedItemsChange={(_: SyntheticEvent, itemIds: string[]) => setExpandedItems(itemIds)}>
                {items}
              </SimpleTreeView>
            </Box>
            {sideBySide && hasMetadata && (
              <Box sx={{ mt: `${panelTop}px`, flexShrink: 0, transition: "margin-top 0.15s ease" }}>
                <MetadataPanel node={selectedNode} />
              </Box>
            )}
          </>
        )}
      </Stack>
    </FlexBox>
  );
};
