import { DragEvent, useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import LayersOutlinedIcon from "@mui/icons-material/LayersOutlined";
import RemoveIcon from "@mui/icons-material/Remove";
import UnfoldLessIcon from "@mui/icons-material/UnfoldLess";
import UnfoldMoreIcon from "@mui/icons-material/UnfoldMore";
import ZoomOutMapIcon from "@mui/icons-material/ZoomOutMap";
import { Box, Slider, Stack } from "@mui/material";
import { useTheme } from "@mui/system";
import Collection from "ol/Collection";
import { EventsKey } from "ol/events";
import { isEmpty } from "ol/extent";
import BaseLayer from "ol/layer/Base";
import BaseVectorLayer from "ol/layer/BaseVector";
import LayerGroup from "ol/layer/Group";
import OlMap from "ol/Map";
import { ObjectEvent } from "ol/Object";
import { unByKey } from "ol/Observable";
import { getUid } from "ol/util";
import { IconButton } from "../../buttons";
import { FormCheckbox } from "../../form/formCheckbox";
import { SearchField } from "../../searchField";

// Custom layer properties read/written by the switcher. The map sets at least TITLE on the layers it
// adds so they have a label here. The others default sensibly when absent, so existing layers work
// without any extra setup:
//   TITLE     – display name shown in the list.
//   DISPLAY   – set to false to hide a layer from the switcher entirely (still rendered on the map).
//   OPEN      – expanded/collapsed state of a group; toggled by the expand button.
//   RESULT    – internal: set by the search filter to mark whether a layer matches the query.
export const LayerSwitcherProperties = {
  TITLE: "title",
  DISPLAY: "displayInLayerSwitcher",
  OPEN: "openInLayerSwitcher",
  RESULT: "searchResultInLayerSwitcher",
} as const;

const { TITLE, DISPLAY, OPEN, RESULT } = LayerSwitcherProperties;

const getTitle = (layer: BaseLayer): string => layer.get(TITLE) ?? "";
const getOpen = (layer: BaseLayer): boolean => layer.get(OPEN) ?? false;
// A layer shows in the switcher when it is not explicitly hidden and (when a search is active) matches it.
const getDisplayed = (layer: BaseLayer): boolean => (layer.get(DISPLAY) ?? true) && (layer.get(RESULT) ?? true);

const useForceUpdate = () => {
  const [, setTick] = useState(0);
  return useCallback(() => setTick(tick => tick + 1), []);
};

// Sets the visibility of every sublayer of a group to match the group itself (respecting hidden layers).
const updateSublayerVisibility = (layer: BaseLayer): void => {
  const visible = layer.getVisible();
  const stack: BaseLayer[] = [layer];
  while (stack.length > 0) {
    const current = stack.pop()!;
    if (current instanceof LayerGroup) {
      for (const subLayer of current.getLayers().getArray()) {
        subLayer.setVisible(visible && getDisplayed(subLayer));
        stack.push(subLayer);
      }
    }
  }
};

// Walks the tree from the root to find the changed layer, then updates its ancestor groups: showing a
// child shows its parents; hiding the last visible child hides the parent group.
const updateParentLayerVisibility = (rootLayers: BaseLayer[], layer: BaseLayer): void => {
  const visible = layer.getVisible();
  const stack: { layer: BaseLayer; path: LayerGroup[] }[] = rootLayers.map(l => ({ layer: l, path: [] }));

  while (stack.length > 0) {
    const { layer: current, path } = stack.pop()!;
    if (current === layer) {
      if (visible) {
        path.forEach(group => group.setVisible(true));
      } else {
        for (let i = path.length - 1; i >= 0; i--) {
          path[i].setVisible(
            path[i]
              .getLayers()
              .getArray()
              .some(subLayer => subLayer.getVisible()),
          );
        }
      }
      return;
    }
    if (current instanceof LayerGroup) {
      current.getLayers().forEach(subLayer => stack.push({ layer: subLayer, path: [...path, current] }));
    }
  }
};

// Filters layers by title using multi-word substring matching. Groups are kept (and auto-expanded) when
// they or any descendant match. Returns whether anything in the passed list matched.
const filterLayers = (filter: string, layers: BaseLayer[]): boolean => {
  const filters = filter
    .toLowerCase()
    .split(" ")
    .filter(part => part !== "");

  let found = false;
  layers.forEach(layer => {
    const title = getTitle(layer).toLowerCase();
    let hit = filters.every(part => title.includes(part));
    if (layer instanceof LayerGroup) {
      // A matching group reveals all its children, so we clear the filter when recursing into it.
      hit = filterLayers(hit ? "" : filter, layer.getLayers().getArray()) || hit;
      layer.set(OPEN, hit && filters.length > 0);
      layer.set(RESULT, hit || filters.length === 0);
    } else {
      layer.set(RESULT, hit);
    }
    found = hit || found;
  });
  return found;
};

// Recursively collapses all groups by clearing their OPEN flag.
const collapseAll = (layers: BaseLayer[]): void => {
  const stack = [...layers];
  while (stack.length > 0) {
    const layer = stack.pop()!;
    if (layer instanceof LayerGroup) {
      layer.unset(OPEN);
      stack.push(...layer.getLayers().getArray());
    }
  }
};

const expandAll = (layers: BaseLayer[]): void => {
  const stack = [...layers];
  while (stack.length > 0) {
    const layer = stack.pop()!;
    if (layer instanceof LayerGroup) {
      layer.set(OPEN, true);
      stack.push(...layer.getLayers().getArray());
    }
  }
};

const containsGroup = (layers: BaseLayer[]): boolean => layers.some(layer => layer instanceof LayerGroup);

const hasOpenGroup = (layers: BaseLayer[]): boolean => {
  const stack = [...layers];
  while (stack.length > 0) {
    const layer = stack.pop()!;
    if (layer instanceof LayerGroup) {
      if (getOpen(layer)) return true;
      stack.push(...layer.getLayers().getArray());
    }
  }
  return false;
};

interface LayerRowProps {
  layer: BaseLayer;
  map: OlMap;
  rootLayers: BaseLayer[];
  onLayerChange?: () => void;
  isFirst?: boolean;
}

const LayerRow = ({ layer, map, rootLayers, onLayerChange, isFirst }: LayerRowProps) => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isGroup = layer instanceof LayerGroup;
  const [visible, setVisible] = useState(layer.getVisible());
  const [opacity, setOpacity] = useState(layer.getOpacity());
  const [title, setTitle] = useState(getTitle(layer));
  const [open, setOpen] = useState(getOpen(layer));

  useEffect(() => {
    const key = layer.on("propertychange", (event: ObjectEvent) => {
      if (event.key === "visible") setVisible(layer.getVisible());
      else if (event.key === "opacity") setOpacity(layer.getOpacity());
      else if (event.key === TITLE) setTitle(getTitle(layer));
      else if (event.key === OPEN) setOpen(getOpen(layer));
    });
    // Sync to the current state in case a property changed between render and effect.
    setVisible(layer.getVisible());
    setOpacity(layer.getOpacity());
    setTitle(getTitle(layer));
    setOpen(getOpen(layer));
    return () => unByKey(key);
  }, [layer]);

  return (
    <Stack
      direction="row"
      sx={{
        gap: 1,
        borderTop: `2px solid ${theme.palette.background.base}`,
        width: "100%",
        minWidth: 0,
        mb: isFirst ? 0 : theme.spacing(0.5),
      }}>
      <Stack
        data-drag-handle
        draggable
        sx={{
          justifyContent: "center",
          alignItems: "center",
          cursor: "grab",
          fontSize: "0.8em",
          backgroundColor: "background.base",
          color: "text.disabled",
          width: "20px",
          flexShrink: 0,
        }}>
        <DragIndicatorIcon fontSize="inherit" titleAccess={t("dragToReorderLayer")} />
      </Stack>
      <Stack sx={{ flex: 1, minWidth: 0, gap: 0.5 }}>
        <Stack direction="row" sx={{ minWidth: 0, ml: 1, alignItems: "center" }}>
          <FormCheckbox
            size="small"
            checked={visible}
            onChange={checked => {
              layer.setVisible(checked);
              updateSublayerVisibility(layer);
              updateParentLayerVisibility(rootLayers, layer);
              onLayerChange?.();
            }}
            label={title}
            truncateLabel
            sx={{ flex: 1, minWidth: 0 }}
          />
          <Box sx={{ width: "30px", flexShrink: 0 }}>
            {isGroup && (
              <IconButton
                size="small"
                icon={open ? <RemoveIcon /> : <AddIcon />}
                label={open ? "collapseLayerGroup" : "expandLayerGroup"}
                sx={{ flexShrink: 0 }}
                onClick={() => layer.set(OPEN, !open)}
              />
            )}
            {layer instanceof BaseVectorLayer && (
              <IconButton
                size="small"
                icon={<ZoomOutMapIcon />}
                label="zoomToLayerExtent"
                sx={{ flexShrink: 0 }}
                onClick={() => {
                  const extent = layer.getSource()?.getExtent();
                  if (extent && !isEmpty(extent)) {
                    map.getView().fit(extent, { padding: [50, 50, 50, 50], maxZoom: 18 });
                  }
                }}
              />
            )}
          </Box>
        </Stack>
        <Stack direction="row" sx={{ minWidth: 0, ml: 1 }}>
          <Slider
            size="small"
            min={0}
            max={1}
            step={0.01}
            value={opacity}
            aria-label={t("layerOpacity")}
            onChange={(_event, value) => {
              layer.setOpacity(value as number);
              onLayerChange?.();
            }}
            sx={{ flex: 1, minWidth: 0 }}
          />
          <Box sx={{ width: "30px", flexShrink: 0 }} />
        </Stack>
        {open && isGroup && (
          <Box>
            <LayerCollection
              collection={layer.getLayers()}
              map={map}
              rootLayers={rootLayers}
              onLayerChange={onLayerChange}
            />
          </Box>
        )}
      </Stack>
    </Stack>
  );
};

interface LayerCollectionProps {
  collection: Collection<BaseLayer>;
  map: OlMap;
  rootLayers: BaseLayer[];
  onLayerChange?: () => void;
}

const LayerCollection = ({ collection, map, rootLayers, onLayerChange }: LayerCollectionProps) => {
  const forceUpdate = useForceUpdate();
  const [draggedLayer, setDraggedLayer] = useState<BaseLayer | null>(null);
  const [dragOverLayer, setDragOverLayer] = useState<BaseLayer | null>(null);

  useEffect(() => {
    // Re-render when a layer's switcher visibility (DISPLAY/RESULT, e.g. via search) changes, or when
    // layers are added to / removed from this collection.
    const onPropertyChange = (event: ObjectEvent) => {
      if (event.key === DISPLAY || event.key === RESULT) forceUpdate();
    };
    const layerKeys = new Map(
      collection.getArray().map(layer => [layer, layer.on("propertychange", onPropertyChange)]),
    );
    const collectionKeys = [
      collection.on("add", event => {
        forceUpdate();
        const layer = event.element as BaseLayer;
        layerKeys.set(layer, layer.on("propertychange", onPropertyChange));
      }),
      collection.on("remove", event => {
        forceUpdate();
        const layer = event.element as BaseLayer;
        const key = layerKeys.get(layer);
        if (key) unByKey(key);
        layerKeys.delete(layer);
      }),
    ];
    forceUpdate();
    return () => unByKey([...layerKeys.values(), ...collectionKeys]);
  }, [collection, forceUpdate]);

  const handleDrop = (event: DragEvent) => {
    event.preventDefault();
    if (!draggedLayer || !dragOverLayer) return;
    const array = collection.getArray();
    const fromIndex = array.indexOf(draggedLayer);
    const toIndex = array.indexOf(dragOverLayer);
    setDraggedLayer(null);
    setDragOverLayer(null);
    if (fromIndex === -1 || toIndex === -1) return;
    collection.removeAt(fromIndex);
    collection.insertAt(toIndex, draggedLayer);
    onLayerChange?.();
  };

  const array = collection.getArray();

  return (
    <Stack
      direction="column-reverse" // column-reverse so the topmost map layer (last in the collection) appears at the top of the list.
      sx={{ width: "100%", gap: 0.25 }}
      onDragOver={event => {
        if (draggedLayer && dragOverLayer) {
          event.preventDefault();
          event.stopPropagation();
        }
      }}
      onDragLeave={event => {
        if (!(event.currentTarget as HTMLElement).contains(event.relatedTarget as Node)) setDragOverLayer(null);
      }}
      onDrop={handleDrop}
      onDragEnd={() => {
        setDraggedLayer(null);
        setDragOverLayer(null);
      }}>
      {array.filter(getDisplayed).map((layer, index) => {
        const isDragOver = dragOverLayer === layer;
        const draggedBelow = draggedLayer != null && array.indexOf(layer) < array.indexOf(draggedLayer);
        return (
          <Box
            key={getUid(layer)}
            draggable
            sx={{
              transition: "margin 100ms ease",
              ...(draggedLayer === layer && { opacity: 0.4 }),
              ...(isDragOver && (draggedBelow ? { mb: "2em" } : { mt: "2em" })),
            }}
            onDragStart={event => {
              if (!(event.target as HTMLElement).closest("[data-drag-handle]")) {
                event.preventDefault();
                return;
              }
              event.stopPropagation();
              setDraggedLayer(layer);
              const target = event.currentTarget as HTMLElement;
              const rect = target.getBoundingClientRect();
              event.dataTransfer.setDragImage(target, event.clientX - rect.left, event.clientY - rect.top);
              event.dataTransfer.effectAllowed = "move";
            }}
            onDragOver={event => {
              if (!draggedLayer) return;
              event.stopPropagation();
              if (draggedLayer === layer) {
                setDragOverLayer(null);
              } else {
                event.preventDefault();
                setDragOverLayer(layer);
              }
            }}>
            <LayerRow
              layer={layer}
              map={map}
              rootLayers={rootLayers}
              onLayerChange={onLayerChange}
              isFirst={index === 0}
            />
          </Box>
        );
      })}
    </Stack>
  );
};

interface LayerSwitcherProps {
  /** The OpenLayers map whose layer tree is managed. Null while the map is still initializing. */
  map: OlMap | null;
  /** Called after any user-driven change (visibility, opacity, order, removal) so callers can persist state. */
  onLayerChange?: () => void;
}

/**
 * A layer switcher overlay for the OpenLayers map. Shows the full layer tree with expand/collapse,
 * visibility (with group cascade), opacity, zoom-to-extent, remove, search and drag-to-reorder.
 */
export const LayerSwitcher = ({ map, onLayerChange }: LayerSwitcherProps) => {
  const [open, setOpen] = useState(false);
  const [searchValue, setSearchValue] = useState("");
  const forceUpdate = useForceUpdate();
  const containerRef = useRef<HTMLDivElement>(null);

  // Close the panel when the user clicks anywhere outside it (e.g. on the map).
  useEffect(() => {
    if (!open) return;
    const handlePointerDown = (event: PointerEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("pointerdown", handlePointerDown);
    return () => document.removeEventListener("pointerdown", handlePointerDown);
  }, [open]);

  // Re-render when any group's expanded state changes so the header can switch between the expand-all and collapse-all buttons
  useEffect(() => {
    if (!map) return;
    const keys: EventsKey[] = [];
    const collections: Collection<BaseLayer>[] = [map.getLayers()];
    while (collections.length > 0) {
      const collection = collections.pop()!;
      for (const layer of collection.getArray()) {
        if (layer instanceof LayerGroup) {
          keys.push(
            layer.on("propertychange", (event: ObjectEvent) => {
              if (event.key === OPEN) forceUpdate();
            }),
          );
          collections.push(layer.getLayers());
        }
      }
    }
    return () => unByKey(keys);
  }, [map, forceUpdate]);

  if (!map) return null;

  const rootCollection = map.getLayers();
  const rootLayers = rootCollection.getArray();

  const applyFilter = (value: string) => {
    setSearchValue(value);
    filterLayers(value, rootLayers);
  };

  const groupsExist = containsGroup(rootLayers);
  const anyGroupOpen = hasOpenGroup(rootLayers);

  return (
    <Stack
      sx={{
        position: "absolute",
        bottom: 0,
        right: 0,
        m: 2,
        maxHeight: theme => `calc(100% - ${theme.spacing(4)})`,
      }}>
      {open ? (
        <Stack
          ref={containerRef}
          sx={{
            width: "360px",
            p: 2,
            pb: 0,
            gap: 1,
            overflow: "hidden",
            backgroundColor: "background.content",
            border: theme => `1px solid ${theme.palette.primary.main}`,
            borderRadius: theme => theme.spacing(0.5),
          }}>
          <Stack direction="row" sx={{ gap: 1, alignItems: "center", mb: 1, width: "100%" }}>
            <SearchField placeholder="searchLayers" value={searchValue} onChange={applyFilter} sx={{ flex: 1 }} />
            {groupsExist &&
              (anyGroupOpen ? (
                <IconButton
                  size="small"
                  icon={<UnfoldLessIcon />}
                  label="collapseAll"
                  onClick={() => collapseAll(rootLayers)}
                />
              ) : (
                <IconButton
                  size="small"
                  icon={<UnfoldMoreIcon />}
                  label="expandAll"
                  onClick={() => expandAll(rootLayers)}
                />
              ))}
          </Stack>
          <Stack sx={{ pb: 2, overflowY: "auto" }}>
            <LayerCollection
              collection={rootCollection}
              map={map}
              rootLayers={rootLayers}
              onLayerChange={onLayerChange}
            />
          </Stack>
        </Stack>
      ) : (
        <IconButton
          color={"primaryOutlined"}
          icon={<LayersOutlinedIcon />}
          label="layers"
          onClick={() => setOpen(true)}
          tooltipPlacement="left"
        />
      )}
    </Stack>
  );
};
