import React, { Dispatch } from "react";

export interface User {
  id: number;
  fullName: string;
  isAdmin: boolean;
  email: string;
}

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
