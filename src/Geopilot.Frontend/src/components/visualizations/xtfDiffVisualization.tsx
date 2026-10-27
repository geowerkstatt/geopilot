import { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import OpenInFullIcon from "@mui/icons-material/OpenInFull";
import { Box, Modal, Stack, Typography, useMediaQuery, useTheme } from "@mui/material";
import { DiffChange, MapLayer, MapVisualizationConfig, TableVisualizationConfig } from "../../api/apiInterfaces";
import { px2rem } from "../../appTheme";
import { stopStepSwipePropagation } from "../../hooks/useStepSwipe";
import { IconButton } from "../buttons";
import { ScrollMarginContext } from "../scrollMargin/ScrollMarginContext";
import { ScrollMarginProvider } from "../scrollMargin/ScrollMarginProvider";
import { GeopilotBox } from "../styledComponents";
import { ReferenceTarget } from "./diff/diffChangeRow";
import { DiffMapControls, DiffMapSlider } from "./diff/diffMapControls";
import { DiffMapFocus, getChangeExtent, getFocus, inferObjectTypes } from "./diff/diffMapGeometry";
import { DiffMapLayers } from "./diff/diffMapLayers";
import { DiffObjectList } from "./diff/diffObjectList";
import {
  ChangeLabeler,
  DIFF_CHANGE_TYPE_LABEL_KEYS,
  DIFF_OBJECT_LABEL_KEYS,
  DIFF_VALUE_TYPE_LABEL_KEYS,
  DiffFilterField,
  filterObjects,
  filterOptions,
  getClassName,
  groupByObject,
  summarize,
  SUMMARY_PREFIXES,
} from "./diff/diffObjects";
import { DiffSide } from "./diff/diffValue";
import { FieldFilters, FilterBar } from "./filterBar";
import { MapVisualization } from "./map/mapVisualization";
import { MapVisualizationProvider, MapZoomRequest } from "./map/mapVisualizationProvider";

/**
 * Mirrors the backend XtfDiffVisualizationConfig. List and map correlate by the object id, which is also the id of
 * the object's features.
 */
export interface XtfDiffVisualizationConfig {
  /** The map of the changes with a location; absent when the map is not included or no change has a location. */
  map?: MapVisualizationConfig;
  /** The changes for the list and its filter; absent when not included. */
  table?: TableVisualizationConfig;
}

interface XtfDiffVisualizationProps {
  config: XtfDiffVisualizationConfig;
}

/** The visible height of the nearest ancestor that scrolls vertically, or of the window. */
const getScrollPortHeight = (element: HTMLElement): number => {
  for (let parent = element.parentElement; parent; parent = parent.parentElement) {
    const { overflowY } = getComputedStyle(parent);
    if ((overflowY === "auto" || overflowY === "scroll") && parent.scrollHeight > parent.clientHeight) {
      return parent.clientHeight;
    }
  }
  return window.innerHeight;
};

/**
 * Scrolls the whole element into view where it fits beside its scroll margins (e.g. the page header), otherwise its
 * start, so a tall object shows its class and first changes.
 */
const scrollIntoFullView = (element: HTMLElement) => {
  const { scrollMarginTop, scrollMarginBottom } = getComputedStyle(element);
  const room = getScrollPortHeight(element) - parseFloat(scrollMarginTop) - parseFloat(scrollMarginBottom);
  element.scrollIntoView({ block: element.offsetHeight <= room ? "nearest" : "start", behavior: "smooth" });
};

/** A jump to an object; the token makes a jump to the same object again scroll and focus it again. */
interface RevealRequest {
  objectId: string;
  token: number;
}

export const XtfDiffVisualization: FC<XtfDiffVisualizationProps> = ({ config }) => {
  const { t } = useTranslation();
  const { scrollMarginTop, scrollMarginBottom } = useContext(ScrollMarginContext);
  const theme = useTheme();
  const isPhone = useMediaQuery(theme.breakpoints.down("sm"));
  const [fullscreen, setFullscreen] = useState(false);
  const [query, setQuery] = useState("");
  const [fieldFilters, setFieldFilters] = useState<FieldFilters<DiffFilterField>>({});
  const [expandedObjectIds, setExpandedObjectIds] = useState<ReadonlySet<string>>(() => new Set());
  const [selectedObjectId, setSelectedObjectId] = useState<string | null>(null);
  const [hoveredObjectIds, setHoveredObjectIds] = useState<ReadonlySet<string>>(() => new Set());
  const [zoomRequest, setZoomRequest] = useState<MapZoomRequest | null>(null);
  const [revealRequest, setRevealRequest] = useState<RevealRequest | null>(null);
  const [focus, setFocus] = useState<DiffMapFocus | null>(null);
  const [slider, setSlider] = useState<number | null>(null);
  const groupElements = useRef(new Map<string, HTMLDivElement>());
  const inlineMap = useRef<HTMLDivElement>(null);

  const changes = useMemo(() => config.table?.changes ?? [], [config.table]);
  const mappedObjectIds = useMemo(
    () => new Set((config.map?.layers ?? []).flatMap(layer => layer.features?.map(feature => feature.id) ?? [])),
    [config.map],
  );
  const objects = useMemo(() => groupByObject(changes, mappedObjectIds), [changes, mappedObjectIds]);
  const objectsById = useMemo(() => new Map(objects.map(object => [object.objectId, object])), [objects]);
  const objectTypes = useMemo(() => {
    if (config.table) return new Map(objects.map(object => [object.objectId, object.kind]));
    const idsOf = (style: MapLayer["style"]) =>
      (config.map?.layers ?? [])
        .filter(layer => layer.style === style)
        .flatMap(layer => layer.features ?? [])
        .map(feature => feature.id);
    return inferObjectTypes(idsOf("old"), idsOf("new"));
  }, [config.table, config.map, objects]);

  const label = useCallback<ChangeLabeler>(
    (field: DiffFilterField, change: DiffChange) => {
      switch (field) {
        case "changeType":
          return t(DIFF_CHANGE_TYPE_LABEL_KEYS[change.changeType]);
        case "valueType":
          return t(DIFF_VALUE_TYPE_LABEL_KEYS[change.valueType]);
        case "class":
          return getClassName(change.interlisName);
        case "attribute":
          return change.attributePath;
      }
    },
    [t],
  );
  const attributes = useMemo(() => filterOptions(objects, label), [objects, label]);
  const hasActiveFilters = query.trim().length > 0 || Object.values(fieldFilters).some(values => values.length > 0);
  const filteredObjects = useMemo(
    () => (hasActiveFilters ? filterObjects(objects, query, fieldFilters, label) : objects),
    [hasActiveFilters, objects, query, fieldFilters, label],
  );
  const listedObjectIds = useMemo(() => new Set(filteredObjects.map(object => object.objectId)), [filteredObjects]);

  // Filter result for the map: the objects still listed (undefined = no filter = show all).
  const visibleObjectIds = hasActiveFilters ? listedObjectIds : undefined;
  // An object the filter hides stays selected but shows as selected only once it is visible again.
  const highlightedObjectIds = useMemo<ReadonlySet<string>>(
    () =>
      new Set(
        selectedObjectId && (!visibleObjectIds || visibleObjectIds.has(selectedObjectId)) ? [selectedObjectId] : [],
      ),
    [selectedObjectId, visibleObjectIds],
  );

  useEffect(() => {
    if (!revealRequest) return;
    const element = groupElements.current.get(revealRequest.objectId);
    if (!element) return;
    scrollIntoFullView(element);
    element.focus({ preventScroll: true });
  }, [revealRequest]);

  /** Selects an object and, when it is listed, expands it and scrolls to it with the focus on it. */
  const revealObject = useCallback(
    (objectId: string) => {
      setSelectedObjectId(objectId);
      setFocus(null);
      if (!listedObjectIds.has(objectId)) return;
      setExpandedObjectIds(previous => new Set(previous).add(objectId));
      setRevealRequest(previous => ({ objectId, token: (previous?.token ?? 0) + 1 }));
    },
    [listedObjectIds],
  );

  const lookupReference = useCallback(
    (oid: string): ReferenceTarget => {
      const target = objectsById.get(oid);
      if (!target) return { state: "absent" };
      const className = getClassName(target.interlisName);
      return { state: listedObjectIds.has(oid) ? "listed" : "filtered", className };
    },
    [objectsById, listedObjectIds],
  );

  // Fullscreen shows the map beside the list, so only the inline map may need scrolling to.
  const scrollToInlineMap = useCallback(() => inlineMap.current && scrollIntoFullView(inlineMap.current), []);

  const showObjectOnMap = useCallback(
    (objectId: string) => {
      setSelectedObjectId(objectId);
      setFocus(null);
      setZoomRequest(previous => ({ featureIds: [objectId], token: (previous?.token ?? 0) + 1 }));
      scrollToInlineMap();
    },
    [scrollToInlineMap],
  );

  const showChangeOnMap = useCallback(
    (change: DiffChange, side: DiffSide) => {
      setSelectedObjectId(change.objectId);
      setFocus(getFocus(change, side));
      setZoomRequest(previous => ({
        featureIds: [change.objectId],
        extent: getChangeExtent(change, side),
        token: (previous?.token ?? 0) + 1,
      }));
      scrollToInlineMap();
    },
    [scrollToInlineMap],
  );

  const clearSelection = useCallback(() => {
    setSelectedObjectId(null);
    setFocus(null);
  }, []);

  const toggleObject = useCallback(
    (objectId: string) =>
      setExpandedObjectIds(previous => {
        const next = new Set(previous);
        if (!next.delete(objectId)) next.add(objectId);
        return next;
      }),
    [],
  );
  const expandObjects = useCallback(
    (objectIds: string[], expanded: boolean) =>
      setExpandedObjectIds(previous => {
        const next = new Set(previous);
        for (const objectId of objectIds) {
          if (expanded) next.add(objectId);
          else next.delete(objectId);
        }
        return next;
      }),
    [],
  );

  const registerObject = useCallback((objectId: string, element: HTMLDivElement | null) => {
    if (element) groupElements.current.set(objectId, element);
    else groupElements.current.delete(objectId);
  }, []);

  const hoverFeatures = useCallback((objectIds: string[]) => setHoveredObjectIds(new Set(objectIds)), []);
  const hoverObject = useCallback(
    (objectId: string | null) => hoverFeatures(objectId ? [objectId] : []),
    [hoverFeatures],
  );

  // The popup on the map names the class of the object and below it what changed. Without the list there are no
  // changes, and the popup falls back to the info of the feature, the qualified name of the class.
  const describeObject = useCallback(
    (objectId: string) => {
      const object = objectsById.get(objectId);
      if (!object) return undefined;
      return {
        text: getClassName(object.interlisName),
        secondaryText:
          object.kind === "changed"
            ? summarize(object)
                .map(item => `${SUMMARY_PREFIXES[item.changeType]}${item.path}`)
                .join(", ")
            : t(DIFF_OBJECT_LABEL_KEYS[object.kind]),
      };
    },
    [objectsById, t],
  );

  const changeFilters = (field: DiffFilterField, selected: string[]) =>
    setFieldFilters(current => ({ ...current, [field]: selected }));
  const clearFilters = () => {
    setQuery("");
    setFieldFilters({});
  };

  if (config.table && changes.length === 0) {
    return <Typography data-cy="diff-no-changes">{t("diffNoChanges")}</Typography>;
  }
  // Only the map is included, but it has nothing to show: no change has a location, or there is no change at all.
  if (!config.table && !config.map) {
    return <Typography data-cy="diff-no-locations">{t("diffNoLocations")}</Typography>;
  }

  const layout = fullscreen || isPhone ? "narrow" : "wide";
  const filter = config.table && (
    <FilterBar
      attributes={attributes}
      searchPlaceholder="diffSearch"
      messageQuery={query}
      onMessageQueryChange={setQuery}
      fieldFilters={fieldFilters}
      onFieldFilterChange={changeFilters}
      onClearFilters={clearFilters}
      forceMobileView={fullscreen}
    />
  );
  const map = config.map && (
    <MapVisualization
      config={config.map}
      reserveSpaceForFilters={!!config.table}
      fullscreen={fullscreen}
      setFullscreen={setFullscreen}
      controls={<DiffMapControls slider={slider} onSliderChange={setSlider} />}>
      {slider !== null && <DiffMapSlider value={slider} onChange={setSlider} />}
    </MapVisualization>
  );
  const list = config.table && (
    <DiffObjectList
      objects={filteredObjects}
      totalChangeCount={changes.length}
      filterActive={hasActiveFilters}
      expandedObjectIds={expandedObjectIds}
      onToggleObject={toggleObject}
      onExpandObjects={expandObjects}
      hoveredObjectIds={hoveredObjectIds}
      onHoverObject={hoverObject}
      onShowObjectOnMap={showObjectOnMap}
      onShowChangeOnMap={showChangeOnMap}
      lookupReference={lookupReference}
      onReference={revealObject}
      registerObject={registerObject}
      layout={layout}
    />
  );

  return (
    <MapVisualizationProvider
      config={config.map}
      visibleFeatureIds={visibleObjectIds}
      highlightedFeatureIds={highlightedObjectIds}
      hoveredFeatureIds={hoveredObjectIds}
      zoomRequest={zoomRequest}
      onSelectFeature={revealObject}
      onClearSelection={clearSelection}
      onHoverFeatures={hoverFeatures}
      showMapSelectionPopup
      describeFeature={describeObject}
      fullscreen={fullscreen}>
      {config.map && (
        <DiffMapLayers
          changes={changes}
          objectTypes={objectTypes}
          visibleIds={visibleObjectIds}
          highlightedIds={highlightedObjectIds}
          hoveredIds={hoveredObjectIds}
          focus={focus}
          slider={slider}
        />
      )}
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
        <ScrollMarginProvider scrollMarginTop="0px" scrollMarginBottom="0px">
          <Modal open onClose={() => setFullscreen(false)} sx={{ padding: 4 }}>
            <Box {...stopStepSwipePropagation} sx={{ width: "100%", height: "100%", position: "relative" }}>
              {map}
              {/* Beside the map: the filter and the list, scrolling together when they outgrow it. */}
              {config.table && (
                <GeopilotBox
                  sx={{
                    position: "absolute",
                    top: 0,
                    left: 0,
                    m: 2,
                    gap: 2,
                    width: px2rem(440),
                    maxHeight: `calc(100% - ${theme.spacing(4)})`,
                    overflowY: "auto",
                  }}>
                  {filter}
                  {list}
                </GeopilotBox>
              )}
            </Box>
          </Modal>
        </ScrollMarginProvider>
      ) : (
        <Stack sx={{ width: "100%", gap: 2 }}>
          {filter}
          <Box ref={inlineMap} sx={{ scrollMarginTop, scrollMarginBottom }}>
            {map}
          </Box>
          {list}
        </Stack>
      )}
    </MapVisualizationProvider>
  );
};
