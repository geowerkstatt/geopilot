import { FC, useEffect, useState } from "react";
import { MsalProvider } from "@azure/msal-react";
import { FC, useEffect, useMemo, useState } from "react";
import { Alert } from "react-bootstrap";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { Snackbar } from "@mui/material";
import BannerContent from "./BannerContent";
import Footer from "./Footer.jsx";
import Home from "./pages/home/Home";
import Admin from "./pages/admin/Admin";
import ModalContent from "./ModalContent";
import "./app.css";
import { OidcContainer } from "./auth/OidcContainer.js";
import { AdminTemplate } from "./auth/AdminTemplate";
import { LoggedOutTemplate } from "./auth/LoggedOutTemplate";
import { I18nextProvider } from "react-i18next";
import i18n from "./i18n";
import { createTheme, ThemeProvider } from "@mui/material/styles";
import { deDE, enUS, frFR, itIT } from "@mui/material/locale";
import DeliveryOverview from "./pages/admin/DeliveryOverview";
import Users from "./pages/admin/Users";
import Mandates from "./pages/admin/Mandates";
import Organisations from "./pages/admin/Organisations";
import { PromptProvider } from "./components/prompt/PromptContext";
import { Prompt } from "./components/prompt/Prompt";
import { AlertProvider } from "./components/alert/AlertContext";
import { AlertBanner } from "./components/alert/AlertBanner";
import { ClientSettings, Language, ModalContentType } from "./AppInterfaces";

export const App: FC = () => {
  const [modalContent, setModalContent] = useState<string>();
  const [modalContentType, setModalContentType] = useState<ModalContentType>();
  const [showModalContent, setShowModalContent] = useState(false);
  const [showBannerContent, setShowBannerContent] = useState(false);
  const [clientSettings, setClientSettings] = useState<ClientSettings>();
  const [backendVersion, setBackendVersion] = useState("");
  const [datenschutzContent, setDatenschutzContent] = useState("");
  const [impressumContent, setImpressumContent] = useState("");
  const [infoHilfeContent, setInfoHilfeContent] = useState("");
  const [bannerContent, setBannerContent] = useState("");
  const [nutzungsbestimmungenContent, setNutzungsbestimmungenContent] = useState("");
  const [quickStartContent, setQuickStartContent] = useState("");
  const [licenseInfo, setLicenseInfo] = useState(null);
  const [licenseInfoCustom, setLicenseInfoCustom] = useState(null);
  const [alertText, setAlertText] = useState("");
  const [language, setLanguage] = useState<Language>("en");
  const [theme, setTheme] = useState({});

  // Update HTML title property
  useEffect(() => {
    document.title = clientSettings?.application?.name + " " + backendVersion;
  }, [clientSettings, backendVersion]);

  useEffect(() => {
    const link = document.querySelector("link[rel=icon]");
    const faviconHref = clientSettings?.application?.favicon;
    if (faviconHref) {
      link?.setAttribute("href", faviconHref);
    }
  }, [clientSettings]);

  const runFetch = async (url: string, contentType?: string) => {
    const response = await fetch(url, {
      method: "GET",
    });

    if (response.ok) {
      const responseContentType = response.headers.get("content-type");
      if (responseContentType?.indexOf("application/json") !== -1) {
        return await response.json();
      } else if (!contentType || responseContentType?.includes(contentType)) {
        return await response.text();
      } else {
        return "";
      }
    } else {
      return response;
    }
  };

  useEffect(() => {
    runFetch("client-settings.json").then(setClientSettings);
    runFetch("/api/v1/version").then(version => {
      setBackendVersion(version.split("+")[0]);
    });
    runFetch("impressum.md", "ext/markdown").then(setImpressumContent);
    runFetch("datenschutz.md", "ext/markdown").then(setDatenschutzContent);
    runFetch("info-hilfe.md", "ext/markdown").then(setInfoHilfeContent);
    runFetch("nutzungsbestimmungen.md", "ext/markdown").then(setNutzungsbestimmungenContent);
    runFetch("banner.md", "ext/markdown").then(setBannerContent);
    runFetch("quickstart.txt", "text/plain").then(setQuickStartContent);
    runFetch("license.json", "application/json").then(setLicenseInfo);
    runFetch("license.custom.json", "application/json").then(setLicenseInfoCustom);
  }, []);

  useEffect(() => {
    let baseTheme = {};
    if (clientSettings && clientSettings.theme) {
      baseTheme = clientSettings.theme;
    }

    let lng = enUS;
    switch (language) {
      case "de":
        lng = deDE;
        break;
      case "fr":
        lng = frFR;
        break;
      case "it":
        lng = itIT;
        break;
      case "en":
        lng = enUS;
        break;
    }
    setTheme(createTheme(baseTheme, lng));
  }, [clientSettings, language]);

  const openModalContent = (content: string, type: ModalContentType) => {
    setModalContent(content);
    setModalContentType(type);
    setShowModalContent(true);
  };

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
        <OidcContainer authScopes={clientSettings?.authScopes} onLoginError={setAlertText}>
              <PromptProvider>
                <Prompt />
                <AlertProvider>
                  <AlertBanner />
                  <div className="app">
                    <BrowserRouter>
                      <Routes>
                        <Route
                          path="/"
                          element={
                            <>
                              <Home
                                clientSettings={clientSettings}
                                nutzungsbestimmungenAvailable={!!nutzungsbestimmungenContent}
                            showNutzungsbestimmungen={() => openModalContent(nutzungsbestimmungenContent, "markdown")}
                                quickStartContent={quickStartContent}
                                setShowBannerContent={setShowBannerContent}
                              />
                              <Footer
                                openModalContent={openModalContent}
                                infoHilfeContent={infoHilfeContent}
                                nutzungsbestimmungenContent={nutzungsbestimmungenContent}
                                datenschutzContent={datenschutzContent}
                                impressumContent={impressumContent}
                                clientSettings={clientSettings}
                                appVersion={backendVersion}
                                licenseInfoCustom={licenseInfoCustom}
                                licenseInfo={licenseInfo}
                              />
                            </>
                          }
                        />
                      </Routes>
                      <LoggedOutTemplate>
                        <Routes>
                          <Route path="*" element={<Navigate to="/" />} />
                        </Routes>
                      </LoggedOutTemplate>
                      <AdminTemplate>
                        <Routes>
                          <Route path="admin" element={<Navigate to="/admin/delivery-overview" replace />} />
                          <Route path="admin" element={<Admin clientSettings={clientSettings} />}>
                            <Route path="delivery-overview" element={<DeliveryOverview />} />
                            <Route path="users" element={<Users />} />
                            <Route path="mandates" element={<Mandates />} />
                            <Route path="organisations" element={<Organisations />} />
                          </Route>
                        </Routes>
                      </AdminTemplate>
                    </BrowserRouter>
                    <ModalContent
                      show={showModalContent}
                      content={modalContent}
                      type={modalContentType}
                      onHide={() => setShowModalContent(false)}
                    />
                    {bannerContent && showBannerContent && (
                      <BannerContent content={bannerContent} onHide={() => setShowBannerContent(false)} />
                    )}
                  </div>
              <Snackbar key={alertText} open={alertText !== ""} anchorOrigin={{ vertical: "top", horizontal: "right" }}>
                    <Alert variant="danger" dismissible onClose={() => setAlertText("")}>
                      <p>{alertText}</p>
                    </Alert>
                  </Snackbar>
                </AlertProvider>
              </PromptProvider>
        </OidcContainer>
      </ThemeProvider>
    </I18nextProvider>
  );
};

export default App;
