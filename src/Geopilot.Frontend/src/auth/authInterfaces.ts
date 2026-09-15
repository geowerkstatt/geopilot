import { User } from "../api/generated";

export interface GeopilotAuthContextInterface {
  authLoaded: boolean;
  isLoading: boolean;
  user: User | null | undefined;
  isAdmin: boolean;
  login: () => void;
  logout: () => void;
}

export interface AuthSettings {
  authority: string;
  publicClientId: string;
  scope: string;
}
