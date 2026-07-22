import { ReactNode } from "react";
import { XtfErrorVisualization, XtfErrorVisualizationConfig } from "./xtfErrorVisualization";

/**
 * A visualization produced by a pipeline step: a `type` discriminator plus its typed `data` payload.
 * Mirrors the backend Visualization&lt;TData&gt; envelope.
 */
export type Visualization = { type: "xtfError"; data: XtfErrorVisualizationConfig };

/** Renders the built-in visualization component selected by the envelope's `type` discriminator. */
export const renderVisualization = (visualization: Visualization): ReactNode => {
  switch (visualization.type) {
    case "xtfError":
      return <XtfErrorVisualization config={visualization.data} />;
    default:
      // Renders nothing for an unrecognized type rather than throwing. Once a second visualization
      // type exists, turn this into a compile-time exhaustiveness guard (assign to `never`).
      console.warn("Unknown visualization type.", visualization);
      return null;
  }
};
