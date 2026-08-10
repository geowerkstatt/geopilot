import { FC, Reducer, useCallback, useMemo, useReducer, useState } from "react";
import { useTranslation } from "react-i18next";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import { Box, Modal, Stack, useTheme } from "@mui/material";
import { MapVisualizationConfig, TreeField, TreeVisualizationConfig } from "../../api/apiInterfaces";
import { IconButton } from "../../components/buttons";
import { GeopilotBox } from "../../components/styledComponents";
import { useLocalized } from "../../hooks/useLocalized";
import { stopStepSwipePropagation } from "../../hooks/useStepSwipe";
import { ScrollMarginProvider } from "../scrollMargin/ScrollMarginProvider";
import { FilterBar } from "./filterBar";
import { MapVisualization } from "./map/mapVisualization";
import { MapVisualizationProvider, MapZoomRequest } from "./map/mapVisualizationProvider";
import { buildErrorIdIndex, buildTree, collectFilterAttributes, FieldFilters, filterItems } from "./tree/treeNode";
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
}

export const XtfErrorVisualization: FC<XtfErrorVisualizationProps> = ({ config }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const theme = useTheme();
  const [messageQuery, setMessageQuery] = useState("");
  const [fieldFilters, setFieldFilters] = useState<FieldFilters>({});
  const [{ selectedNodeId, selectionToken }, setSelectedNodeId] = useReducer<
    Reducer<NodeSelectionState, string | null>
  >(
    (state, nodeId) => ({
      selectedNodeId: nodeId,
      selectionToken: state.selectionToken + 1, // automatically increment the token every time setSelectedNodeId is called, so the tree can scroll to the new selection
    }),
    { selectedNodeId: null, selectionToken: 0 },
  );
  const [fullscreen, setFullscreen] = useState(false);
  const [zoomRequest, setZoomRequest] = useState<MapZoomRequest | null>(null);

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
    () => new Set((config.map?.layers ?? []).flatMap(layer => layer.features?.map(feature => feature.errorId) ?? [])),
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
      setSelectedNodeId(nodeId);
      const featureIds = errorIdsByNodeId.get(nodeId) ?? [];
      setZoomRequest(prev => ({ featureIds, token: (prev?.token ?? 0) + 1 }));
    },
    [errorIdsByNodeId],
  );

  const filter = config.tree && (
    <FilterBar
      attributes={attributes}
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
      {config.map && (
        <IconButton
          size="small"
          label="fullscreen"
          icon={<OpenInFullIcon />}
          sx={{
            position: "absolute",
            top: 0,
            right: theme.spacing(6),
            padding: theme.spacing(2),
            display: { xs: "none", md: "block" },
          }}
          onClick={() => setFullscreen(prev => !prev)}
        />
      )}
      {config.map && fullscreen ? (
        <Modal open onClose={() => setFullscreen(false)} sx={{ padding: 4 }}>
          <ScrollMarginProvider scrollMarginTop="0px" scrollMarginBottom="0px">
            <Box {...stopStepSwipePropagation} sx={{ width: "100%", height: "100%", position: "relative" }}>
              {map}
              {config.tree && (
                <GeopilotBox
                  sx={{
                    position: "absolute",
                    top: 0,
                    left: 0,
                    m: 2,
                    width: "400px",
                    maxHeight: `calc(100% - ${theme.spacing(4)})`,
                  }}>
                  {filter}
                  {tree}
                </GeopilotBox>
              )}
            </Box>
          </ScrollMarginProvider>
        </Modal>
      ) : (
        <Stack sx={{ width: "100%" }}>
          {filter}
          {map}
          {tree}
        </Stack>
      )}
    </MapVisualizationProvider>
  );
};
