import i18n from "i18next";
import backend from "i18next-http-backend";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";

i18n
  .use(backend)
  .use(initReactI18next)
  .use(LanguageDetector)
  .init({
    detection: {
      order: ["navigator", "cookie", "htmlTag"],
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
    },
    supportedLngs: ["de", "en", "it", "fr"],
    whitelist: ["de", "en", "it", "fr"],
    ns: ["common"],
    defaultNS: "common",
    interpolation: {
      escapeValue: false,
      formatSeparator: ",",
      transSupportBasicHtmlNodes: true,
    },
  });

export default i18n;
