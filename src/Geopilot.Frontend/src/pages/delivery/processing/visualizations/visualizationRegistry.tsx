import { ComponentType } from "react";
import { VisualizationKind } from "../../../../api/apiInterfaces";
import { MapVisualization } from "../mapVisualization";
import { TreeVisualization } from "./treeVisualization";

interface VisualizationComponentProps {
  url: string;
}

export const visualizationComponents: Partial<Record<VisualizationKind, ComponentType<VisualizationComponentProps>>> = {
  [VisualizationKind.Tree]: TreeVisualization,
  [VisualizationKind.Map]: MapVisualization,
};
