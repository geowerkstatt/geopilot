import React, { Dispatch } from "react";
import { User } from "../AppInterfaces.ts";

export interface AuthContextInterface {
  user: User | undefined;
  login: () => void;
  logout: () => void;
}

export interface AuthProviderProps {
  children: React.ReactNode;
  authScopes: string[];
  onLoginError: Dispatch<React.SetStateAction<string>>;
}
