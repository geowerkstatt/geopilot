import { useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { LocalizedText } from "../api/apiInterfaces";

/** Resolver for the text of a backend multilingual string, from the useLocalized hook. */
export type LocalizedResolver = (localizedText?: LocalizedText, fallback?: string) => string;

/** Resolver for the language a backend multilingual string is displayed in, from the useLocalized hook. */
type LanguageResolver = (localizedText?: LocalizedText) => string | undefined;

export interface Localizer {
  localized: LocalizedResolver;
  language: LanguageResolver;
}

const hasTextForLanguage = (localizedText: LocalizedText | undefined, language?: string): boolean =>
  !!language && !!localizedText?.[language] && localizedText[language].trim() !== "";

/**
 * Determines which language a multilingual string is displayed in. Prefers the active language and
 * otherwise falls back to English > German > French > Italian > any available language. Returns undefined
 * when the string carries no text at all.
 */
const resolveLanguage = (
  localizedText: LocalizedText | undefined,
  activeLanguage: string | undefined,
): string | undefined =>
  [activeLanguage, "en", "de", "fr", "it"].find(candidate => hasTextForLanguage(localizedText, candidate)) ??
  Object.keys(localizedText ?? {}).find(candidate => hasTextForLanguage(localizedText, candidate));

/**
 * Resolves the text of a multilingual string, using the language chosen by resolveLanguage and the
 * given fallback when the string carries no text.
 */
const resolveLocalized = (
  localizedText: LocalizedText | undefined,
  activeLanguage: string | undefined,
  fallback: string = "",
): string => {
  const resolvedLanguage = resolveLanguage(localizedText, activeLanguage);
  return (resolvedLanguage ? localizedText?.[resolvedLanguage] : undefined) ?? fallback;
};

/**
 * Provides resolvers for multilingual strings, picking the entry for the active language.
 * If the string for the active language is not available, the following fallback strategy is used:
 * English > German > French > Italian > any available language
 *
 * `localized` returns the text of the resolved language, `language` returns the resolved language itself.
 */
export const useLocalized = (): Localizer => {
  const { i18n } = useTranslation();
  const activeLanguage = i18n.resolvedLanguage;

  const language = useCallback<LanguageResolver>(
    localizedText => resolveLanguage(localizedText, activeLanguage),
    [activeLanguage],
  );

  const localized = useCallback<LocalizedResolver>(
    (localizedText, fallback) => resolveLocalized(localizedText, activeLanguage, fallback),
    [activeLanguage],
  );

  return useMemo(() => ({ localized, language }), [localized, language]);
};
