import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import AddIcon from "@mui/icons-material/Add";
import DragIndicatorIcon from "@mui/icons-material/DragIndicator";
import RemoveIcon from "@mui/icons-material/Remove";
import ZoomOutMapIcon from "@mui/icons-material/ZoomOutMap";
import { Box, Slider, Stack } from "@mui/material";
import { useTheme } from "@mui/system";
import { isEmpty } from "ol/extent";
import BaseLayer from "ol/layer/Base";
import BaseVectorLayer from "ol/layer/BaseVector";
import LayerGroup from "ol/layer/Group";
import OlMap from "ol/Map";
import { ObjectEvent } from "ol/Object";
import { unByKey } from "ol/Observable";
import { IconButton } from "../../buttons";
import { FormCheckbox } from "../../form/formCheckbox";
import { LayerSwitcherCollection } from "./layerSwitcherCollection";
import { getDisplayed, getOpen, getTitle, LayerSwitcherProperties } from "./layerSwitcherProps";

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

interface LayerRowProps {
  layer: BaseLayer;
  map: OlMap;
  rootLayers: BaseLayer[];
  onLayerChange?: () => void;
  isFirst?: boolean;
}

export const LayerSwitcherRow = ({ layer, map, rootLayers, onLayerChange, isFirst }: LayerRowProps) => {
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
      else if (event.key === LayerSwitcherProperties.TITLE) setTitle(getTitle(layer));
      else if (event.key === LayerSwitcherProperties.OPEN) setOpen(getOpen(layer));
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
        mb: isFirst ? 0 : 0.5,
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
                onClick={() => layer.set(LayerSwitcherProperties.OPEN, !open)}
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
            <LayerSwitcherCollection
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
