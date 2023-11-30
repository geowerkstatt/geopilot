import { useAuth } from ".";

export const LoggedInTemplate = ({ children }) => {
  const { user } = useAuth();

  return user ? children : null;
};
