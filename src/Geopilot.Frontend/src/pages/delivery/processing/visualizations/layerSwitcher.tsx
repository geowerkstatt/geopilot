import { DragEvent, useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import LayersOutlinedIcon from "@mui/icons-material/LayersOutlined";
import RemoveIcon from "@mui/icons-material/Remove";
import SearchIcon from "@mui/icons-material/Search";
import UnfoldLessIcon from "@mui/icons-material/UnfoldLess";
import ZoomOutMapIcon from "@mui/icons-material/ZoomOutMap";
import { Box, Checkbox, IconButton, InputBase, Paper, Slider, Tooltip, Typography } from "@mui/material";
import Collection from "ol/Collection";
import { isEmpty } from "ol/extent";
import BaseLayer from "ol/layer/Base";
import BaseVectorLayer from "ol/layer/BaseVector";
import LayerGroup from "ol/layer/Group";
import OlMap from "ol/Map";
import { ObjectEvent } from "ol/Object";
import { unByKey } from "ol/Observable";
import { getUid } from "ol/util";

// Custom layer properties read/written by the switcher. The map sets at least TITLE on the layers it
// adds so they have a label here. The others default sensibly when absent, so existing layers work
// without any extra setup:
//   TITLE     – display name shown in the list.
//   DISPLAY   – set to false to hide a layer from the switcher entirely (still rendered on the map).
//   REMOVABLE – set to true to surface a remove (trash) button for the layer.
//   OPEN      – expanded/collapsed state of a group; toggled by the expand button.
//   RESULT    – internal: set by the search filter to mark whether a layer matches the query.
export const LayerSwitcherProperties = {
  TITLE: "title",
  DISPLAY: "displayInLayerSwitcher",
  REMOVABLE: "removableInLayerSwitcher",
  OPEN: "openInLayerSwitcher",
  RESULT: "searchResultInLayerSwitcher",
} as const;

const { TITLE, DISPLAY, REMOVABLE, OPEN, RESULT } = LayerSwitcherProperties;

const getTitle = (layer: BaseLayer): string => layer.get(TITLE) ?? "";
const getRemovable = (layer: BaseLayer): boolean => layer.get(REMOVABLE) ?? false;
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

interface LayerRowProps {
  layer: BaseLayer;
  map: OlMap;
  rootLayers: BaseLayer[];
  onLayerChange?: () => void;
  remove: () => void;
}

const LayerRow = ({ layer, map, rootLayers, onLayerChange, remove }: LayerRowProps) => {
  const { t } = useTranslation();
  const isGroup = layer instanceof LayerGroup;
  const [visible, setVisible] = useState(layer.getVisible());
  const [opacity, setOpacity] = useState(layer.getOpacity());
  const [title, setTitle] = useState(getTitle(layer));
  const [removable, setRemovable] = useState(getRemovable(layer));
  const [open, setOpen] = useState(getOpen(layer));

  useEffect(() => {
    const key = layer.on("propertychange", (event: ObjectEvent) => {
      if (event.key === "visible") setVisible(layer.getVisible());
      else if (event.key === "opacity") setOpacity(layer.getOpacity());
      else if (event.key === TITLE) setTitle(getTitle(layer));
      else if (event.key === REMOVABLE) setRemovable(getRemovable(layer));
      else if (event.key === OPEN) setOpen(getOpen(layer));
    });
    // Sync to the current state in case a property changed between render and effect.
    setVisible(layer.getVisible());
    setOpacity(layer.getOpacity());
    setTitle(getTitle(layer));
    setRemovable(getRemovable(layer));
    setOpen(getOpen(layer));
    return () => unByKey(key);
  }, [layer]);

  const iconButtonSx = { color: "text.secondary", "&:hover": { color: "text.primary" } };

  return (
    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: "1.5em 1fr",
        gridTemplateAreas: `"sidebar header" "sidebar controls" "sidebar content"`,
        overflow: "hidden",
        borderTop: theme => `2px solid ${theme.palette.action.hover}`,
      }}>
      <Box
        data-drag-handle
        draggable
        sx={{
          gridArea: "sidebar",
          display: "grid",
          placeItems: "center",
          cursor: "grab",
          fontSize: "0.8em",
          backgroundColor: "action.hover",
          color: "text.disabled",
        }}>
        <DragIndicatorIcon fontSize="inherit" titleAccess={t("dragToReorderLayer")} />
      </Box>

      <Box sx={{ gridArea: "header", display: "flex", gap: 0.5, px: 0.5, alignItems: "center", overflow: "hidden" }}>
        <Checkbox
          size="small"
          checked={visible}
          data-cy="layer-visibility"
          onChange={event => {
            layer.setVisible(event.target.checked);
            updateSublayerVisibility(layer);
            updateParentLayerVisibility(rootLayers, layer);
            onLayerChange?.();
          }}
          sx={{ p: 0.25 }}
        />
        <Typography
          variant="body2"
          title={title}
          data-cy="layer-title"
          sx={{ flex: 1, overflow: "hidden", whiteSpace: "nowrap", textOverflow: "ellipsis" }}>
          {title}
        </Typography>
        {isGroup && (
          <Tooltip title={open ? t("collapseLayerGroup") : t("expandLayerGroup")}>
            <IconButton size="small" data-cy="expand-layers" onClick={() => layer.set(OPEN, !open)} sx={iconButtonSx}>
              {open ? <RemoveIcon fontSize="small" /> : <AddIcon fontSize="small" />}
            </IconButton>
          </Tooltip>
        )}
      </Box>

      <Box sx={{ gridArea: "controls", display: "flex", gap: 0.5, px: 0.5, alignItems: "center" }}>
        <Slider
          size="small"
          min={0}
          max={1}
          step={0.01}
          value={opacity}
          data-cy="opacity-slider"
          aria-label={t("layerOpacity")}
          onChange={(_event, value) => {
            layer.setOpacity(value as number);
            onLayerChange?.();
          }}
          // Fixed width so every row's opacity slider lines up regardless of which action buttons follow.
          sx={{ width: "75%", flexShrink: 0, mx: 1 }}
        />
        <Box sx={{ flex: 1 }} />
        {layer instanceof BaseVectorLayer && (
          <Tooltip title={t("zoomToLayerExtent")}>
            <IconButton
              size="small"
              data-cy="zoom-to-extent"
              sx={iconButtonSx}
              onClick={() => {
                const extent = layer.getSource()?.getExtent();
                if (extent && !isEmpty(extent)) {
                  map.getView().fit(extent, { padding: [50, 50, 50, 50], maxZoom: 18 });
                }
              }}>
              <ZoomOutMapIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
        {removable && (
          <Tooltip title={t("removeLayer")}>
            <IconButton
              size="small"
              data-cy="layer-remove"
              onClick={remove}
              sx={{ color: "text.secondary", "&:hover": { color: "error.main" } }}>
              <DeleteOutlineIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      </Box>

      {open && isGroup && (
        <Box sx={{ gridArea: "content" }}>
          <LayerCollection
            collection={layer.getLayers()}
            map={map}
            rootLayers={rootLayers}
            onLayerChange={onLayerChange}
            indent
          />
        </Box>
      )}
    </Box>
  );
};

interface LayerCollectionProps {
  collection: Collection<BaseLayer>;
  map: OlMap;
  rootLayers: BaseLayer[];
  onLayerChange?: () => void;
  /** Indent the list. Used for nested groups; the root list stays flush with the header. */
  indent?: boolean;
}

const LayerCollection = ({ collection, map, rootLayers, onLayerChange, indent = false }: LayerCollectionProps) => {
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
    <Box
      // column-reverse so the topmost map layer (last in the collection) appears at the top of the list.
      sx={{ display: "flex", flexDirection: "column-reverse", ml: indent ? 0.5 : 0 }}
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
      {array.filter(getDisplayed).map(layer => {
        const isDragOver = dragOverLayer === layer;
        const draggedBelow = draggedLayer != null && array.indexOf(layer) < array.indexOf(draggedLayer);
        return (
          <Box
            key={getUid(layer)}
            data-cy="layer"
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
              remove={() => {
                collection.remove(layer);
                onLayerChange?.();
              }}
            />
          </Box>
        );
      })}
    </Box>
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
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [searchValue, setSearchValue] = useState("");
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

  if (!map) return null;

  const rootCollection = map.getLayers();
  const rootLayers = rootCollection.getArray();

  const applyFilter = (value: string) => {
    setSearchValue(value);
    filterLayers(value, rootLayers);
  };

  return (
    <Box
      ref={containerRef}
      sx={{
        position: "absolute",
        bottom: 8,
        right: 8,
        zIndex: 10,
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-end",
        gap: 0.5,
      }}>
      {!open && (
        <Tooltip title={t("layers")}>
          <IconButton
            data-cy="layer-switcher-toggle"
            onClick={() => setOpen(true)}
            sx={{
              backgroundColor: "background.paper",
              color: "text.secondary",
              boxShadow: 1,
              borderRadius: "4px",
              "&:hover": { backgroundColor: "background.paper", color: "text.primary" },
              // Keep the fill on focus/active: the theme globally clears the IconButton background on these states.
              "&:focus, &:focus-visible, &.Mui-focusVisible, &:active": { backgroundColor: "background.paper" },
            }}>
            <LayersOutlinedIcon />
          </IconButton>
        </Tooltip>
      )}

      {open && (
        <Paper
          data-cy="layer-switcher"
          elevation={3}
          sx={{ width: "20em", maxHeight: 360, display: "flex", flexDirection: "column", p: 0.5 }}>
          <Box sx={{ display: "flex", gap: 0.5, alignItems: "center", mb: 0.5 }}>
            <Box
              sx={{
                display: "flex",
                alignItems: "center",
                gap: 0.5,
                flex: 1,
                px: 0.5,
                border: theme => `2px solid ${searchValue ? theme.palette.primary.main : theme.palette.action.hover}`,
                borderRadius: "4px",
              }}>
              <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
              <InputBase
                data-cy="layer-search-input"
                placeholder={t("searchLayers")}
                value={searchValue}
                onChange={event => applyFilter(event.target.value)}
                sx={{ flex: 1, fontSize: "0.875rem" }}
              />
              <IconButton
                size="small"
                data-cy="layer-search-clear"
                aria-label={t("clearSearch")}
                onClick={() => applyFilter("")}
                sx={{
                  visibility: searchValue ? "visible" : "hidden",
                  color: "text.secondary",
                  "&:hover": { color: "error.main" },
                }}>
                <CloseIcon fontSize="small" />
              </IconButton>
            </Box>
            <Tooltip title={t("collapseAllLayers")}>
              <IconButton
                size="small"
                data-cy="collapse-all-layers"
                onClick={() => collapseAll(rootLayers)}
                sx={{ color: "text.secondary", "&:hover": { color: "text.primary" } }}>
                <UnfoldLessIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          </Box>
          <Box sx={{ overflowY: "auto" }}>
            <LayerCollection
              collection={rootCollection}
              map={map}
              rootLayers={rootLayers}
              onLayerChange={onLayerChange}
            />
          </Box>
        </Paper>
      )}
    </Box>
  );
};
