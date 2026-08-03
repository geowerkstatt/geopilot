import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { LocalizedText } from "../api/apiInterfaces";

/**
 * Returns a resolver for a backend multilingual string, picking the entry for the active language.
 * If the string for the active language is not available, the following fallback strategy is used:
 * English > German > French > Italian > any available language > specified fallback > empty string
 */
export const useLocalized = () => {
  const { i18n } = useTranslation();
  return useCallback(
    (entries?: LocalizedText, fallback: string = ""): string =>
      entries?.[i18n.resolvedLanguage ?? "en"] ??
      entries?.["en"] ??
      entries?.["de"] ??
      entries?.["fr"] ??
      entries?.["it"] ??
      Object.values(entries ?? {})[0] ??
      fallback,
    [i18n.resolvedLanguage],
  );
};
