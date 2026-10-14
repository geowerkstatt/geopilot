import { FC, useCallback, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import { Box, Modal, Stack, Typography, useTheme } from "@mui/material";
import { gridPaginationModelSelector, gridSortedRowIdsSelector, useGridApiRef } from "@mui/x-data-grid";
import {
  DiffChange,
  DiffChangeType,
  MapLayerStyle,
  MapVisualizationConfig,
  TableVisualizationConfig,
} from "../../api/apiInterfaces";
import { px2rem } from "../../appTheme";
import { stopStepSwipePropagation } from "../../hooks/useStepSwipe";
import { IconButton } from "../buttons";
import { GeopilotBox } from "../styledComponents";
import { filterChanges, getChangeLabelKey, getClassName } from "./diff/diffChanges";
import { DiffDetailPanel } from "./diff/diffDetailPanel";
import { DiffFilterBar } from "./diff/diffFilterBar";
import { DiffTable } from "./diff/diffTable";
import { MapVisualization } from "./map/mapVisualization";
import { MapVisualizationProvider, MapZoomRequest } from "./map/mapVisualizationProvider";

/**
 * The XTF diff visualization: every change of a diff in a table and the changed objects on a map, the old state in
 * orange beneath the new one in blue. Mirrors the backend XtfDiffVisualizationConfig, and like the error
 * visualization it shows the views that are present. This component owns the state the views share: the filter,
 * which comes with the table and filters it and the map, and the selected object and the one under the pointer,
 * which both highlight. They correlate by the object id, which is also the id of the object's features.
 */
export interface XtfDiffVisualizationConfig {
  /** The map of the changes with a location; absent when the map is not included or no change has a location. */
  map?: MapVisualizationConfig;
  /** The table of the changes, with the filter and the detail of the selected object; absent when not included. */
  table?: TableVisualizationConfig;
}

interface XtfDiffVisualizationProps {
  config: XtfDiffVisualizationConfig;
}

export const XtfDiffVisualization: FC<XtfDiffVisualizationProps> = ({ config }) => {
  const { t } = useTranslation();
  const theme = useTheme();
  const apiRef = useGridApiRef();
  const [fullscreen, setFullscreen] = useState(false);
  const [query, setQuery] = useState("");
  const [changeTypes, setChangeTypes] = useState<DiffChangeType[]>([]);
  const [selectedObjectId, setSelectedObjectId] = useState<string | null>(null);
  const [hoveredObjectIds, setHoveredObjectIds] = useState<readonly string[]>([]);
  const [zoomRequest, setZoomRequest] = useState<MapZoomRequest | null>(null);

  const changes = useMemo(() => config.table?.changes ?? [], [config.table]);
  const filteredChanges = useMemo(() => filterChanges(changes, query, changeTypes), [changes, query, changeTypes]);
  const hasActiveFilters = query.trim().length > 0 || changeTypes.length > 0;
  // Filter result for the map: the objects still listed (undefined = no filter = show all).
  const visibleObjectIds = useMemo<ReadonlySet<string> | undefined>(
    () => (hasActiveFilters ? new Set(filteredChanges.map(change => change.objectId)) : undefined),
    [hasActiveFilters, filteredChanges],
  );
  // As in the error visualization, an object the filter hides stays selected but shows neither as selected nor in the
  // detail until it is listed again.
  const shownObjectId =
    selectedObjectId && (!visibleObjectIds || visibleObjectIds.has(selectedObjectId)) ? selectedObjectId : null;
  const highlightedObjectIds = useMemo<ReadonlySet<string>>(
    () => new Set(shownObjectId ? [shownObjectId] : []),
    [shownObjectId],
  );
  // The map draws these lighter than the selection, as the table does with their rows.
  const hoveredFeatureIds = useMemo<ReadonlySet<string>>(() => new Set(hoveredObjectIds), [hoveredObjectIds]);
  // The states each object is drawn in, old before new: the layers of the map carry the style of their state.
  const objectStates = useMemo<ReadonlyMap<string, readonly MapLayerStyle[]>>(() => {
    const states = new Map<string, MapLayerStyle[]>();
    for (const layer of config.map?.layers ?? []) {
      const style = layer.style;
      if (!style) continue;
      for (const feature of layer.features ?? []) {
        states.set(feature.id, [...(states.get(feature.id) ?? []), style]);
      }
    }
    return states;
  }, [config.map]);
  const changesByObjectId = useMemo(() => {
    const grouped = new Map<string, DiffChange[]>();
    for (const change of changes) {
      const objectChanges = grouped.get(change.objectId);
      if (objectChanges) objectChanges.push(change);
      else grouped.set(change.objectId, [change]);
    }
    return grouped;
  }, [changes]);
  const selectedChanges = shownObjectId ? (changesByObjectId.get(shownObjectId) ?? []) : [];

  // The popup on the map names the class of the object and below it its changes. Without the table there are no
  // changes, and the popup falls back to the info of the feature, the qualified name of the class.
  const describeObject = useCallback(
    (objectId: string) => {
      const objectChanges = changesByObjectId.get(objectId);
      if (!objectChanges) return undefined;
      return {
        text: getClassName(objectChanges[0].interlisName),
        secondaryText: objectChanges
          .map(change => {
            const label = t(getChangeLabelKey(change));
            return change.valueType === "object" || !change.attributePath
              ? label
              : `${label} (${change.attributePath})`;
          })
          .join(", "),
      };
    },
    [changesByObjectId, t],
  );

  const handleShowObjectOnMap = useCallback((objectId: string) => {
    setSelectedObjectId(objectId);
    setZoomRequest(previous => ({ featureIds: [objectId], token: (previous?.token ?? 0) + 1 }));
  }, []);

  const handleHoverFeatures = useCallback((objectIds: string[]) => setHoveredObjectIds(objectIds), []);
  const handleHoverObject = useCallback(
    (objectId: string | null) => setHoveredObjectIds(objectId ? [objectId] : []),
    [],
  );

  // A feature clicked on the map selects its object and turns the table to the page of the object's first change.
  const handleSelectFeature = useCallback(
    (objectId: string) => {
      setSelectedObjectId(objectId);
      const api = apiRef.current;
      if (!api) return;
      const index = gridSortedRowIdsSelector(apiRef).findIndex(id => api.getRow<DiffChange>(id)?.objectId === objectId);
      if (index >= 0) api.setPage(Math.floor(index / gridPaginationModelSelector(apiRef).pageSize));
    },
    [apiRef],
  );

  if (config.table && changes.length === 0) {
    return <Typography data-cy="diff-no-changes">{t("diffNoChanges")}</Typography>;
  }
  // Only the map is included, but it has nothing to show: no change has a location, or there is no change at all.
  if (!config.table && !config.map) {
    return <Typography data-cy="diff-no-locations">{t("diffNoLocations")}</Typography>;
  }

  const filter = config.table && (
    <DiffFilterBar
      changes={changes}
      query={query}
      onQueryChange={setQuery}
      changeTypes={changeTypes}
      onChangeTypesChange={setChangeTypes}
    />
  );
  const map = config.map && (
    <MapVisualization
      config={config.map}
      reserveSpaceForFilters={!!config.table}
      fullscreen={fullscreen}
      setFullscreen={setFullscreen}
    />
  );
  const table = config.table && (
    <DiffTable
      apiRef={apiRef}
      changes={filteredChanges}
      selectedObjectId={shownObjectId}
      hoveredObjectIds={hoveredObjectIds}
      objectStates={objectStates}
      onSelectObject={setSelectedObjectId}
      onHoverObject={handleHoverObject}
      onShowObjectOnMap={handleShowObjectOnMap}
      compact={fullscreen}
    />
  );
  const detail = config.table && selectedChanges.length > 0 && (
    <DiffDetailPanel changes={selectedChanges} compact={fullscreen} />
  );

  return (
    <MapVisualizationProvider
      config={config.map}
      visibleFeatureIds={visibleObjectIds}
      highlightedFeatureIds={highlightedObjectIds}
      hoveredFeatureIds={hoveredFeatureIds}
      zoomRequest={zoomRequest}
      onSelectFeature={handleSelectFeature}
      onHoverFeatures={handleHoverFeatures}
      showMapSelectionPopup
      describeFeature={describeObject}
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
          onClick={() => setFullscreen(previous => !previous)}
        />
      )}
      {map && fullscreen ? (
        <Modal open onClose={() => setFullscreen(false)} sx={{ padding: 4 }}>
          <Box {...stopStepSwipePropagation} sx={{ width: "100%", height: "100%", position: "relative" }}>
            {map}
            {/* Beside the map: the filter, the narrow table and the detail, scrolling together when they outgrow it. */}
            {config.table && (
              <GeopilotBox
                sx={{
                  position: "absolute",
                  top: 0,
                  left: 0,
                  m: 2,
                  gap: 2,
                  width: px2rem(400),
                  maxHeight: `calc(100% - ${theme.spacing(4)})`,
                  overflowY: "auto",
                }}>
                {filter}
                {table}
                {detail}
              </GeopilotBox>
            )}
          </Box>
        </Modal>
      ) : (
        <Stack sx={{ width: "100%", gap: 2 }}>
          {filter}
          {map}
          {table}
          {detail}
        </Stack>
      )}
    </MapVisualizationProvider>
  );
};
