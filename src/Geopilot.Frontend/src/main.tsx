import React from "react";
import ReactDOM from "react-dom/client";
import createCache from "@emotion/cache";
import { CacheProvider } from "@emotion/react";
import "./index.css";
import "./assets/fonts/fonts.css";
import { AppContext } from "./appContext.tsx";

const nonce = document.querySelector<HTMLMetaElement>('meta[property="csp-nonce"]')?.nonce;
const emotionCache = createCache({ key: "css", nonce });

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <CacheProvider value={emotionCache}>
      <AppContext />
    </CacheProvider>
  </React.StrictMode>,
);
