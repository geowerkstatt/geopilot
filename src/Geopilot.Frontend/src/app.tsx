import { FC, useEffect, useState } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import "./app.css";
import { I18nextProvider } from "react-i18next";
import i18n from "./i18n";
import { createTheme, ThemeProvider } from "@mui/material/styles";
import { deDE, enUS, frFR, itIT } from "@mui/material/locale";
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
  const [theme, setTheme] = useState({});
  const { enabled, user } = useGeopilotAuth();
  useEffect(() => {
    let lng = enUS;
    switch (language) {
      case Language.DE:
        lng = deDE;
        break;
      case Language.FR:
        lng = frFR;
        break;
      case Language.IT:
        lng = itIT;
        break;
      case Language.EN:
        lng = enUS;
        break;
    }
    setTheme(createTheme(geopilotTheme, lng));
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
                <Header />
                <LayoutBox>
                  <PageContentBox>
                    <Routes>
                      <Route path="/" element={<Delivery />} />
                      {enabled && !!user?.isAdmin ? (
                        <>
                          <Route path="admin" element={<Navigate to="/admin/delivery-overview" replace />} />
                          <Route path="admin" element={<Admin />}>
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
