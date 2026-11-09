import { ParseKeys } from "i18next";

export enum Language {
  DE = "de",
  EN = "en",
  FR = "fr",
  IT = "it",
}

/** Whether a language code, such as the one i18next resolved, is one of the languages the application offers. */
export const isLanguage = (code: string | undefined): code is Language =>
  Object.values(Language).some(language => language === code);

export type TranslationKey = ParseKeys;
