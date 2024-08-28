import { ApiContextInterface, ApiError, FetchParams } from "./apiInterfaces.ts";
import { createContext, FC, PropsWithChildren, useContext } from "react";
import { AlertContext } from "../components/alert/alertContext.tsx";
import { useTranslation } from "react-i18next";

export const ApiContext = createContext<ApiContextInterface>({
  fetchApi: () => {
    throw new Error("fetchApi not implemented");
  },
});

export const ApiProvider: FC<PropsWithChildren> = ({ children }) => {
  const { t } = useTranslation();
  const { showAlert } = useContext(AlertContext);

  async function fetchApi<T>(url: string, options: FetchParams = {}): Promise<T> {
    try {
      const response = await fetch(url, options);

      if (response.ok) {
        const responseContentType = response.headers.get("content-type");
        if (responseContentType?.indexOf("application/json") !== -1) {
          return (await response.json()) as T;
        } else if (!options.responseType || responseContentType?.includes(options.responseType)) {
          return (await response.text()) as T;
        } else {
          throw new ApiError(t("invalidContentType", { contentType: responseContentType }));
        }
      } else {
        const errorResponse = await response.text();
        throw new ApiError(errorResponse, response.status);
      }
    } catch (error) {
      if (options.errorMessageLabel) {
        showAlert(t(options.errorMessageLabel, { error: (error as Error)?.message }), "error");
      }
      if (error instanceof ApiError) {
        throw error;
      } else {
        throw new ApiError(String(error));
      }
    }
  }

  return <ApiContext.Provider value={{ fetchApi }}>{children}</ApiContext.Provider>;
};
