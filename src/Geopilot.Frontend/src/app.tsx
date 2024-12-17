import { FC, useState } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import "./app.css";
import { CircularProgress } from "@mui/material";
import { useGeopilotAuth } from "./auth";
import Header from "./components/header/header";
import { AppBox, LayoutBox, PageContentBox } from "./components/styledComponents";
import Admin from "./pages/admin/admin";
import DeliveryOverview from "./pages/admin/deliveryOverview";
import Mandates from "./pages/admin/mandates";
import Organisations from "./pages/admin/organisations";
import Users from "./pages/admin/users";
import Delivery from "./pages/delivery/delivery";
import { DeliveryProvider } from "./pages/delivery/deliveryContext.tsx";
import { About } from "./pages/footer/about.tsx";
import Footer from "./pages/footer/footer";
import { Imprint } from "./pages/footer/imprint.tsx";
import { PrivacyPolicy } from "./pages/footer/privacyPolicy.tsx";

export const App: FC = () => {
  const [isSubMenuOpen, setIsSubMenuOpen] = useState(false);
  const { isLoading, isAdmin } = useGeopilotAuth();

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
            {isLoading ? (
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
