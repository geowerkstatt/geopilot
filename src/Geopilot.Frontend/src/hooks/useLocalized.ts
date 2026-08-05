import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { LocalizedText } from "../api/apiInterfaces";

const takeLanguageIfAvailable = (localizedText: LocalizedText | undefined | null, lang?: string): string | undefined =>
  lang && localizedText?.[lang] && localizedText[lang].trim() !== "" ? localizedText[lang] : undefined;

/**
 * Returns a resolver for a backend multilingual string, picking the entry for the active language.
 * If the string for the active language is not available, the following fallback strategy is used:
 * English > German > French > Italian > any available language > specified fallback > empty string
 */
export const useLocalized = () => {
  const { i18n } = useTranslation();

  return useCallback(
    (localizedText?: LocalizedText, fallback: string = ""): string =>
      takeLanguageIfAvailable(localizedText, i18n.resolvedLanguage) ??
      takeLanguageIfAvailable(localizedText, "en") ??
      takeLanguageIfAvailable(localizedText, "de") ??
      takeLanguageIfAvailable(localizedText, "fr") ??
      takeLanguageIfAvailable(localizedText, "it") ??
      Object.values(localizedText ?? {}).find(value => value.trim() !== "") ??
      fallback ??
      "",
    [i18n.resolvedLanguage],
  );
};
