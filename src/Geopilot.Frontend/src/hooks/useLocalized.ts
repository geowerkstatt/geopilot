import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { LocalizedText } from "../api/apiInterfaces";

/**
 * Returns a resolver for a backend multilingual string, picking the entry for the active language and
 * falling back to English, then to any available entry. Reactive to the active language, so consumers
 * re-render and re-resolve when the user switches the UI language.
 */
export const useLocalized = () => {
  const { i18n } = useTranslation();
  return useCallback(
    (entries?: LocalizedText): string =>
      entries?.[i18n.resolvedLanguage ?? "en"] ?? entries?.["en"] ?? Object.values(entries ?? {})[0] ?? "",
    [i18n.resolvedLanguage],
  );
};
