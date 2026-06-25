import { useCallback } from "react";
import { useTranslation } from "react-i18next";

/**
 * Returns a resolver for a backend multilingual string (a language-keyed record), picking the entry for
 * the active language and falling back to English. Reactive to the active language, so consumers re-render
 * and re-resolve when the user switches the UI language.
 */
export const useLocalized = () => {
  const { i18n } = useTranslation();
  return useCallback(
    (entries?: Record<string, string>): string => entries?.[i18n.resolvedLanguage ?? "en"] ?? entries?.["en"] ?? "",
    [i18n.resolvedLanguage],
  );
};
