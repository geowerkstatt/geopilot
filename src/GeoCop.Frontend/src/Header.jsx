import { Button, Navbar, Nav, Container } from "react-bootstrap";
import { AuthenticatedTemplate, UnauthenticatedTemplate } from "@azure/msal-react";
import { NavLink } from "react-router-dom";
import { useAuth } from "./contexts/auth";
import { AdminTemplate } from "./AdminTemplate";

export const Header = ({ clientSettings }) => {
  const { user, login, logout } = useAuth();

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
                <AdminTemplate>
                  <NavLink className="nav-link" to="/admin">
                    ABGABEÜBERSICHT
                  </NavLink>
                  <a className="nav-link" href="/browser">
                    STAC BROWSER
                  </a>
                </AdminTemplate>
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
                <div className="user-info">Angemeldet als {user?.name}</div>
              </AuthenticatedTemplate>
            </div>
          </Navbar.Collapse>
        </Container>
      </Navbar>
    </header>
  );
};

export default Header;
