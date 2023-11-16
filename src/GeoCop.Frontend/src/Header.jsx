import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { Button } from "react-bootstrap";
import styled from "styled-components";
import "./app.css";

const AccountContainer = styled.div`
  align-items: center;
  display: flex;
`;

const AccountNameContainer = styled.span`
  margin-right: 10px;
`;

const LoginButton = styled(Button)`
  font-family: "Dosis", sans-serif;
`;

export const Header = ({ clientSettings }) => {
  const { instance } = useMsal();
  const activeAccount = instance.getActiveAccount();

  async function login() {
    try {
      const result = await instance.loginPopup({
        scopes: clientSettings?.authScopes,
      });
      instance.setActiveAccount(result.account);
    } catch (error) {
      console.warn(error);
    }
  }

  async function logout() {
    try {
      await instance.logoutPopup();
    } catch (error) {
      console.warn(error);
    }
  }

  return (
    <header>
      {clientSettings?.vendor?.logo && (
        <a href={clientSettings?.vendor?.url} target="_blank" rel="noreferrer">
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
      <AccountContainer>
        <UnauthenticatedTemplate>
          <LoginButton onClick={login}>Log in</LoginButton>
        </UnauthenticatedTemplate>
        <AuthenticatedTemplate>
          <AccountNameContainer>{activeAccount?.username}</AccountNameContainer>
          <LoginButton onClick={logout}>Log out</LoginButton>
        </AuthenticatedTemplate>
      </AccountContainer>
    </header>
  );
};

export default Header;
