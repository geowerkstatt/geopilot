import { PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
import { useEffect, useMemo, useState } from "react";
import { Alert } from "react-bootstrap";
import { BrowserRouter as Router, Routes, Route, Navigate } from "react-router-dom";
import { Snackbar } from "@mui/material";
import BannerContent from "./BannerContent";
import Footer from "./Footer";
import Header from "./Header";
import Home from "./pages/home/Home";
import Admin from "./pages/admin/Admin";
import ModalContent from "./ModalContent";
import "./app.css";
import { AuthProvider } from "./auth/AuthContext";
import { AdminTemplate } from "./auth/AdminTemplate";
import { LoggedOutTemplate } from "./auth/LoggedOutTemplate";
import { I18nextProvider } from "react-i18next";
import i18n from "./i18n";
import { createTheme, ThemeProvider } from "@mui/material/styles";
import { deDE, enUS, frFR, itIT } from "@mui/material/locale";

const baseTheme = {};

const getThemeByLanguage = lng => {
  switch (lng) {
    case "de":
      return createTheme(baseTheme, deDE);
    case "fr":
      return createTheme(baseTheme, frFR);
    case "it":
      return createTheme(baseTheme, itIT);
    case "en":
      return createTheme(baseTheme, enUS);
    default:
      return createTheme(baseTheme, enUS);
  }
};

export const App = () => {
  const [modalContent, setModalContent] = useState(false);
  const [modalContentType, setModalContentType] = useState(null);
  const [showModalContent, setShowModalContent] = useState(false);
  const [showBannerContent, setShowBannerContent] = useState(false);
  const [clientSettings, setClientSettings] = useState({});
  const [auth, setAuth] = useState(undefined);
  const [backendVersion, setBackendVersion] = useState("");
  const [datenschutzContent, setDatenschutzContent] = useState(null);
  const [impressumContent, setImpressumContent] = useState(null);
  const [infoHilfeContent, setInfoHilfeContent] = useState(null);
  const [bannerContent, setBannerContent] = useState(null);
  const [nutzungsbestimmungenContent, setNutzungsbestimmungenContent] = useState(null);
  const [quickStartContent, setQuickStartContent] = useState(null);
  const [licenseInfo, setLicenseInfo] = useState(null);
  const [licenseInfoCustom, setLicenseInfoCustom] = useState(null);
  const [alertText, setAlertText] = useState("");
  const [theme, setTheme] = useState(getThemeByLanguage(i18n.language));

  // Update HTML title property
  useEffect(() => {
    document.title = clientSettings?.application?.name + " " + backendVersion;
  }, [clientSettings, backendVersion]);

  useEffect(() => {
    const link = document.querySelector("link[rel=icon]");
    const faviconHref = clientSettings?.application?.favicon;
    if (faviconHref) {
      link.setAttribute("href", faviconHref);
    }
  }, [clientSettings]);

  // Fetch client settings
  useEffect(() => {
    fetch("client-settings.json")
      .then(res => res.headers.get("content-type")?.includes("application/json") && res.json())
      .then(setClientSettings);
  }, []);

  useEffect(() => {
    fetch("/api/v1/user/auth")
      .then(res => res.headers.get("content-type")?.includes("application/json") && res.json())
      .then(setAuth);
  }, []);

  useEffect(() => {
    fetch("api/v1/version")
      .then(res => res.headers.get("content-type")?.includes("text/plain") && res.text())
      .then(version => setBackendVersion(version));
  }, []);

  // Fetch optional custom content
  useEffect(() => {
    fetch("impressum.md")
      .then(res => res.headers.get("content-type")?.includes("ext/markdown") && res.text())
      .then(text => setImpressumContent(text));

    fetch("datenschutz.md")
      .then(res => res.headers.get("content-type")?.includes("ext/markdown") && res.text())
      .then(text => setDatenschutzContent(text));

    fetch("info-hilfe.md")
      .then(res => res.headers.get("content-type")?.includes("ext/markdown") && res.text())
      .then(text => setInfoHilfeContent(text));

    fetch("nutzungsbestimmungen.md")
      .then(res => res.headers.get("content-type")?.includes("ext/markdown") && res.text())
      .then(text => setNutzungsbestimmungenContent(text));

    fetch("banner.md")
      .then(res => res.headers.get("content-type")?.includes("ext/markdown") && res.text())
      .then(text => setBannerContent(text));

    fetch("quickstart.txt")
      .then(res => res.headers.get("content-type")?.includes("text/plain") && res.text())
      .then(text => setQuickStartContent(text));

    fetch("license.json")
      .then(res => res.headers.get("content-type")?.includes("application/json") && res.json())
      .then(json => setLicenseInfo(json));

    fetch("license.custom.json")
      .then(res => res.headers.get("content-type")?.includes("application/json") && res.json())
      .then(json => setLicenseInfoCustom(json));
  }, []);

  const openModalContent = (content, type) =>
    setModalContent(content) & setModalContentType(type) & setShowModalContent(true);

  const authCache = clientSettings?.authCache;
  const msalInstance = useMemo(() => {
    return new PublicClientApplication({
      auth,
      cache: authCache,
    });
  }, [auth, authCache]);

  useEffect(() => {
    const handleLanguageChange = lng => {
      const newTheme = getThemeByLanguage(lng);
      setTheme(newTheme);
    };

    i18n.on("languageChanged", handleLanguageChange);

    return () => {
      i18n.off("languageChanged", handleLanguageChange);
    };
  }, [i18n]);

  return (
    <I18nextProvider i18n={i18n}>
      <ThemeProvider theme={theme}>
        <MsalProvider instance={msalInstance}>
          <AuthProvider authScopes={clientSettings?.authScopes} onLoginError={setAlertText}>
            <div className="app">
              <Router>
                <Header clientSettings={clientSettings} />
                <Routes>
                  <Route
                    exact
                    path="/"
                    element={
                      <Home
                        clientSettings={clientSettings}
                        nutzungsbestimmungenAvailable={nutzungsbestimmungenContent ? true : false}
                        showNutzungsbestimmungen={() => openModalContent(nutzungsbestimmungenContent, "markdown")}
                        quickStartContent={quickStartContent}
                        setShowBannerContent={setShowBannerContent}
                      />
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
                    <Route path="/admin" element={<Admin />} />
                  </Routes>
                </AdminTemplate>
              </Router>
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
              <ModalContent
                className="modal"
                show={showModalContent}
                content={modalContent}
                type={modalContentType}
                onHide={() => setShowModalContent(false)}
              />
              {bannerContent && showBannerContent && (
                <BannerContent className="banner" content={bannerContent} onHide={() => setShowBannerContent(false)} />
              )}
            </div>
            <Snackbar key={alertText} open={alertText !== ""} anchorOrigin={{ vertical: "top", horizontal: "right" }}>
              <Alert variant="danger" dismissible onClose={() => setAlertText("")}>
                <p>{alertText}</p>
              </Alert>
            </Snackbar>
          </AuthProvider>
        </MsalProvider>
      </ThemeProvider>
    </I18nextProvider>
  );
};

export default App;
