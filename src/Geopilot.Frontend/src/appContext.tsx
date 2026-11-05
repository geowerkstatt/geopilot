import { useEffect, useState } from "react";
import { I18nextProvider } from "react-i18next";
import { GlobalStyles, ThemeProvider } from "@mui/material";
import { deDE as coreDe, enUS as coreEn, frFR as coreFr, itIT as coreIt } from "@mui/material/locale";
import { createTheme, Theme } from "@mui/material/styles";
import { deDE as gridDe, enUS as gridEn, frFR as gridFr, itIT as gridIt } from "@mui/x-data-grid/locales";
import { LocalizationProvider } from "@mui/x-date-pickers";
import { AdapterDateFns } from "@mui/x-date-pickers/AdapterDateFnsV2";
import {
  deDE as pickersDe,
  enUS as pickersEn,
  frFR as pickersFr,
  itIT as pickersIt,
} from "@mui/x-date-pickers/locales";
import { Locale } from "date-fns";
import { de, enGB, fr, it } from "date-fns/locale";
import App from "./app.tsx";
import { Language } from "./appInterfaces.ts";
import { geopilotTheme, NEOGEO_LETTERSPACING, px2rem } from "./appTheme.ts";
import { GeopilotAuthProvider } from "./auth/geopilotAuthProvider.tsx";
import { AlertBanner } from "./components/alert/alertBanner.tsx";
import { AlertProvider } from "./components/alert/alertProvider.tsx";
import { AppSettingsProvider } from "./components/appSettings/appSettingsProvider.tsx";
import { Prompt } from "./components/prompt/prompt.tsx";
import { PromptProvider } from "./components/prompt/promptProvider.tsx";
import i18n from "./i18n";

const dateLocales: Record<Language, Locale> = {
  [Language.DE]: de,
  [Language.FR]: fr,
  [Language.IT]: it,
  [Language.EN]: enGB,
};

const pickerLocales: Record<Language, typeof pickersEn> = {
  [Language.DE]: pickersDe,
  [Language.FR]: pickersFr,
  [Language.IT]: pickersIt,
  [Language.EN]: pickersEn,
};

export const AppContext = () => {
  const [language, setLanguage] = useState<Language>(Language.EN);
  const [theme, setTheme] = useState<Theme>(geopilotTheme);

  useEffect(() => {
    let coreLng = coreEn;
    let gridLng = gridEn;
    switch (language) {
      case Language.DE:
        coreLng = coreDe;
        gridLng = gridDe;
        break;
      case Language.FR:
        coreLng = coreFr;
        gridLng = gridFr;
        break;
      case Language.IT:
        coreLng = coreIt;
        gridLng = gridIt;
        break;
      case Language.EN:
        coreLng = coreEn;
        gridLng = gridEn;
        break;
    }
    document.documentElement.lang = language;
    setTheme(createTheme(geopilotTheme, gridLng, coreLng));
  }, [language]);

  useEffect(() => {
    const handleLanguageChange = (lng: Language) => {
      setLanguage(lng);
    };

    i18n.on("languageChanged", handleLanguageChange);

    return () => {
      i18n.off("languageChanged", handleLanguageChange);
    };
  }, []);

  return (
    <I18nextProvider i18n={i18n}>
      <ThemeProvider theme={theme}>
        <GlobalStyles
          styles={{
            ":root": {
              fontFamily: '"NeoGeo", sans-serif',
              fontSize: px2rem(16),
              letterSpacing: NEOGEO_LETTERSPACING,
            },
          }}
        />
        <LocalizationProvider
          dateAdapter={AdapterDateFns}
          adapterLocale={dateLocales[language]}
          localeText={pickerLocales[language].components.MuiLocalizationProvider.defaultProps.localeText}>
          <PromptProvider>
            <Prompt />
            <AlertProvider>
              <AlertBanner />
              <AppSettingsProvider>
                <GeopilotAuthProvider>
                  <App />
                </GeopilotAuthProvider>
              </AppSettingsProvider>
            </AlertProvider>
          </PromptProvider>
        </LocalizationProvider>
      </ThemeProvider>
    </I18nextProvider>
  );
};
