import { User } from "./auth/AuthInterfaces";

export interface ClientSettings {
  authCache: {
    cacheLocation: string;
    storeAuthStateInCookie: boolean;
  };
  authScopes: string[];
  application: {
    name: string;
    logo: string;
    favicon: string;
  };
  vendor: {
    name: string;
    logo: string;
    url: string;
  };
  theme: object;
}

export type Language = "de" | "fr" | "it" | "en";

export interface TranslationFunction {
  (key: string): string;
}

export interface DataGridColumnValueFormatterParams {
  value: string | number;
}

export type ModalContentType = "markdown" | "raw";

export interface Mandate {
  name: string;
}

export interface Delivery {
  id: number;
  date: Date;
  declaringUser: User;
  mandate: Mandate;
  comment: string;
}
