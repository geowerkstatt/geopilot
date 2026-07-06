import { SyntheticEvent, useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import UnfoldLessIcon from "@mui/icons-material/UnfoldLess";
import UnfoldMoreIcon from "@mui/icons-material/UnfoldMore";
import { Box, Stack, Typography } from "@mui/material";
import { SimpleTreeView } from "@mui/x-tree-view";
import { Button } from "../../../../components/buttons.tsx";
import { MetadataPanel } from "./metadataPanel";
import { renderTreeItems } from "./renderTreeItems";
import { collectExpandableIds, collectItemIds, indexNodes, TreeNode } from "./treeNode";

interface TreeVisualizationProps {
  /** The nodes to render (already filtered by the coordinator). */
  nodes: TreeNode[];
  /** Structural id of the selected node, or null. */
  selectedId: string | null;
  /** Called with the structural node id when the selection changes (null when cleared). */
  onSelect: (nodeId: string | null) => void;
  /** Whether a filter is active: expands every match, shows the no-results hint, and switches the header count. */
  filterActive?: boolean;
  /** Total number of errors across all items, shown in the header. */
  totalCount: number;
  /** Number of errors currently shown (after filtering); equals totalCount when no filter is active. */
  shownCount: number;
}

// Once the tree can no longer keep its minimum width next to the detail box, the box is
// rendered inline, directly below the selected element instead.
const PANEL_WIDTH = 380;
const PANEL_GAP = 16;
const MIN_TREE_WIDTH = 400;
const SIDE_BY_SIDE_THRESHOLD = MIN_TREE_WIDTH + PANEL_GAP + PANEL_WIDTH;

// Ancestor structural ids of a node id, e.g. "n-0-2" -> ["n-0"]. The root prefix "n" alone is not a node.
const ancestorIds = (id: string): string[] => {
  const segments = id.split("-");
  const ancestors: string[] = [];
  for (let end = 2; end < segments.length; end++) {
    ancestors.push(segments.slice(0, end).join("-"));
  }
  return ancestors;
};

// Tracks an element's width via ResizeObserver. Returns a callback ref to attach to the element and the
// latest measured width (0 until mounted). A callback ref handles the element mounting only once data loads.
const useElementWidth = <T extends HTMLElement>(): [(node: T | null) => void, number] => {
  const [width, setWidth] = useState(0);
  const observerRef = useRef<ResizeObserver | null>(null);

  const ref = useCallback((node: T | null) => {
    observerRef.current?.disconnect();
    if (!node) return;
    const observer = new ResizeObserver(entries => setWidth(entries[0].contentRect.width));
    observer.observe(node);
    observerRef.current = observer;
  }, []);

  return [ref, width];
};

export const TreeVisualization = ({
  nodes,
  selectedId,
  onSelect,
  filterActive = false,
  totalCount,
  shownCount,
}: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const [expandedItems, setExpandedItems] = useState<string[]>([]);
  const [panelTop, setPanelTop] = useState(0);
  const [measureContainer, containerWidth] = useElementWidth<HTMLDivElement>();
  const treeWrapperRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  const sideBySide = containerWidth === 0 || containerWidth >= SIDE_BY_SIDE_THRESHOLD;

  const allItemIds = useMemo(() => {
    const ids: string[] = [];
    collectItemIds(nodes, ids);
    return ids;
  }, [nodes]);

  const expandableIds = useMemo(() => {
    const ids: string[] = [];
    collectExpandableIds(nodes, ids);
    return ids;
  }, [nodes]);

  const nodesById = useMemo(() => {
    const map = new Map<string, TreeNode>();
    indexNodes(nodes, map);
    return map;
  }, [nodes]);

  // Expand the ancestors of an externally selected node (e.g. selected by clicking a map feature) so it is
  // visible without manual expansion.
  useEffect(() => {
    if (!selectedId) return;
    setExpandedItems(prev => Array.from(new Set([...prev, ...ancestorIds(selectedId)])));
  }, [selectedId]);

  // Applying or changing a filter reveals its matches by expanding all nodes. Expansion then stays
  // user-controlled, so the tree can still be collapsed and expanded while a filter is active.
  useEffect(() => {
    if (filterActive) setExpandedItems(allItemIds);
  }, [filterActive, allItemIds]);

  const anyExpanded = expandableIds.some(id => expandedItems.includes(id));
  const toggleExpandAll = () => {
    setExpandedItems(anyExpanded ? [] : expandableIds);
    onSelect(null);
  };

  const selectedNode = selectedId ? (nodesById.get(selectedId) ?? null) : null;
  const hasMetadata = !!selectedNode?.metadata && Object.keys(selectedNode.metadata).length > 0;

  const items = useMemo(() => {
    if (!sideBySide && hasMetadata) {
      return renderTreeItems(nodes, "n", {
        selectedId,
        inlinePanel: <MetadataPanel node={selectedNode} />,
      });
    }
    return renderTreeItems(nodes);
  }, [nodes, sideBySide, hasMetadata, selectedId, selectedNode]);

  // Align the box's top with the selected row, but keep it within the tree so a selection far down does not
  // push the box past the tree and grow the accordion: clamp to the tree's bottom edge. Recompute when
  // layout-affecting state changes.
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
    const panelHeight = panelRef.current?.offsetHeight ?? 0;
    const maxTop = Math.max(0, wrapper.offsetHeight - panelHeight);
    setPanelTop(Math.min(Math.max(0, offset), maxTop));
  }, [sideBySide, selectedId, expandedItems, items, containerWidth]);

  // Scroll the selected node into view once it (and its now-expanded ancestors) are rendered.
  useEffect(() => {
    if (!selectedId) return;
    treeWrapperRef.current?.querySelector<HTMLElement>(".Mui-selected")?.scrollIntoView({ block: "nearest" });
  }, [selectedId, expandedItems]);

  if (nodes.length === 0 && !filterActive) return null;

  return (
    <Stack ref={measureContainer} sx={{ width: "100%" }} gap={1}>
      <Stack
        direction="row"
        sx={{ alignItems: "center", justifyContent: expandableIds.length > 0 ? "space-between" : "flex-start" }}>
        <Typography variant="body2" m={0}>
          {filterActive
            ? t("treeErrorCountFiltered", { count: totalCount, shown: shownCount })
            : t("treeErrorCount", { count: totalCount })}
        </Typography>
        {expandableIds.length > 0 && (
          <Button
            variant="text"
            size="small"
            onClick={toggleExpandAll}
            label={anyExpanded ? "treeCollapseAll" : "treeExpandAll"}
            endIcon={anyExpanded ? <UnfoldLessIcon /> : <UnfoldMoreIcon />}
          />
        )}
      </Stack>
      {nodes.length > 0 && (
        <Stack direction="row" sx={{ alignItems: "flex-start" }}>
          <Box
            ref={treeWrapperRef}
            sx={{
              flex: "1 1 auto",
              minWidth: 0,
            }}>
            <SimpleTreeView
              selectedItems={selectedId}
              onSelectedItemsChange={(_: SyntheticEvent, itemId: string | null) => onSelect(itemId)}
              expandedItems={expandedItems}
              onExpandedItemsChange={(_: SyntheticEvent, itemIds: string[]) => setExpandedItems(itemIds)}
              sx={{ "& .MuiTreeItem-content": { pl: 0 } }}>
              {items}
            </SimpleTreeView>
          </Box>
          <Box
            ref={panelRef}
            sx={{
              display: sideBySide ? "block" : "none",
              mt: `${panelTop}px`,
              flexShrink: 0,
              transition: "margin-top 0.15s ease",
              width: PANEL_WIDTH,
              maxWidth: "100%",
            }}>
            {sideBySide && hasMetadata && <MetadataPanel node={selectedNode} />}
          </Box>
        </Stack>
      )}
    </Stack>
  );
};
