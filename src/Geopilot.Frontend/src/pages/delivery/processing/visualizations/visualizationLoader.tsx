import { FC, ReactNode, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { CircularProgress, Stack, Typography } from "@mui/material";
import { ContentType, MapVisualizationConfig } from "../../../../api/apiInterfaces";
import useFetch from "../../../../hooks/useFetch";
import { MapVisualization } from "./map/mapVisualization";
import { TreeVisualizationConfig } from "./tree/treeNode";
import { TreeVisualization } from "./tree/treeVisualization";

/**
 * A visualization produced by a pipeline step: a `type` discriminator plus its typed `data` payload.
 * Mirrors the backend Visualization&lt;TData&gt; envelope.
 */
type Visualization = { type: "map"; data: MapVisualizationConfig } | { type: "tree"; data: TreeVisualizationConfig };

/** Renders the built-in visualization component selected by the envelope's `type` discriminator. */
const renderVisualization = (visualization: Visualization): ReactNode => {
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

interface VisualizationLoaderProps {
  /** Absolute URL to fetch the self-describing JSON visualization config from. */
  url: string;
}

/**
 * Fetches a visualization config lazily and renders the component its `type` selects. Owns the shared
 * loading and error UI so the individual visualization components stay pure renderers of a parsed config.
 */
export const VisualizationLoader: FC<VisualizationLoaderProps> = ({ url }) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const [visualization, setVisualization] = useState<Visualization | null>(null);
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setVisualization(null);
    setHasError(false);

    fetchApi<Visualization>(url, { method: "GET", responseType: ContentType.Json })
      .then(loaded => {
        if (!cancelled) setVisualization(loaded);
      })
      .catch(() => {
        if (!cancelled) setHasError(true);
      });

    return () => {
      cancelled = true;
    };
  }, [url, fetchApi]);

  if (hasError) {
    return (
      <Typography color="error" data-cy="visualization-error">
        {t("visualizationLoadFailed")}
      </Typography>
    );
  }

  if (!visualization) {
    return (
      <Stack sx={{ justifyContent: "center", p: 2 }} data-cy="visualization-loading">
        <CircularProgress />
      </Stack>
    );
  }

  return <>{renderVisualization(visualization)}</>;
};
