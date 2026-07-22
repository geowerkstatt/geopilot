import { DragEvent, useState } from "react";
import { Box, Stack } from "@mui/material";
import Collection from "ol/Collection";
import BaseLayer from "ol/layer/Base";
import OlMap from "ol/Map";
import { getUid } from "ol/util";
import { getDisplayed } from "./layerSwitcherProps";
import { LayerSwitcherRow } from "./layerSwitcherRow";

interface LayerCollectionProps {
  collection: Collection<BaseLayer>;
  map: OlMap;
  rootLayers: BaseLayer[];
}

export const LayerSwitcherCollection = ({ collection, map, rootLayers }: LayerCollectionProps) => {
  const [draggedLayer, setDraggedLayer] = useState<BaseLayer | null>(null);
  const [dragOverLayer, setDragOverLayer] = useState<BaseLayer | null>(null);

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
            <LayerSwitcherRow layer={layer} map={map} rootLayers={rootLayers} isFirst={index === 0} />
          </Box>
        );
      })}
    </Stack>
  );
};
