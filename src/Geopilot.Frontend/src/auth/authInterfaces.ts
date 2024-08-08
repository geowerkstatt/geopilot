import { PropsWithChildren } from "react";
import { User } from "../api/apiInterfaces.ts";

export interface GeopilotAuthContextInterface {
  enabled: boolean;
  user: User | undefined;
  isAdmin: boolean;
  isLoggedIn: boolean;
  login: () => void;
  logout: () => void;
}

export interface GeopilotAuthComponentProps extends PropsWithChildren {
  authScopes: string[];
}

export interface OidcContainerProps extends PropsWithChildren {
  authScopes: string[];
}

export interface AuthSettings {
  authority: string;
  clientId: string;
}
