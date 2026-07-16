import { useEffect, useRef, useState } from "react";
import LayersOutlinedIcon from "@mui/icons-material/LayersOutlined";
import UnfoldLessIcon from "@mui/icons-material/UnfoldLess";
import UnfoldMoreIcon from "@mui/icons-material/UnfoldMore";
import { Stack } from "@mui/material";
import Collection from "ol/Collection";
import { EventsKey } from "ol/events";
import BaseLayer from "ol/layer/Base";
import LayerGroup from "ol/layer/Group";
import OlMap from "ol/Map";
import { ObjectEvent } from "ol/Object";
import { unByKey } from "ol/Observable";
import { useForceUpdate } from "../../../hooks/useForceUpdate";
import { IconButton } from "../../buttons";
import { SearchField } from "../../searchField";
import { LayerSwitcherCollection } from "./layerSwitcherCollection";
import { getOpen, getTitle, LayerSwitcherProperties } from "./layerSwitcherProps";

const { OPEN, RESULT } = LayerSwitcherProperties;

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

interface LayerSwitcherProps {
  /** The OpenLayers map whose layer tree is managed. Null while the map is still initializing. */
  map: OlMap | null;
  /** Called after any user-driven change (visibility, opacity, order, removal) so callers can persist state. */
  onLayerChange?: () => void;
}

/**
 * A layer switcher overlay for the OpenLayers map. Shows the full layer tree with expand/collapse,
 * visibility (with group cascade), opacity, zoom-to-extent, search and drag-to-reorder.
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
            <LayerSwitcherCollection
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
