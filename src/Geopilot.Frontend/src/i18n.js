import { initReactI18next } from "react-i18next";
import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import backend from "i18next-http-backend";
import { Language } from "./appInterfaces";

i18n
  .use(backend)
  .use(initReactI18next)
  .use(LanguageDetector)
  .init({
    detection: {
      order: ["cookie", "navigator", "htmlTag"],
      lookupCookie: "i18next",
      caches: ["cookie"],
    },
    backend: {
      loadPath: `/locale/{{lng}}/{{ns}}.json`,
      allowMultiLoading: false,
      queryStringParams: { v: "1.0.0" },
    },
    react: {
      useSuspense: false,
      transSupportBasicHtmlNodes: true,
    },
    load: "languageOnly",
    fallbackLng: Language.EN,
    supportedLngs: Object.values(Language),
    ns: ["common"],
    defaultNS: "common",
    interpolation: {
      escapeValue: false,
      formatSeparator: ",",
    },
  });

export default i18n;
