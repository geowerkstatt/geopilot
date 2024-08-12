import { FC, useEffect, useState } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import "./app.css";
import { I18nextProvider } from "react-i18next";
import i18n from "./i18n";
import { createTheme, CustomTheme, ThemeProvider } from "@mui/material/styles";
import { deDE as coreDe, enUS as coreEn, frFR as coreFr, itIT as coreIt } from "@mui/material/locale";
import { deDE as gridDe, enUS as gridEn, frFR as gridFr, itIT as gridIt } from "@mui/x-data-grid/locales";
import { PromptProvider } from "./components/prompt/promptContext";
import { Prompt } from "./components/prompt/prompt";
import { AlertProvider } from "./components/alert/alertContext";
import { AlertBanner } from "./components/alert/alertBanner";
import { Language } from "./appInterfaces";
import { geopilotTheme } from "./appTheme";
import { AppBox, LayoutBox, PageContentBox } from "./components/styledComponents";
import Header from "./components/header/header";
import { useGeopilotAuth } from "./auth";
import Delivery from "./pages/delivery/delivery";
import Admin from "./pages/admin/admin";
import DeliveryOverview from "./pages/admin/deliveryOverview";
import Users from "./pages/admin/users";
import Mandates from "./pages/admin/mandates";
import Organisations from "./pages/admin/organisations";
import Footer from "./pages/footer/footer";

export const App: FC = () => {
  const [language, setLanguage] = useState<Language>(Language.EN);
  const [theme, setTheme] = useState<CustomTheme>(geopilotTheme);
  const [isSubMenuOpen, setIsSubMenuOpen] = useState(false);
  const { isAdmin } = useGeopilotAuth();
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
        <PromptProvider>
          <Prompt />
          <AlertProvider>
            <AlertBanner />
            <AppBox>
              <BrowserRouter>
                <Header
                  openSubMenu={() => {
                    setIsSubMenuOpen(true);
                  }}
                />
                <LayoutBox>
                  <PageContentBox>
                    <Routes>
                      <Route path="/" element={<Delivery />} />
                      {isAdmin ? (
                        <>
                          <Route path="admin" element={<Navigate to="/admin/delivery-overview" replace />} />
                          <Route
                            path="admin"
                            element={<Admin isSubMenuOpen={isSubMenuOpen} setIsSubMenuOpen={setIsSubMenuOpen} />}>
                            <Route path="delivery-overview" element={<DeliveryOverview />} />
                            <Route path="users" element={<Users />} />
                            <Route path="mandates" element={<Mandates />} />
                            <Route path="organisations" element={<Organisations />} />
                          </Route>
                        </>
                      ) : (
                        <Route path="admin/*" element={<Navigate to="/" replace />} />
                      )}
                    </Routes>
                  </PageContentBox>
                  <Footer />
                </LayoutBox>
              </BrowserRouter>
            </AppBox>
          </AlertProvider>
        </PromptProvider>
      </ThemeProvider>
    </I18nextProvider>
  );
};

export default App;
