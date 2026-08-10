import { useTranslation } from "react-i18next";
import { useAppSettings } from "../components/appSettings/appSettingsInterface";

/**
 * Returns the configured application title for the active UI language. Returns undefined when no
 * title is configured for that language, so callers can use it directly as a render guard. Reactive
 * to the active language.
 */
export const useApplicationTitle = (): string | undefined => {
  const { i18n } = useTranslation();
  const { clientSettings } = useAppSettings();
  return clientSettings?.application?.localTitle?.[i18n.language];
};
