import { Button, Navbar, Nav, Container } from "react-bootstrap";
import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";

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
      document.cookie =
        "geocop.auth=;expires=Thu, 01 Jan 1970 00:00:00 GMT;Path=/;Secure";
    } catch (error) {
      console.warn(error);
    }
  }

  return (
    <header>
      <Navbar
        expand="md"
        className="full-width justify-content-between"
        sticky="top"
      >
        <Container fluid>
          <Navbar.Brand
            href={clientSettings?.vendor?.url}
            target="_blank"
            rel="noreferrer"
          >
            {clientSettings?.vendor?.logo && (
              <a
                href={clientSettings?.vendor?.url}
                target="_blank"
                rel="noreferrer"
              >
                <img
                  className="vendor-logo"
                  src={clientSettings?.vendor?.logo}
                  alt={`Logo of ${clientSettings?.vendor?.name}`}
                  onError={(e) => {
                    e.target.style.display = "none";
                  }}
                />
              </a>
            )}
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="navbar-nav" />
          <Navbar.Collapse id="navbar-nav">
            <Nav className="full-width mr-auto" navbarScroll>
              <Nav.Link href="/">DATENABGABE</Nav.Link>
              <AuthenticatedTemplate>
                <Nav.Link href="/admin">ABGABEÜBERSICHT</Nav.Link>
                <Nav.Link href="/browser">STAC BROWSER</Nav.Link>
              </AuthenticatedTemplate>
            </Nav>
            <Nav>
              <UnauthenticatedTemplate>
                <Button className="nav-button" onClick={login}>
                  ANMELDEN
                </Button>
              </UnauthenticatedTemplate>
              <AuthenticatedTemplate>
                <div className="logged-in-button">
                  <Button className="nav-button" onClick={logout}>
                    ABMELDEN
                  </Button>
                  <div className="user-info">
                    Angemeldet als {activeAccount?.username}
                  </div>
                </div>
              </AuthenticatedTemplate>
            </Nav>
          </Navbar.Collapse>
        </Container>
      </Navbar>
    </header>
  );
};

export default Header;
