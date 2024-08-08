export enum Language {
  DE = "de",
  EN = "en",
  FR = "fr",
  IT = "it",
}

export interface TranslationFunction {
  (key: string): string;
}

export type ModalContentType = "markdown" | "raw";

export interface Validation {
  allowedFileExtensions: string[];
}

export interface ErrorResponse {
  status: string;
  detail: string;
}
