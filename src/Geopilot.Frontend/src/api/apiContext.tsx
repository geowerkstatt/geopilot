import { ApiContextInterface, ApiError, ContentType, FetchParams } from "./apiInterfaces.ts";
import { createContext, FC, PropsWithChildren, useContext } from "react";
import { AlertContext } from "../components/alert/alertContext.tsx";
import { useTranslation } from "react-i18next";

export const ApiContext = createContext<ApiContextInterface>({
  fetchApi: () => {
    throw new Error("fetchApi not implemented");
  },
  fetchLocalizedMarkdown: () => {
    throw new Error("fetchLocalizedMarkdown not implemented");
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
        if (responseContentType !== null && responseContentType?.indexOf("application/json") !== -1) {
          return (await response.json()) as T;
        } else if (!options.responseType || responseContentType?.includes(options.responseType)) {
          return (await response.text()) as T;
        } else {
          throw new ApiError(t("invalidContentType", { contentType: responseContentType }));
        }
      } else {
        let errorResponse;
        const clonedResponse = response.clone();
        try {
          const errorObject = await clonedResponse.json();
          if (errorObject.detail) {
            errorResponse = errorObject.detail;
          } else {
            errorResponse = errorObject.title;
          }
        } catch (e) {
          errorResponse = await response.text();
        }
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

  const fetchLocalizedMarkdown = async (markdown: string, language: string): Promise<string> => {
    try {
      if (!language) {
        throw new Error("Language undefined");
      }
      const response = await fetchApi<string>(`/${markdown}.${language}.md`, { responseType: ContentType.Markdown });
      if (response) {
        return response;
      }
      throw new Error("Language-specific markdown not found");
    } catch (error) {
      try {
        return await fetchApi<string>(`/${markdown}.md`, { responseType: ContentType.Markdown });
      } catch (fallbackError) {
        console.error("Failed to fetch markdown:", fallbackError);
        return "";
      }
    }
  };

  return <ApiContext.Provider value={{ fetchApi, fetchLocalizedMarkdown }}>{children}</ApiContext.Provider>;
};
