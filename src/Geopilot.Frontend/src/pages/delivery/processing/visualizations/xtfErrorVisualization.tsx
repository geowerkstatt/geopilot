import { FC, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack } from "@mui/material";
import { MapVisualizationConfig, TreeVisualizationConfig } from "../../../../api/apiInterfaces";
import { useLocalized } from "../../../../hooks/useLocalized";
import { FilterBar } from "./filterBar";
import { MapVisualization } from "./mapVisualization";
import { buildErrorIdIndex, buildTree, collectMetadataAttributes, filterItems, MetadataFilters } from "./treeNode";
import { TreeVisualization } from "./treeVisualization";

/**
 * The composite XTF error visualization: an optional map and an optional error tree of the same validation
 * errors. Mirrors the backend XtfErrorVisualizationConfig. This component owns the state the two views
 * share: the filter (which filters both) and the selection (which cross-highlights both), correlated by a
 * shared errorId. The tree itself is built here from the flat items the backend ships, grouped by the
 * configured metadata keys.
 */
export interface XtfErrorVisualizationConfig {
  map?: MapVisualizationConfig;
  tree?: TreeVisualizationConfig;
  /** Metadata keys offered as filters, in display order; the filter spans map + tree. Absent without a tree. */
  filterBy?: string[];
}

interface XtfErrorVisualizationProps {
  config: XtfErrorVisualizationConfig;
}

export const XtfErrorVisualization: FC<XtfErrorVisualizationProps> = ({ config }) => {
  const { t } = useTranslation();
  const localize = useLocalized();
  const [messageQuery, setMessageQuery] = useState("");
  const [metadataFilters, setMetadataFilters] = useState<MetadataFilters>({});
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);

  const items = useMemo(() => config.tree?.items ?? [], [config.tree]);
  const groupBy = useMemo(() => config.tree?.groupBy ?? [], [config.tree]);
  const filterBy = useMemo(() => config.filterBy ?? [], [config.filterBy]);
  const attributes = useMemo(() => collectMetadataAttributes(items, localize, filterBy), [items, localize, filterBy]);
  const hasActiveFilters =
    messageQuery.trim().length > 0 || Object.values(metadataFilters).some(values => values.length > 0);
  const filteredItems = useMemo(
    () => (hasActiveFilters ? filterItems(items, messageQuery.trim().toLowerCase(), metadataFilters, localize) : items),
    [items, hasActiveFilters, messageQuery, metadataFilters, localize],
  );

  const ungroupedLabel = t("treeVisualizationUngrouped");
  // The displayed hierarchy, rebuilt from the filtered items so structural ids, counts and selection stay consistent.
  const nodes = useMemo(
    () => buildTree(filteredItems, groupBy, localize, ungroupedLabel),
    [filteredItems, groupBy, localize, ungroupedLabel],
  );

  // One index over the SAME nodes the tree renders so structural ids match.
  const { nodeIdByErrorId, errorIdsByNodeId } = useMemo(() => buildErrorIdIndex(nodes), [nodes]);

  // Filter result for the map: the error ids still visible (undefined = no filter = show all).
  const visibleErrorIds = useMemo<ReadonlySet<string> | undefined>(
    () => (hasActiveFilters ? new Set([...errorIdsByNodeId.values()].flat()) : undefined),
    [hasActiveFilters, errorIdsByNodeId],
  );
  // Selection for the map: the error ids under the selected node (one for a leaf, many for a group).
  const highlightedErrorIds = useMemo<ReadonlySet<string>>(
    () => new Set(selectedNodeId ? (errorIdsByNodeId.get(selectedNodeId) ?? []) : []),
    [selectedNodeId, errorIdsByNodeId],
  );

  const handleMetadataFilterChange = (key: string, selected: string[]) =>
    setMetadataFilters(current => ({ ...current, [key]: selected }));
  const handleClearFilters = () => {
    setMessageQuery("");
    setMetadataFilters({});
  };
  const handleSelectFeature = (errorId: string) => setSelectedNodeId(nodeIdByErrorId.get(errorId) ?? null);
  const toggleFullscreen = () => setIsFullscreen(current => !current);

  // Allow Escape to leave the fullscreen overlay.
  useEffect(() => {
    if (!isFullscreen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setIsFullscreen(false);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [isFullscreen]);

  return (
    <Stack
      sx={{
        width: "100%",
        // In fullscreen the whole visualization becomes a fixed overlay filling the viewport; the map grows.
        ...(isFullscreen && {
          position: "fixed",
          inset: 0,
          zIndex: "modal",
          height: "100vh",
          bgcolor: "background.default",
          p: 2,
          overflow: "auto",
        }),
      }}>
      {config.tree && (
        <FilterBar
          attributes={attributes}
          messageQuery={messageQuery}
          onMessageQueryChange={setMessageQuery}
          metadataFilters={metadataFilters}
          onMetadataFilterChange={handleMetadataFilterChange}
          onClearFilters={handleClearFilters}
        />
      )}
      {config.map && (
        <Box sx={{ ...(isFullscreen && { flex: 1, minHeight: 0 }) }}>
          <MapVisualization
            config={config.map}
            visibleErrorIds={visibleErrorIds}
            highlightedErrorIds={highlightedErrorIds}
            onSelectFeature={handleSelectFeature}
            showPopup={!config.tree}
            isFullscreen={isFullscreen}
            onToggleFullscreen={toggleFullscreen}
          />
        </Box>
      )}
      {config.tree && (
        <TreeVisualization
          nodes={nodes}
          selectedId={selectedNodeId}
          onSelect={setSelectedNodeId}
          filterActive={hasActiveFilters}
          totalCount={items.length}
          shownCount={filteredItems.length}
        />
      )}
    </Stack>
  );
};
