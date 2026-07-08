import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Box, CircularProgress, Typography } from "@mui/material";
import { ContentType } from "../../../../api/apiInterfaces";
import useFetch from "../../../../hooks/useFetch";
import { renderVisualization, Visualization } from "./visualizationRegistry";

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
      <Typography variant="body1" color="error">
        {t("visualizationLoadFailed")}
      </Typography>
    );
  }

  if (!visualization) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 2 }}>
        <CircularProgress />
      </Box>
    );
  }

  return <>{renderVisualization(visualization)}</>;
};
