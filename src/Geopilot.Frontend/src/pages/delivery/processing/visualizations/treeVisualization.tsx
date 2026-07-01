import { SyntheticEvent, useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import UnfoldLessIcon from "@mui/icons-material/UnfoldLess";
import UnfoldMoreIcon from "@mui/icons-material/UnfoldMore";
import { Box, Button, Stack, Typography } from "@mui/material";
import { SimpleTreeView } from "@mui/x-tree-view";
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
  /** Whether a filter is active: expands every node so all matches are visible, and shows the no-results hint. */
  filterActive?: boolean;
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

export const TreeVisualization = ({ nodes, selectedId, onSelect, filterActive = false }: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const [expandedItems, setExpandedItems] = useState<string[]>([]);
  const [containerWidth, setContainerWidth] = useState(0);
  const [panelTop, setPanelTop] = useState(0);
  const resizeObserverRef = useRef<ResizeObserver | null>(null);
  const treeWrapperRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

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

  // While a filter is active every match is shown expanded; otherwise expansion is user-controlled.
  const expanded = filterActive ? allItemIds : expandedItems;

  // Toggle between fully expanded and fully collapsed. Disabled while a filter forces everything open.
  // Clears the selection so the metadata box does not linger at a stale position when its row is collapsed away.
  const allExpanded = expandableIds.length > 0 && expandableIds.every(id => expandedItems.includes(id));
  const toggleExpandAll = () => {
    setExpandedItems(allExpanded ? [] : expandableIds);
    onSelect(null);
  };

  const selectedNode = selectedId ? (nodesById.get(selectedId) ?? null) : null;
  const hasMetadata = !!selectedNode?.metadata && Object.keys(selectedNode.metadata).length > 0;

  // In narrow layouts the box is woven into the tree right below the selected item.
  const items = useMemo(() => {
    if (!sideBySide && hasMetadata) {
      return renderTreeItems(nodes, "n", {
        selectedId,
        inlinePanel: <MetadataPanel node={selectedNode} fullWidth />,
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
  }, [sideBySide, selectedId, expanded, items, containerWidth]);

  // Scroll the selected node into view once it (and its now-expanded ancestors) are rendered.
  useEffect(() => {
    if (!selectedId) return;
    treeWrapperRef.current?.querySelector<HTMLElement>(".Mui-selected")?.scrollIntoView({ block: "nearest" });
  }, [selectedId, expanded]);

  const getResultMessage = () => {
    if (filterActive) {
      // TODO: Add translation keys using i18n plurals
      return "Showing 3 out of 18 errors found";
    } else if (nodes.length === 0) {
      return t("treeVisualizationNoResults");
    } else {
      // TODO: Add translation keys using i18n plurals
      return "18 errors found";
    }
  };

  if (nodes.length === 0 && !filterActive) return null;

  return (
    <Stack ref={measureContainer} sx={{ width: "100%", position: "relative", gap: 1 }}>
      <Stack
        direction="row"
        sx={{ justifyContent: nodes.length > 0 ? "space-between" : "flex-start", alignItems: "center" }}>
        <Typography variant="body2" m={0}>
          {getResultMessage()}
        </Typography>
        {nodes.length > 0 && (
          <Button
            data-cy="tree-expand-toggle"
            onClick={toggleExpandAll}
            disabled={expandableIds.length === 0}
            variant="text"
            size="small"
            endIcon={allExpanded ? <UnfoldLessIcon fontSize="small" /> : <UnfoldMoreIcon fontSize="small" />}>
            {allExpanded ? t("treeCollapseAll") : t("treeExpandAll")}
          </Button>
        )}
      </Stack>
      <Stack direction="row" sx={{ alignItems: "flex-start" }}>
        <Box
          ref={treeWrapperRef}
          sx={{
            flex: "1 1 auto",
            minWidth: 0,
            // Reserve the detail box's width so the tree (and its selection highlight) end at the same
            // boundary whether or not the box is currently rendered.
            maxWidth: sideBySide ? `calc(100% - ${PANEL_WIDTH + PANEL_GAP}px)` : "100%",
          }}>
          <SimpleTreeView
            selectedItems={selectedId}
            onSelectedItemsChange={(_: SyntheticEvent, itemId: string | null) => onSelect(itemId)}
            expandedItems={expanded}
            onExpandedItemsChange={(_: SyntheticEvent, itemIds: string[]) => setExpandedItems(itemIds)}>
            {items}
          </SimpleTreeView>
        </Box>
        {sideBySide && hasMetadata && (
          <Box ref={panelRef} sx={{ mt: `${panelTop}px`, flexShrink: 0, transition: "margin-top 0.15s ease" }}>
            <MetadataPanel node={selectedNode} />
          </Box>
        )}
      </Stack>
    </Stack>
  );
};
