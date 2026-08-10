import { useAppSettings } from "../components/appSettings/appSettingsInterface";
import { useLocalized } from "./useLocalized";

/**
 * Returns the configured application name for the active UI language, falling back to the default
 * name. Returns undefined when no name is configured, so callers can use it directly as a render
 * guard. Reactive to the active language.
 *
 * application.localName is a Record<string, string>, structurally the backend LocalizedText, so we
 * reuse useLocalized to keep a single language-resolution strategy across the app. The data here
 * comes from client-settings.json, not from the backend.
 */
export const useApplicationName = (): string | undefined => {
  const { localized } = useLocalized();
  const { clientSettings } = useAppSettings();
  const application = clientSettings?.application;
  return localized(application?.localName, application?.name) || undefined;
};
