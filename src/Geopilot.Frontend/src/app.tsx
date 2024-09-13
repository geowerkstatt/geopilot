import { FC, useState } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import "./app.css";
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
import { PrivacyPolicy } from "./pages/footer/privacyPolicy.tsx";
import { About } from "./pages/footer/about.tsx";
import { Imprint } from "./pages/footer/imprint.tsx";
import { DeliveryProvider } from "./pages/delivery/deliveryContext.tsx";
import { CircularProgress } from "@mui/material";

export const App: FC = () => {
  const [isSubMenuOpen, setIsSubMenuOpen] = useState(false);
  const { enabled, isAdmin } = useGeopilotAuth();

  return (
    <AppBox>
      <BrowserRouter>
        <Header
          openSubMenu={() => {
            setIsSubMenuOpen(true);
          }}
        />
        <LayoutBox>
          <PageContentBox>
            {!enabled ? (
              <CircularProgress />
            ) : (
              <Routes>
                <Route
                  path="/"
                  element={
                    <DeliveryProvider>
                      <Delivery />
                    </DeliveryProvider>
                  }
                />
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
                <Route path="/imprint" element={<Imprint />} />
                <Route path="/privacy-policy" element={<PrivacyPolicy />} />
                <Route path="/about" element={<About />} />
              </Routes>
            )}
          </PageContentBox>
          <Footer />
        </LayoutBox>
      </BrowserRouter>
    </AppBox>
  );
};

export default App;
