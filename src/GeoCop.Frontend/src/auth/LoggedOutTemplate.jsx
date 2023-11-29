import { useAuth } from ".";

export const LoggedOutTemplate = ({ children }) => {
  const { user } = useAuth();

  return user ? null : children;
};
