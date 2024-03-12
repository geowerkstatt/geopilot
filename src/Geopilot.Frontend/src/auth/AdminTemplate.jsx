import { useAuth } from ".";

export const AdminTemplate = ({ children }) => {
  const { user } = useAuth();

  return user?.isAdmin ? children : null;
};
