import { Button, Navbar, Nav, Container } from "react-bootstrap";
import { NavLink } from "react-router-dom";
import { useAuth } from "./auth";
import { useTranslation } from "react-i18next";
import { AdminTemplate } from "./auth/AdminTemplate";
import { LoggedInTemplate } from "./auth/LoggedInTemplate";
import { LoggedOutTemplate } from "./auth/LoggedOutTemplate";

export const Header = ({ clientSettings }) => {
  const { user, login, logout } = useAuth();
  const { t } = useTranslation();

  return (
    <header>
      <Navbar expand="md" className="full-width justify-content-between" sticky="top">
        <Container fluid className="align-items-baseline">
          {clientSettings?.vendor?.logo && (
            <Navbar.Brand href={clientSettings?.vendor?.url} target="_blank" rel="noreferrer">
              <img
                className="vendor-logo"
                src={clientSettings?.vendor?.logo}
                alt={`Logo of ${clientSettings?.vendor?.name}`}
                onError={e => {
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
                  {t("delivery").toUpperCase()}
                </NavLink>
                <AdminTemplate>
                  <NavLink className="nav-link" to="/admin">
                    {t("deliveryOverview").toUpperCase()}
                  </NavLink>
                  <a className="nav-link" href="/browser">
                    {t("stacBrowser").toUpperCase()}
                  </a>
                </AdminTemplate>
              </Nav>
              <Nav>
                <LoggedOutTemplate>
                  <Button className="nav-button" onClick={login}>
                    {t("logIn")}
                  </Button>
                </LoggedOutTemplate>
                <LoggedInTemplate>
                  <Button className="nav-button" onClick={logout}>
                    {t("logOut")}
                  </Button>
                </LoggedInTemplate>
              </Nav>
            </div>
            <div className="navbar-info-container">
              <LoggedInTemplate>
                <div className="user-info">{t("loggedInAs", { name: user?.name })}</div>
              </LoggedInTemplate>
            </div>
          </Navbar.Collapse>
        </Container>
      </Navbar>
    </header>
  );
};

export default Header;
