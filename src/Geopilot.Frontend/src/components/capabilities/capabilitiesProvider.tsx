import { FC, PropsWithChildren, useEffect, useState } from "react";
import { CircularProgress } from "@mui/material";
import { CapabilitiesResponse } from "../../api/generated";
import useFetch from "../../hooks/useFetch.ts";
import { PageContent } from "../styledComponents";
import { CapabilitiesContext } from "./capabilitiesContext";

/**
 * Loads what this installation offers before rendering its children. Blocking on purpose: a form field
 * that mounts late is missing from the payload it submits, which would clear the stored value instead of
 * leaving it alone. An installation whose capabilities cannot be read offers nothing.
 */
export const CapabilitiesProvider: FC<PropsWithChildren> = ({ children }) => {
  const { fetchApi } = useFetch();
  const [capabilities, setCapabilities] = useState<CapabilitiesResponse>();

  useEffect(() => {
    fetchApi<CapabilitiesResponse>("/api/v1/capabilities")
      .then(setCapabilities)
      .catch(() => setCapabilities({ machineDeliveryEnabled: false }));
  }, [fetchApi]);

  if (!capabilities) {
    return (
      <PageContent>
        <CircularProgress />
      </PageContent>
    );
  }

  return <CapabilitiesContext.Provider value={capabilities}>{children}</CapabilitiesContext.Provider>;
};
