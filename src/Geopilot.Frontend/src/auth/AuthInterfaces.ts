import { PropsWithChildren } from "react";

export interface User {
  id: number;
  fullName: string;
  isAdmin: boolean;
  email: string;
}

export interface IGeopilotAuthContext {
  enabled: boolean;
  user: User | undefined;
  login: () => void;
  logout: () => void;
}

export interface GeopilotAuthComponentProps extends PropsWithChildren {
  authScopes: string[];
}

export interface OidcContainerProps extends PropsWithChildren{
  authScopes: string[];
}

export interface AuthSettings {
  authority: string;
  clientId: string;
}
