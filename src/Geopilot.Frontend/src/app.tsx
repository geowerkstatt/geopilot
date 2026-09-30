import "./app.css";
import { FC, useRef, useState } from "react";
import { BrowserRouter, Navigate, Outlet, Route, Routes } from "react-router-dom";
import { CircularProgress } from "@mui/material";
import { useGeopilotAuth } from "./auth";
import { useCapabilities } from "./components/capabilities/capabilitiesInterface.ts";
import { CapabilitiesProvider } from "./components/capabilities/capabilitiesProvider.tsx";
import { ControlledNavigateProvider } from "./components/controlledNavigate/controlledNavigateProvider.tsx";
import Header from "./components/header/header";
import { FullPageStack, PageContent, ScrollableContent } from "./components/styledComponents";
import { StepSwipeHandlers } from "./hooks/useStepSwipe";
import Admin from "./pages/admin/admin";
import { DeliveryOverview } from "./pages/admin/deliveries/deliveryOverview.tsx";
import MachineClientDetail from "./pages/admin/machineClients/machineClientDetail.tsx";
import MachineClients from "./pages/admin/machineClients/machineClients.tsx";
import MandateDetail from "./pages/admin/mandates/mandateDetail.tsx";
import Mandates from "./pages/admin/mandates/mandates.tsx";
import OrganisationDetail from "./pages/admin/organisations/organisationDetail.tsx";
import Organisations from "./pages/admin/organisations/organisations.tsx";
import UserDetail from "./pages/admin/users/userDetail.tsx";
import Users from "./pages/admin/users/users.tsx";
import Delivery from "./pages/delivery/delivery";
import { DeliveryProvider } from "./pages/delivery/deliveryProvider.tsx";
import { About } from "./pages/footer/about.tsx";
import Footer from "./pages/footer/footer";
import { Imprint } from "./pages/footer/imprint.tsx";
import { Licenses } from "./pages/footer/licenses.tsx";
import { PrivacyPolicy } from "./pages/footer/privacyPolicy.tsx";
import { UserDeliveryOverview } from "./pages/user/deliveries/userDeliveryOverview.tsx";

// Without machine delivery the administration of the clients has no server side either, so a bookmark or a
// typed address leads back instead of onto a page that can only fail. Rendered inside CapabilitiesProvider,
// which holds its children back until it knows, so this never redirects on a half-loaded state.
const MachineDeliveryRoutes: FC = () => {
  const { machineDeliveryEnabled } = useCapabilities();

  return machineDeliveryEnabled ? <Outlet /> : <Navigate to="/admin" replace />;
};

const App: FC = () => {
  const [isSubMenuOpen, setIsSubMenuOpen] = useState(false);
  const { isLoading, isAdmin, user } = useGeopilotAuth();
  const stepSwipeRef = useRef<StepSwipeHandlers | null>(null);

  return (
    <FullPageStack>
      <BrowserRouter>
        <ControlledNavigateProvider>
          <Header
            openSubMenu={() => {
              setIsSubMenuOpen(true);
            }}
          />
          <ScrollableContent
            onTouchStart={event => stepSwipeRef.current?.onTouchStart(event)}
            onTouchEnd={event => stepSwipeRef.current?.onTouchEnd(event)}
            onWheel={event => stepSwipeRef.current?.onWheel(event)}>
            <Routes>
              <Route
                path="/"
                element={
                  <DeliveryProvider>
                    <Delivery stepSwipeRef={stepSwipeRef} />
                  </DeliveryProvider>
                }
              />
              <Route path="/imprint" element={<Imprint />} />
              <Route path="/privacy-policy" element={<PrivacyPolicy />} />
              <Route path="/about" element={<About />} />
              <Route path="/licenses" element={<Licenses />} />
              {isLoading ? (
                <Route
                  path="*"
                  element={
                    <PageContent>
                      <CircularProgress />
                    </PageContent>
                  }
                />
              ) : (
                <>
                  {user ? (
                    <Route path="user">
                      <Route index element={<Navigate to="/user/deliveries" replace />} />
                      <Route path="deliveries" element={<UserDeliveryOverview />} />
                    </Route>
                  ) : (
                    <Route path="user/*" element={<Navigate to="/" replace />} />
                  )}
                  {isAdmin ? (
                    <Route
                      path="admin"
                      element={
                        <CapabilitiesProvider>
                          <Admin isSubMenuOpen={isSubMenuOpen} setIsSubMenuOpen={setIsSubMenuOpen} />
                        </CapabilitiesProvider>
                      }>
                      <Route index element={<Navigate to="/admin/delivery-overview" replace />} />
                      <Route path="delivery-overview" element={<DeliveryOverview />} />
                      <Route path="users" element={<Users />} />
                      <Route path="users/:id" element={<UserDetail />} />
                      <Route path="mandates" element={<Mandates />} />
                      <Route path="mandates/:id" element={<MandateDetail />} />
                      <Route path="organisations" element={<Organisations />} />
                      <Route path="organisations/:id" element={<OrganisationDetail />} />
                      <Route element={<MachineDeliveryRoutes />}>
                        <Route path="machine-clients" element={<MachineClients />} />
                        <Route path="machine-clients/:id" element={<MachineClientDetail />} />
                      </Route>
                    </Route>
                  ) : (
                    <Route path="admin/*" element={<Navigate to="/" replace />} />
                  )}
                </>
              )}
            </Routes>
            <Footer />
          </ScrollableContent>
        </ControlledNavigateProvider>
      </BrowserRouter>
    </FullPageStack>
  );
};

export default App;
