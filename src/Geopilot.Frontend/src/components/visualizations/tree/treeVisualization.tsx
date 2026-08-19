import { forwardRef, SyntheticEvent, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import UnfoldLessIcon from "@mui/icons-material/UnfoldLess";
import UnfoldMoreIcon from "@mui/icons-material/UnfoldMore";
import { Box, Stack, Typography, useTheme } from "@mui/material";
import { SimpleTreeView } from "@mui/x-tree-view";
import { TreeItem } from "../../../api/apiInterfaces";
import { Button } from "../../buttons";
import { ScrollMarginContext } from "../../scrollMargin/ScrollMarginContext";
import { DetailPanel } from "./detailPanel";
import { renderTreeItems } from "./renderTreeItems";
import { collectExpandableIds, collectItemIds, indexNodes, TreeNode } from "./treeNode";

interface TreeVisualizationProps {
  /** The nodes to render (already filtered by the coordinator). */
  nodes: TreeNode[];
  /** Structural id of the selected node, or null. */
  selectedId: string | null;
  /** Called with the structural node id when the selection changes (null when cleared). */
  onSelect: (nodeId: string | null) => void;
  /** A token that can be used to scroll to the selected node. */
  selectionToken?: unknown;
  /** Whether a filter is active: expands every match, shows the no-results hint, and switches the header count. */
  filterActive?: boolean;
  /** Total number of errors across all items, shown in the header. */
  totalCount: number;
  /** Number of errors currently shown (after filtering); equals totalCount when no filter is active. */
  shownCount: number;
  /** Whether the tree is shown in a fullscreen map or inline. */
  fullscreen?: boolean;
  /** Called with the structural node id to zoom the map to that node's errors (leaf or group). */
  onZoom?: (nodeId: string) => void;
  /** Structural ids of nodes whose subtree has at least one error present on the map (i.e. zoomable). */
  zoomableNodeIds?: ReadonlySet<string>;
}

// Once the tree can no longer keep its minimum width next to the detail box, the box is
// rendered inline, directly below the selected element instead.
const PANEL_WIDTH = 380;
const PANEL_GAP = 16;
const MIN_TREE_WIDTH = 400;
const SIDE_BY_SIDE_THRESHOLD = MIN_TREE_WIDTH + PANEL_GAP + PANEL_WIDTH;

const SCROLL_DEBOUNCE_MS = 100;

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

const InlineDetailPanel = forwardRef<HTMLDivElement, { item: TreeItem }>(({ item }, ref) => {
  const theme = useTheme();
  const { scrollMarginTop, scrollMarginBottom } = useContext(ScrollMarginContext);

  return (
    <Box
      ref={ref}
      sx={{
        scrollMarginTop: `calc(${scrollMarginTop} + ${theme.spacing(4)})`, // add additional spacing for the item itself, placed above the panel
        scrollMarginBottom,
      }}>
      <DetailPanel item={item} />
    </Box>
  );
});

export const TreeVisualization = ({
  nodes,
  selectedId,
  onSelect,
  selectionToken,
  filterActive = false,
  totalCount,
  shownCount,
  fullscreen,
  onZoom,
  zoomableNodeIds,
}: TreeVisualizationProps) => {
  const { t } = useTranslation();
  const [expandedItems, setExpandedItems] = useState<string[]>([]);
  const [measureContainer, containerWidth] = useElementWidth<HTMLDivElement>();
  const treeWrapperRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const inlinePanelRef = useRef<HTMLDivElement>(null);
  const treeRef = useRef<HTMLUListElement>(null);
  const { scrollMarginTop, scrollMarginBottom } = useContext(ScrollMarginContext);

  const sideBySide = !fullscreen && (containerWidth === 0 || containerWidth >= SIDE_BY_SIDE_THRESHOLD);

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
  const selectedItem = selectedNode?.item ?? null;

  const items = useMemo(() => {
    const zoomOptions = { onZoom, zoomableNodeIds };
    if (!sideBySide && selectedItem) {
      return renderTreeItems(nodes, "n", {
        ...zoomOptions,
        selectedId,
        inlinePanel: <InlineDetailPanel ref={inlinePanelRef} item={selectedItem} />,
      });
    }
    return renderTreeItems(nodes, "n", zoomOptions);
  }, [nodes, onZoom, selectedId, selectedItem, sideBySide, zoomableNodeIds]);

  // Align the box's top with the selected row, but keep it within the tree so a selection far down does not
  // push the box past the tree and grow the accordion: clamp to the tree's bottom edge. Recompute when
  // layout-affecting state changes.
  const calculatePanelTop = useCallback(() => {
    if (!sideBySide || !selectedId) {
      return 0;
    }
    const wrapper = treeWrapperRef.current;
    const selected = wrapper?.querySelector<HTMLElement>(".MuiTreeItem-content[data-selected]");
    if (!wrapper || !selected) {
      return 0;
    }
    const offset = selected.getBoundingClientRect().top - wrapper.getBoundingClientRect().top;
    const panelHeight = panelRef.current?.offsetHeight ?? 0;
    const maxTop = Math.max(0, wrapper.offsetHeight - panelHeight);
    return Math.min(Math.max(0, offset), maxTop);
  }, [selectedId, sideBySide]);

  useEffect(() => {
    const tree = treeRef.current;
    if (!tree) return;

    const scrollToPanel = () => {
      if (panelRef.current) {
        panelRef.current.style.marginTop = `${calculatePanelTop()}px`;
        panelRef.current.scrollIntoView({ block: "nearest", behavior: "smooth" });
      }
      inlinePanelRef.current?.scrollIntoView({ block: "nearest", behavior: "smooth" });
    };

    let scrollTimeout: number | undefined;
    const scheduleScroll = () => {
      clearTimeout(scrollTimeout);
      scrollTimeout = setTimeout(scrollToPanel, SCROLL_DEBOUNCE_MS);
    };

    scrollToPanel();

    const observer = new ResizeObserver(() => scheduleScroll());
    observer.observe(tree);
    return () => {
      clearTimeout(scrollTimeout);
      observer.disconnect();
    };
  }, [expandedItems, items, calculatePanelTop, selectionToken]);

  if (nodes.length === 0 && !filterActive) return null;

  return (
    <Stack ref={measureContainer} sx={{ width: "100%", minHeight: 0 }} spacing={1}>
      <Stack
        direction="row"
        sx={{ alignItems: "center", justifyContent: expandableIds.length > 0 ? "space-between" : "flex-start" }}>
        <Typography variant="body2" sx={{ m: 0 }}>
          {filterActive
            ? t("treeErrorCountFiltered", { count: totalCount, shown: shownCount })
            : t("treeErrorCount", { count: totalCount })}
        </Typography>
        {expandableIds.length > 0 && (
          <Button
            variant="text"
            size="small"
            onClick={toggleExpandAll}
            label={anyExpanded ? "collapseAll" : "expandAll"}
            endIcon={anyExpanded ? <UnfoldLessIcon /> : <UnfoldMoreIcon />}
          />
        )}
      </Stack>
      {nodes.length > 0 && (
        <Stack
          direction="row"
          sx={{
            alignItems: "flex-start",
            overflowY: fullscreen ? "auto" : undefined,
            ...(sideBySide || !fullscreen ? {} : { scrollPaddingTop: theme => theme.spacing(4) }), // reserve space for the item itself in fullscreen
          }}>
          <Box
            ref={treeWrapperRef}
            sx={{
              flex: "1 1 auto",
              minWidth: 0,
            }}>
            <SimpleTreeView
              ref={treeRef}
              selectedItems={selectedId}
              onSelectedItemsChange={(_: SyntheticEvent | null, itemId: string | null) => onSelect(itemId)}
              expandedItems={expandedItems}
              onExpandedItemsChange={(_: SyntheticEvent | null, itemIds: string[]) => setExpandedItems(itemIds)}
              sx={{
                "& .tree-zoom-button": { opacity: 0, transition: "opacity 0.1s ease" },
                "& .MuiTreeItem-content:hover .tree-zoom-button": { opacity: 1 },
                // Keyboard focus of the button and touch devices (no hover) still reveal it; selection alone
                // does not, so a selected row shows the control only while hovered.
                "& .tree-zoom-button:focus-visible": { opacity: 1 },
                "@media (hover: none)": { "& .tree-zoom-button": { opacity: 1 } },
              }}>
              {items}
            </SimpleTreeView>
          </Box>
          <Box
            sx={{
              display: sideBySide ? "block" : "none",
              flexShrink: 0,
              width: PANEL_WIDTH,
              maxWidth: "100%",
            }}>
            <Box
              ref={panelRef}
              sx={{
                display: selectedItem ? "block" : "none",
                scrollMarginTop,
                scrollMarginBottom,
              }}>
              {sideBySide && selectedItem && <DetailPanel item={selectedItem} />}
            </Box>
          </Box>
        </Stack>
      )}
    </Stack>
  );
};
