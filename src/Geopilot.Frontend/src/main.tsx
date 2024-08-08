import React from "react";
import ReactDOM from "react-dom/client";
import App from "./app";
import "./index.css";
import "./assets/fonts/fonts.css";
import "bootstrap/dist/css/bootstrap.min.css";
import { AppSettingsProvider } from "./components/appSettings/appSettingsContext";
import { GeopilotAuthProvider } from "./auth/geopilotAuthComponent";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AppSettingsProvider>
      <GeopilotAuthProvider>
        <App />
      </GeopilotAuthProvider>
    </AppSettingsProvider>
  </React.StrictMode>,
);
