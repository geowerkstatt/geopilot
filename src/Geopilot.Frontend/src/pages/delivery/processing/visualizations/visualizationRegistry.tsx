import { ReactNode } from "react";
import { MapVisualizationConfig } from "../../../../api/apiInterfaces";
import { MapVisualization } from "./map/mapVisualization";
import { TreeVisualizationConfig } from "./tree/treeNode";
import { TreeVisualization } from "./tree/treeVisualization";

/**
 * A visualization produced by a pipeline step: a `type` discriminator plus its typed `data` payload.
 * Mirrors the backend Visualization&lt;TData&gt; envelope.
 */
export type Visualization =
  | { type: "map"; data: MapVisualizationConfig }
  | { type: "tree"; data: TreeVisualizationConfig };

/** Renders the built-in visualization component selected by the envelope's `type` discriminator. */
export const renderVisualization = (visualization: Visualization): ReactNode => {
  switch (visualization.type) {
    case "map":
      return <MapVisualization config={visualization.data} />;
    case "tree":
      return <TreeVisualization config={visualization.data} />;
    default: {
      // Exhaustiveness guard: adding a new visualization type without handling it here fails to compile.
      // An unknown type at runtime renders nothing rather than throwing.
      const unknownVisualization: never = visualization;
      console.warn("Unknown visualization type.", unknownVisualization);
      return null;
    }
  }
};
