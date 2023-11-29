import { AuthenticatedTemplate } from "@azure/msal-react";
import { useAuth } from "./contexts/auth";

export const AdminTemplate = ({ children }) => {
  const { user } = useAuth();

  return <AuthenticatedTemplate>{user?.isAdmin && children}</AuthenticatedTemplate>;
};
