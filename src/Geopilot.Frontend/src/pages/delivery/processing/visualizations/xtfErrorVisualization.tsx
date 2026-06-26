import { FC } from "react";
import { Stack } from "@mui/material";
import { MapVisualizationConfig } from "../../../../api/apiInterfaces";
import { MapVisualization } from "../mapVisualization";
import { TreeVisualizationConfig } from "./treeNode";
import { TreeVisualization } from "./treeVisualization";

/**
 * The composite XTF error visualization: an optional map and an optional error tree of the same
 * validation errors. Mirrors the backend XtfErrorVisualizationConfig. Iteration 1 renders the two views
 * stacked; cross-selection between map and tree is a later iteration and lives in this component.
 */
export interface XtfErrorVisualizationConfig {
  map?: MapVisualizationConfig;
  tree?: TreeVisualizationConfig;
}

interface XtfErrorVisualizationProps {
  config: XtfErrorVisualizationConfig;
}

export const XtfErrorVisualization: FC<XtfErrorVisualizationProps> = ({ config }) => {
  return (
    <Stack sx={{ gap: 2, width: "100%" }}>
      {config.map && <MapVisualization config={config.map} />}
      {config.tree && <TreeVisualization config={config.tree} />}
    </Stack>
  );
};
