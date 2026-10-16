import { ReactNode } from "react";
import { XtfDiffVisualization, XtfDiffVisualizationConfig } from "./xtfDiffVisualization";
import { XtfErrorVisualization, XtfErrorVisualizationConfig } from "./xtfErrorVisualization";

/**
 * A visualization produced by a pipeline step: a `type` discriminator plus its typed `data` payload.
 * Mirrors the backend Visualization&lt;TData&gt; envelope.
 */
export type Visualization =
  | { type: "xtfError"; data: XtfErrorVisualizationConfig }
  | { type: "xtfDiff"; data: XtfDiffVisualizationConfig };

/** Renders the built-in visualization component selected by the envelope's `type` discriminator. */
export const renderVisualization = (visualization: Visualization): ReactNode => {
  switch (visualization.type) {
    case "xtfError":
      return <XtfErrorVisualization config={visualization.data} />;
    case "xtfDiff":
      return <XtfDiffVisualization config={visualization.data} />;
    default: {
      // A type added to the union without a case here fails to compile. At runtime an unknown type, from a newer
      // backend for example, renders nothing rather than throwing.
      const unknown: never = visualization;
      console.warn("Unknown visualization type.", unknown);
      return null;
    }
  }
};
