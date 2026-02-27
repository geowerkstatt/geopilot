import { User } from "../api/apiInterfaces.ts";

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
  clientAudience: string;
  fullScope: string;
}
