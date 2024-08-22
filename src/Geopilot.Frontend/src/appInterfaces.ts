export enum Language {
  DE = "de",
  EN = "en",
  FR = "fr",
  IT = "it",
}

export interface TranslationFunction {
  (key: string): string;
}

export interface Validation {
  allowedFileExtensions: string[];
}
