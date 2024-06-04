import i18n from "i18next";
import backend from "i18next-http-backend";
import { initReactI18next } from "react-i18next";

i18n
  .use(backend)
  .use(initReactI18next)
  .init({
    detection: {
      order: ["cookie", "htmlTag"],
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
    fallbackLng: {
      de: ["de-CH"],
      default: ["de"],
    },
    supportedLngs: ["de"],
    whitelist: ["de"],
    ns: ["common"],
    defaultNS: "common",
    interpolation: {
      escapeValue: false,
      formatSeparator: ",",
      transSupportBasicHtmlNodes: true,
    },
  });

export default i18n;
