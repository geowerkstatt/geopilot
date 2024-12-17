import { useEffect, useState } from "react";
import { I18nextProvider } from "react-i18next";
import { GlobalStyles, ThemeProvider } from "@mui/material";
import { deDE as coreDe, enUS as coreEn, frFR as coreFr, itIT as coreIt } from "@mui/material/locale";
import { createTheme, CustomTheme } from "@mui/material/styles";
import { deDE as gridDe } from "@mui/x-data-grid/locales/deDE";
import { enUS as gridEn } from "@mui/x-data-grid/locales/enUS";
import { frFR as gridFr } from "@mui/x-data-grid/locales/frFR";
import { itIT as gridIt } from "@mui/x-data-grid/locales/itIT";
import { ApiProvider } from "./api/apiContext.tsx";
import App from "./app.tsx";
import { Language } from "./appInterfaces.ts";
import { geopilotTheme } from "./appTheme.ts";
import { GeopilotAuthProvider } from "./auth/geopilotAuthComponent.tsx";
import { AlertBanner } from "./components/alert/alertBanner.tsx";
import { AlertProvider } from "./components/alert/alertContext.tsx";
import { AppSettingsProvider } from "./components/appSettings/appSettingsContext.tsx";
import { Prompt } from "./components/prompt/prompt.tsx";
import { PromptProvider } from "./components/prompt/promptContext.tsx";
import i18n from "./i18n";

export const AppContext = () => {
  const [language, setLanguage] = useState<Language>(Language.EN);
  const [theme, setTheme] = useState<CustomTheme>(geopilotTheme);

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
    setTheme(createTheme(geopilotTheme, gridLng, coreLng) as unknown as CustomTheme);
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
              "font-family": '"NeoGeo", sans-serif',
              "font-size": "16px",
              "letter-spacing": "0.05em",
            },
          }}
        />
        <PromptProvider>
          <Prompt />
          <AlertProvider>
            <AlertBanner />
            <ApiProvider>
              <AppSettingsProvider>
                <GeopilotAuthProvider>
                  <App />
                </GeopilotAuthProvider>
              </AppSettingsProvider>
            </ApiProvider>
          </AlertProvider>
        </PromptProvider>
      </ThemeProvider>
    </I18nextProvider>
  );
};
