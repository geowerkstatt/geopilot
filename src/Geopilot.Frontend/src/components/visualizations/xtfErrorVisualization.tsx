import { FC, useCallback, useEffect, useMemo, useReducer, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { MapVisualizationConfig, TreeField, TreeVisualizationConfig } from "../../api/apiInterfaces";
import { useLocalized } from "../../hooks/useLocalized";
import { FieldFilters, FilterBar } from "./filterBar";
import { MapVisualization } from "./map/mapVisualization";
import { MapVisualizationProvider, MapZoomRequest } from "./map/mapVisualizationProvider";
import { MapListLayout } from "./mapListLayout";
import { scrollIntoFullView } from "./scrollIntoFullView";
import { buildErrorIdIndex, buildTree, collectFilterAttributes, filterItems } from "./tree/treeNode";
import { TreeVisualization } from "./tree/treeVisualization";

/**
 * The composite XTF error visualization: an optional map and an optional error tree of the same validation
 * errors. Mirrors the backend XtfErrorVisualizationConfig. This component owns the state the two views
 * share: the filter (which filters both) and the selection (which cross-highlights both), correlated by a
 * shared errorId. The tree itself is built here from the flat items the backend ships, grouped by the
 * configured fields.
 */
export interface XtfErrorVisualizationConfig {
  map?: MapVisualizationConfig;
  tree?: TreeVisualizationConfig;
  /** Fields offered as filters, in display order; the filter spans map + tree. Absent without a tree. */
  filterBy?: TreeField[];
}

interface XtfErrorVisualizationProps {
  config: XtfErrorVisualizationConfig;
}

interface NodeSelectionState {
  selectedNodeId: string | null;
  selectionToken: number;
  /** Off after a zoom from the tree, which scrolls to the map instead of the tree's panel. */
  scrollToSelection: boolean;
}

interface NodeSelection {
  nodeId: string | null;
  scrollToSelection: boolean;
}

export const XtfErrorVisualization: FC<XtfErrorVisualizationProps> = ({ config }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const [messageQuery, setMessageQuery] = useState("");
  const [fieldFilters, setFieldFilters] = useState<FieldFilters<TreeField>>({});
  const [{ selectedNodeId, selectionToken, scrollToSelection }, dispatchSelection] = useReducer(
    (state: NodeSelectionState, { nodeId, scrollToSelection }: NodeSelection): NodeSelectionState => ({
      selectedNodeId: nodeId,
      selectionToken: state.selectionToken + 1, // automatically increment the token every time setSelectedNodeId is called, so the tree can scroll to the new selection
      scrollToSelection,
    }),
    { selectedNodeId: null, selectionToken: 0, scrollToSelection: true },
  );
  const setSelectedNodeId = useCallback(
    (nodeId: string | null) => dispatchSelection({ nodeId, scrollToSelection: true }),
    [],
  );
  const [fullscreen, setFullscreen] = useState(false);
  const [zoomRequest, setZoomRequest] = useState<MapZoomRequest | null>(null);
  const inlineMap = useRef<HTMLDivElement>(null);

  // After the render, once the selected node's panel has moved the tree; fullscreen shows the map beside the tree.
  useEffect(() => {
    if (zoomRequest && inlineMap.current) scrollIntoFullView(inlineMap.current);
  }, [zoomRequest]);

  const items = useMemo(() => config.tree?.items ?? [], [config.tree]);
  const groupBy = useMemo(() => config.tree?.groupBy ?? [], [config.tree]);
  const filterBy = useMemo(() => config.filterBy ?? [], [config.filterBy]);
  const attributes = useMemo(() => collectFilterAttributes(items, localized, filterBy), [items, localized, filterBy]);
  const hasActiveFilters =
    messageQuery.trim().length > 0 || Object.values(fieldFilters).some(values => (values?.length ?? 0) > 0);
  const filteredItems = useMemo(
    () => (hasActiveFilters ? filterItems(items, messageQuery.trim().toLowerCase(), fieldFilters, localized) : items),
    [items, hasActiveFilters, messageQuery, fieldFilters, localized],
  );

  const ungroupedLabel = t("treeVisualizationUngrouped");
  // The displayed hierarchy, rebuilt from the filtered items so structural ids, counts and selection stay consistent.
  const nodes = useMemo(
    () => buildTree(filteredItems, groupBy, localized, ungroupedLabel),
    [filteredItems, groupBy, localized, ungroupedLabel],
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

  // Error ids that actually have a feature on the map; a tree node is zoomable only when its subtree holds
  // at least one of them. Without a map (tree-only visualization) nothing is zoomable.
  const mappableErrorIds = useMemo(
    () => new Set((config.map?.layers ?? []).flatMap(layer => layer.features?.map(feature => feature.id) ?? [])),
    [config.map],
  );
  const zoomableNodeIds = useMemo(() => {
    const zoomable = new Set<string>();
    for (const [id, errorIds] of errorIdsByNodeId) {
      if (errorIds.some(errorId => mappableErrorIds.has(errorId))) zoomable.add(id);
    }
    return zoomable;
  }, [errorIdsByNodeId, mappableErrorIds]);

  const handleFieldFilterChange = (field: TreeField, selected: string[]) =>
    setFieldFilters(current => ({ ...current, [field]: selected }));
  const handleClearFilters = () => {
    setMessageQuery("");
    setFieldFilters({});
  };
  const handleSelectFeature = (errorId: string) => setSelectedNodeId(nodeIdByErrorId.get(errorId) ?? null);
  const handleZoomToNode = useCallback(
    (nodeId: string) => {
      dispatchSelection({ nodeId, scrollToSelection: false });
      const featureIds = errorIdsByNodeId.get(nodeId) ?? [];
      setZoomRequest(prev => ({ featureIds, token: (prev?.token ?? 0) + 1 }));
    },
    [errorIdsByNodeId],
  );

  const filter = config.tree && (
    <FilterBar
      attributes={attributes}
      searchPlaceholder="treeVisualizationMessageSearch"
      messageQuery={messageQuery}
      onMessageQueryChange={setMessageQuery}
      fieldFilters={fieldFilters}
      onFieldFilterChange={handleFieldFilterChange}
      onClearFilters={handleClearFilters}
      forceMobileView={fullscreen}
    />
  );
  const map = config.map && (
    <MapVisualization
      config={config.map}
      reserveSpaceForFilters={!!config.tree}
      fullscreen={fullscreen}
      setFullscreen={setFullscreen}
    />
  );
  const tree = config.tree && (
    <TreeVisualization
      nodes={nodes}
      selectedId={selectedNodeId}
      selectionToken={selectionToken}
      scrollToSelection={scrollToSelection}
      onSelect={setSelectedNodeId}
      onZoom={handleZoomToNode}
      zoomableNodeIds={zoomableNodeIds}
      filterActive={hasActiveFilters}
      totalCount={items.length}
      shownCount={filteredItems.length}
      fullscreen={fullscreen}
    />
  );

  return (
    <MapVisualizationProvider
      config={config.map}
      visibleFeatureIds={visibleErrorIds}
      highlightedFeatureIds={highlightedErrorIds}
      zoomRequest={zoomRequest}
      onSelectFeature={handleSelectFeature}
      showMapSelectionPopup={!config.tree}
      fullscreen={fullscreen}>
      <MapListLayout
        map={map}
        filter={filter}
        list={tree}
        fullscreen={fullscreen}
        setFullscreen={setFullscreen}
        inlineMapRef={inlineMap}
      />
    </MapVisualizationProvider>
  );
};
