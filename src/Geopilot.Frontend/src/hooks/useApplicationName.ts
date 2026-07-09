import { useTranslation } from "react-i18next";
import { ClientSettings, useAppSettings } from "../components/appSettings/appSettingsInterface";

/**
 * Resolves the configured application name for the given language, preferring the language-specific
 * localName and falling back to the default name. Returns undefined when no name is configured.
 * Shared by the useApplicationName hook and the settings provider, which supplies the context and
 * therefore cannot consume the hook itself.
 */
export const resolveApplicationName = (
  application: ClientSettings["application"] | undefined,
  language: string,
): string | undefined => application?.localName?.[language] || application?.name;

/**
 * Returns the configured application name for the active UI language. Returns undefined when no name
 * is configured, so callers can use it directly as a render guard. Reactive to the active language.
 */
export const useApplicationName = (): string | undefined => {
  const { i18n } = useTranslation();
  const { clientSettings } = useAppSettings();
  return resolveApplicationName(clientSettings?.application, i18n.language);
};
