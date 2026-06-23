import { FC, useState } from "react";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import "./app.css";
import { CircularProgress } from "@mui/material";
import { useGeopilotAuth } from "./auth";
import { ControlledNavigateProvider } from "./components/controlledNavigate/controlledNavigateProvider.tsx";
import Header from "./components/header/header";
import { AppBox, LayoutBox, PageContentBox } from "./components/styledComponents";
import Admin from "./pages/admin/admin";
import DeliveryOverview from "./pages/admin/deliveries/deliveryOverview.tsx";
import MandateDetail from "./pages/admin/mandates/mandateDetail.tsx";
import Mandates from "./pages/admin/mandates/mandates.tsx";
import OrganisationDetail from "./pages/admin/organisations/organisationDetail.tsx";
import Organisations from "./pages/admin/organisations/organisations.tsx";
import UserDetail from "./pages/admin/users/userDetail.tsx";
import Users from "./pages/admin/users/users.tsx";
import Delivery from "./pages/delivery/delivery";
import { DeliveryProvider } from "./pages/delivery/deliveryContext.tsx";
import { About } from "./pages/footer/about.tsx";
import Footer from "./pages/footer/footer";
import { Imprint } from "./pages/footer/imprint.tsx";
import { Licenses } from "./pages/footer/licenses.tsx";
import { PrivacyPolicy } from "./pages/footer/privacyPolicy.tsx";

const App: FC = () => {
  const [isSubMenuOpen, setIsSubMenuOpen] = useState(false);
  const { isLoading, isAdmin } = useGeopilotAuth();

  return (
    <AppBox>
      <BrowserRouter>
        <ControlledNavigateProvider>
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
                        <Route path="users/:id" element={<UserDetail />} />
                        <Route path="mandates" element={<Mandates />} />
                        <Route path="mandates/:id" element={<MandateDetail />} />
                        <Route path="organisations" element={<Organisations />} />
                        <Route path="organisations/:id" element={<OrganisationDetail />} />
                      </Route>
                    </>
                  ) : (
                    <Route path="admin/*" element={<Navigate to="/" replace />} />
                  )}
                  <Route path="/imprint" element={<Imprint />} />
                  <Route path="/privacy-policy" element={<PrivacyPolicy />} />
                  <Route path="/about" element={<About />} />
                  <Route path="/licenses" element={<Licenses />} />
                </Routes>
              )}
            </PageContentBox>
            <Footer />
          </LayoutBox>
        </ControlledNavigateProvider>
      </BrowserRouter>
    </AppBox>
  );
};

export default App;
