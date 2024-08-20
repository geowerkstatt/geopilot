import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import "./assets/fonts/fonts.css";
import "bootstrap/dist/css/bootstrap.min.css";
import { AppContext } from "./appContext.tsx";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AppContext />
  </React.StrictMode>,
);
