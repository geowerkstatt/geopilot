import { Button, Navbar, Nav, Container } from "react-bootstrap";
import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { NavLink } from "react-router-dom";

export const Header = ({ clientSettings }) => {
  const { instance } = useMsal();
  const activeAccount = instance.getActiveAccount();

  async function login() {
    try {
      const result = await instance.loginPopup({
        scopes: clientSettings?.authScopes,
      });
      instance.setActiveAccount(result.account);
      document.cookie = `geocop.auth=${result.idToken};Path=/;Secure`;
    } catch (error) {
      console.warn(error);
    }
  }

  async function logout() {
    try {
      await instance.logoutPopup();
      document.cookie = "geocop.auth=;expires=Thu, 01 Jan 1970 00:00:00 GMT;Path=/;Secure";
    } catch (error) {
      console.warn(error);
    }
  }

  return (
    <header>
      <Navbar expand="md" className="full-width justify-content-between" sticky="top">
        <Container fluid>
          {clientSettings?.vendor?.logo && (
          <Navbar.Brand href={clientSettings?.vendor?.url} target="_blank" rel="noreferrer">
                <img
                  className="vendor-logo"
                  src={clientSettings?.vendor?.logo}
                  alt={`Logo of ${clientSettings?.vendor?.name}`}
                  onError={(e) => {
                    e.target.style.display = "none";
                  }}
                />
            </Navbar.Brand>
            )}
          <Navbar.Toggle aria-controls="navbar-nav" />
          <Navbar.Collapse id="navbar-nav">
            <div className="navbar-container">
              <Nav className="full-width mr-auto" navbarScroll>
                <NavLink className="nav-link" to="/">
                  DATENABGABE
                </NavLink>
                <AuthenticatedTemplate>
                  <NavLink className="nav-link" to="/admin">
                    ABGABEÜBERSICHT
                  </NavLink>
                  <NavLink className="nav-link" to="/browser">
                    STAC BROWSER
                  </NavLink>
                </AuthenticatedTemplate>
              </Nav>
              <Nav>
                <UnauthenticatedTemplate>
                  <Button className="nav-button" onClick={login}>
                    ANMELDEN
                  </Button>
                </UnauthenticatedTemplate>
                <AuthenticatedTemplate>
                  <Button className="nav-button" onClick={logout}>
                    ABMELDEN
                  </Button>
                </AuthenticatedTemplate>
              </Nav>
            </div>
            <div className="navbar-info-container">
              <AuthenticatedTemplate>
                <div className="user-info">Angemeldet als {activeAccount?.name}</div>
              </AuthenticatedTemplate>
            </div>
          </Navbar.Collapse>
        </Container>
      </Navbar>
    </header>
  );
};

export default Header;
