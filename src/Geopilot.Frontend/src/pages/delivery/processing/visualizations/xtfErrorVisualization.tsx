import { FC, useMemo, useState } from "react";
import { Stack } from "@mui/material";
import { MapVisualizationConfig } from "../../../../api/apiInterfaces";
import { FilterBar } from "./filterBar";
import { MapVisualization } from "./mapVisualization";
import {
  buildErrorIdIndex,
  collectMetadataAttributes,
  filterNodes,
  MetadataFilters,
  TreeVisualizationConfig,
} from "./treeNode";
import { TreeVisualization } from "./treeVisualization";

/**
 * The composite XTF error visualization: an optional map and an optional error tree of the same validation
 * errors. Mirrors the backend XtfErrorVisualizationConfig. This component owns the state the two views
 * share: the filter (which filters both) and the selection (which cross-highlights both), correlated by a
 * shared errorId.
 */
export interface XtfErrorVisualizationConfig {
  map?: MapVisualizationConfig;
  tree?: TreeVisualizationConfig;
}

interface XtfErrorVisualizationProps {
  config: XtfErrorVisualizationConfig;
}

export const XtfErrorVisualization: FC<XtfErrorVisualizationProps> = ({ config }) => {
  const [messageQuery, setMessageQuery] = useState("");
  const [metadataFilters, setMetadataFilters] = useState<MetadataFilters>({});
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);

  const treeNodes = useMemo(() => config.tree?.nodes ?? [], [config.tree]);
  const attributes = useMemo(() => collectMetadataAttributes(treeNodes), [treeNodes]);
  const hasActiveFilters =
    messageQuery.trim().length > 0 || Object.values(metadataFilters).some(values => values.length > 0);
  const filteredNodes = useMemo(
    () => (hasActiveFilters ? filterNodes(treeNodes, messageQuery.trim().toLowerCase(), metadataFilters) : treeNodes),
    [treeNodes, hasActiveFilters, messageQuery, metadataFilters],
  );

  // One index over the SAME nodes the tree renders (filteredNodes) so structural ids match. When no filter
  // is active filteredNodes === treeNodes, so this also covers the unfiltered case.
  const { nodeIdByErrorId, errorIdsByNodeId } = useMemo(() => buildErrorIdIndex(filteredNodes), [filteredNodes]);

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
  const handleSelectFeature = (errorId: string) => setSelectedNodeId(nodeIdByErrorId.get(errorId) ?? null);

  return (
    <Stack sx={{ width: "100%" }}>
      {config.tree && (
        <FilterBar
          attributes={attributes}
          messageQuery={messageQuery}
          onMessageQueryChange={setMessageQuery}
          metadataFilters={metadataFilters}
          onMetadataFilterChange={handleMetadataFilterChange}
        />
      )}
      {config.map && (
        <MapVisualization
          config={config.map}
          visibleErrorIds={visibleErrorIds}
          highlightedErrorIds={highlightedErrorIds}
          onSelectFeature={handleSelectFeature}
          showPopup={!config.tree}
        />
      )}
      {config.tree && (
        <TreeVisualization
          nodes={filteredNodes}
          selectedId={selectedNodeId}
          onSelect={setSelectedNodeId}
          filterActive={hasActiveFilters}
        />
      )}
    </Stack>
  );
};
